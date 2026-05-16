using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using SmileyApi.Api.Endpoints;
using SmileyApi.Api.Middleware;
using SmileyApi.Api.Workers;
using SmileyApi.Core.Interfaces;
using SmileyApi.Core.Models;
using SmileyApi.Infrastructure.Data;
using SmileyApi.Infrastructure.Repositories;
using SmileyApi.Infrastructure.Services;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<SmileyDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IEstablishmentRepository, EstablishmentRepository>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<EstablishmentSyncService>();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<FodevareXmlParser>();
builder.Services.AddHostedService<XmlSyncWorker>();

builder.Services.AddOpenApi();

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

    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = 429;
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync(
            """{"error":{"code":"rate_limit_exceeded","message":"Daily request limit reached. Upgrade to Pro for higher limits."}}""", ct);
    };
});

var app = builder.Build();

app.UseStaticFiles();
app.MapOpenApi();
app.MapScalarApiReference();

app.UseMiddleware<ApiKeyMiddleware>();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .ExcludeFromDescription();

app.MapEstablishmentEndpoints();

app.Run();

public partial class Program { }
