namespace SmileyApi.Api.Middleware;

public class ApiKeyMiddleware(RequestDelegate next)
{
    // Phase 2: validate X-Api-Key header against SHA-256 hash in DB
    public Task InvokeAsync(HttpContext context) => next(context);
}
