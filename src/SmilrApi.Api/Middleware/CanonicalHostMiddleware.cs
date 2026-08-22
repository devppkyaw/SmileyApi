namespace SmilrApi.Api.Middleware;

/// <summary>
/// Redirects requests arriving on a non-canonical host (e.g. the raw *.azurecontainerapps.io Container
/// App hostname) to the same path+query on the configured canonical origin (Seo:CanonicalOrigin), with a
/// permanent redirect, so the platform-default hostname never becomes an indexed duplicate of the real
/// domain. Runs after UseForwardedHeaders so Host reflects X-Forwarded-Host correctly. Inert outside
/// Production (no canonical domain is DNS-live locally/in dev) and explicitly skips /health so Container
/// Apps' internal readiness/liveness probes are never redirected regardless of the Host header they use —
/// verify post-deploy that probes indeed don't send a Host header requiring this path anyway.
///
/// Also gated on Seo:EnforceCanonicalHost (default false), mirroring infra/parameters/prod.bicepparam's
/// own customDomainName gate: smilrhq.dk isn't bound to this Container App yet (it currently points at an
/// unrelated static site), so redirecting there unconditionally would 404 every single request — this
/// caused a real production outage before the flag was added. Flip it to true only once the custom domain
/// is actually DNS-live and bound (see prod.bicepparam's comment for the exact sequencing).
/// </summary>
public class CanonicalHostMiddleware(RequestDelegate next, IConfiguration configuration, IWebHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!env.IsProduction()
            || !configuration.GetValue<bool>("Seo:EnforceCanonicalHost")
            || context.Request.Path.StartsWithSegments("/health"))
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
