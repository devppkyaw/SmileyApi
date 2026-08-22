namespace SmilrApi.Api.Middleware;

/// <summary>
/// Redirects requests arriving on a non-canonical host (e.g. the raw *.azurecontainerapps.io Container
/// App hostname) to the same path+query on the configured canonical origin (Seo:CanonicalOrigin), with a
/// permanent redirect, so the platform-default hostname never becomes an indexed duplicate of the real
/// domain. Runs after UseForwardedHeaders so Host reflects X-Forwarded-Host correctly. Inert outside
/// Production (no canonical domain is DNS-live locally/in dev) and explicitly skips /health so Container
/// Apps' internal readiness/liveness probes are never redirected regardless of the Host header they use —
/// verify post-deploy that probes indeed don't send a Host header requiring this path anyway.
/// </summary>
public class CanonicalHostMiddleware(RequestDelegate next, IConfiguration configuration, IWebHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!env.IsProduction() || context.Request.Path.StartsWithSegments("/health"))
        {
            await next(context);
            return;
        }

        var canonicalOrigin = configuration["Seo:CanonicalOrigin"] ?? "https://smilrhq.dk";
        var canonicalHost = new Uri(canonicalOrigin).Host;

        if (!string.Equals(context.Request.Host.Host, canonicalHost, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Redirect($"{canonicalOrigin}{context.Request.Path}{context.Request.QueryString}", permanent: true);
            return;
        }

        await next(context);
    }
}
