using Azure.Identity;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Scalar.AspNetCore;
using SmilrApi.Api.Endpoints;
using SmilrApi.Api.Middleware;
using SmilrApi.Api.Rendering;
using SmilrApi.Api.Workers;
using SmilrApi.Core.Interfaces;
using SmilrApi.Core.Models;
using SmilrApi.Infrastructure.Data;
using SmilrApi.Infrastructure.Jobs;
using SmilrApi.Infrastructure.Repositories;
using SmilrApi.Infrastructure.Services;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Azure Container Apps terminates TLS at the ingress and forwards requests to the container
// over HTTP. Without this, ctx.Request.Scheme is "http", producing http:// magic link URLs.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                             | ForwardedHeaders.XForwardedProto
                             | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Wire up Key Vault as a config source when running on Azure.
// DefaultAzureCredential uses the App Service Managed Identity automatically;
// locally it falls back to Azure CLI / VS credentials.
var kvUri = builder.Configuration["AZURE_KEY_VAULT_URI"];
if (!string.IsNullOrEmpty(kvUri))
    builder.Configuration.AddAzureKeyVault(new Uri(kvUri), new DefaultAzureCredential());

builder.Services.AddDbContext<SmilrDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IEstablishmentRepository, EstablishmentRepository>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<IBusinessService, BusinessService>();
if (builder.Environment.IsDevelopment())
    builder.Services.AddScoped<IEmailService, DevEmailService>();
else
    builder.Services.AddScoped<IEmailService, AcsEmailService>();
builder.Services.AddScoped<EstablishmentSyncService>();
builder.Services.AddScoped<WebhookService>();
builder.Services.AddScoped<WebhookDeliveryJob>();
builder.Services.AddScoped<IStripeService, StripeService>();

builder.Services.AddDistributedMemoryCache();

// General-purpose in-process cache for the /find directory pages (protects Azure SQL Basic from
// repeated crawler traffic). Distinct from AddDistributedMemoryCache above, which only backs session
// state. In-process is sufficient at the current scale — the Container App runs a single replica
// (minReplicas = maxReplicas = 1), so there's no multi-instance cache-coherency concern.
builder.Services.AddMemoryCache();

// Compresses response bodies (Brotli preferred, Gzip fallback) — currently off entirely, so
// Accept-Encoding: gzip,br is silently ignored and hub/sitemap/detail pages ship uncompressed.
// Safe over HTTPS here: these are anonymous, no-auth pages/JSON with no secret (session ID, CSRF
// token, etc.) echoed back in the response body — the session cookie is HttpOnly and never appears
// in the body — so there's no BREACH-style compression-oracle target for this content.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly  = true;
    options.Cookie.SameSite  = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.IdleTimeout      = TimeSpan.FromDays(30);
});

builder.Services.AddHttpClient();
builder.Services.AddHttpClient("webhook", client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddHttpClient("fodevarestyrelsen", client =>
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Smilr/1.0 (+https://smilrhq.dk)"));
builder.Services.AddSingleton<FodevareXmlParser>();
builder.Services.AddHostedService<XmlSyncWorker>();

var connectionString = builder.Configuration.GetConnectionString("Default")!;
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    }));
builder.Services.AddHangfireServer();

builder.Services.AddCors(options =>
    options.AddPolicy("widget", policy =>
        policy.AllowAnyOrigin().WithMethods("GET").WithHeaders("Content-Type")));

builder.Services.AddOpenApi();

builder.Services.AddOpenApi("public", options =>
{
    options.AddDocumentTransformer((doc, ctx, ct) =>
    {
        var toRemove = doc.Paths
            .Where(p => p.Value.Operations.Values
                .All(op => op.Tags == null || !op.Tags.Any(t => t.Name == "Public")))
            .Select(p => p.Key)
            .ToList();
        foreach (var path in toRemove)
            doc.Paths.Remove(path);
        return Task.CompletedTask;
    });
});

var aiConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"]
    ?? builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
if (!string.IsNullOrEmpty(aiConnectionString))
    builder.Services.AddApplicationInsightsTelemetry();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;

    options.AddPolicy("api-key-tier", httpContext =>
    {
        var apiKey = httpContext.Items["ApiKey"] as ApiKey;
        int limit = apiKey?.Tier == "pro" ? 10_000 : 100;

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: apiKey?.Id.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit          = limit,
                Window               = TimeSpan.FromDays(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0
            });
    });

    options.AddPolicy("widget-ip", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit          = 60,
                Window               = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0
            });
    });

    // More generous than widget-ip: /find serves legitimate search-engine crawlers in addition to
    // human browsing, and most requests are served from cache rather than hitting the DB.
    options.AddPolicy("find-ip", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit          = 120,
                Window               = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0
            });
    });

    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = 429;
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync(
            """{"error":{"code":"rate_limit_exceeded","message":"Daily request limit reached."}}""", ct);
    };
});

var app = builder.Build();

FindPageRenderer.Configure(app.Configuration["Seo:CanonicalOrigin"] ?? "https://smilrhq.dk");

app.UseForwardedHeaders();

app.UseHttpsRedirection();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseResponseCompression();
app.UseMiddleware<CanonicalHostMiddleware>();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(
            exceptionFeature?.Error,
            "Unhandled exception on {Method} {Path}",
            context.Request.Method,
            context.Request.Path);
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            """{"error":{"code":"internal_error","message":"An unexpected error occurred."}}""");
    });
});

// Run any pending EF migrations on startup in non-dev environments.
// In dev, the developer runs migrations manually via dotnet ef database update.
if (!app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<SmilrDbContext>().Database.MigrateAsync();
}

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            ctx.Context.Response.Headers["Cache-Control"] = "no-cache";
    }
});
app.UseCors();
app.UseSession();

app.MapOpenApi();

app.MapScalarApiReference("/scalar/v1", options =>
{
    options.OpenApiRoutePattern = "/openapi/public.json";
    options.Title = "Smilr API";
    options.HideModels();
});

if (!app.Environment.IsProduction())
{
    app.MapScalarApiReference("/scalar/internal", options =>
    {
        options.OpenApiRoutePattern = "/openapi/v1.json";
        options.Title = "Smilr API — Internal";
        options.HideModels();
    });
    app.UseHangfireDashboard("/hangfire");
}

app.UseMiddleware<ApiKeyMiddleware>();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .ExcludeFromDescription();

app.MapEstablishmentEndpoints();
app.MapLeadsEndpoint();
app.MapAdminEndpoints();
app.MapWebhookEndpoints();
app.MapWidgetEndpoints();
app.MapBusinessEndpoints();
app.MapStripeEndpoints();
app.MapFindEndpoints();

app.Run();

public partial class Program { }
