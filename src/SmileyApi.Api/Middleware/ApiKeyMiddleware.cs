using SmileyApi.Core.Interfaces;
using System.Text.Json;

namespace SmileyApi.Api.Middleware;

public class ApiKeyMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory)
{
    private static readonly string[] PublicPaths = ["/health", "/openapi", "/scalar", "/admin", "/v1/leads", "/v1/business", "/v1/stripe", "/widget"];

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (PublicPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Api-Key", out var rawKey)
            || string.IsNullOrWhiteSpace(rawKey))
        {
            await WriteError(context, 401, "missing_api_key", "X-Api-Key header is required.");
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var apiKeyService = scope.ServiceProvider.GetRequiredService<IApiKeyService>();
        var apiKey = await apiKeyService.ValidateAsync(rawKey!, context.RequestAborted);

        if (apiKey is null)
        {
            await WriteError(context, 401, "invalid_api_key", "The provided API key is invalid or inactive.");
            return;
        }

        context.Items["ApiKey"] = apiKey;
        await next(context);
    }

    private static Task WriteError(HttpContext context, int status, string code, string message)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(
            JsonSerializer.Serialize(new { error = new { code, message } }));
    }
}
