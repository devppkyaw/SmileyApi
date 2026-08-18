using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using SmilrApi.Api.Rendering;
using SmilrApi.Core.Interfaces;

namespace SmilrApi.Api.Endpoints;

/// <summary>
/// Public, anonymous, SEO-indexable directory ("Smilr Finder") at /find. Bypasses ApiKeyMiddleware
/// (see PublicPaths in ApiKeyMiddleware.cs) and uses its own IP-based rate limit policy ("find-ip")
/// rather than the developer-API "api-key-tier" policy, since this surface has no API key.
///
/// URL design: CVR is the human-facing identifier (people know their CVR, not their Navnelbnr), with
/// Navnelbnr as a disambiguator for CVRs that have multiple locations:
///   /find/{cvr}             -> resolves to the single location's detail page, or a location list
///   /find/{cvr}/{navnelbnr} -> canonical per-location detail page
/// Establishments without a CVR are excluded from the directory entirely (no repo method surfaces them
/// here — GetAllForSitemapAsync already filters CvrNumber IS NOT NULL).
/// </summary>
public static class FindEndpoints
{
    // Danish CVR numbers are always exactly 8 digits — used to detect CVR-shaped free-text search queries.
    private static readonly Regex CvrPattern = new(@"^\d{8}$", RegexOptions.Compiled);

    // Establishment data only changes once per ~20-24h sync cycle (XmlSyncWorker), so a long TTL is safe.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(12);

    private const int SearchPageSize = 20;

    public static void MapFindEndpoints(this WebApplication app)
    {
        var find = app.MapGroup("/find")
                       .RequireRateLimiting("find-ip")
                       .ExcludeFromDescription();

        find.MapGet("/", () =>
            Results.Content(FindPageRenderer.SearchLandingPage(), "text/html"));

        find.MapGet("/search", async (
            string? q, int? page,
            IMemoryCache cache, IEstablishmentRepository repo, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(q))
                return Results.Redirect("/find");

            q = q.Trim();

            // People search by CVR, not by name — route CVR-shaped queries through the same
            // resolver as a direct /find/{cvr} visit instead of a text search.
            if (CvrPattern.IsMatch(q))
                return await ResolveCvrAsync(q, cache, repo, ct);

            var pageNum = Math.Max(page ?? 1, 1);
            var results = await repo.SearchAsync(q, pageNum, SearchPageSize, ct);
            return Results.Content(FindPageRenderer.SearchResultsPage(q, pageNum, SearchPageSize, results), "text/html");
        });

        find.MapGet("/sitemap.xml", async (IMemoryCache cache, IEstablishmentRepository repo, CancellationToken ct) =>
        {
            var xml = await cache.GetOrCreateAsync("find:sitemap", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;
                var entries = await repo.GetAllForSitemapAsync(ct);
                return FindPageRenderer.SitemapXml(entries);
            });
            return Results.Content(xml, "application/xml");
        });

        find.MapGet("/{cvr}", async (
            string cvr, IMemoryCache cache, IEstablishmentRepository repo, CancellationToken ct) =>
                await ResolveCvrAsync(cvr, cache, repo, ct));

        find.MapGet("/{cvr}/{navnelbnr:int}", async (
            string cvr, int navnelbnr,
            IMemoryCache cache, IEstablishmentRepository repo, CancellationToken ct) =>
        {
            var result = await cache.GetOrCreateAsync($"find:detail:{navnelbnr}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;
                var est = await repo.GetHistoryByNavnelbnrAsync(navnelbnr, ct);
                return est is null || est.CvrNumber is null
                    ? new DetailLookupResult(null, null)
                    : new DetailLookupResult(FindPageRenderer.DetailPage(est), est.CvrNumber);
            });

            if (result!.Html is null)
                return Results.Content(
                    FindPageRenderer.NotFoundPage($"No establishment found for Navnelbnr {navnelbnr}."),
                    "text/html", statusCode: 404);

            // Canonical-redirect if the URL's CVR segment doesn't match the establishment's actual CVR
            // (stale link, chain that changed CVR, or a probing/typo'd URL) — avoids duplicate-content
            // signals for search engines.
            if (!string.Equals(result.Cvr, cvr, StringComparison.Ordinal))
                return Results.Redirect($"/find/{result.Cvr}/{navnelbnr}", permanent: true);

            return Results.Content(result.Html, "text/html");
        });
    }

    private static async Task<IResult> ResolveCvrAsync(
        string cvr, IMemoryCache cache, IEstablishmentRepository repo, CancellationToken ct)
    {
        var result = await cache.GetOrCreateAsync($"find:cvr:{cvr}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            var locations = await repo.GetByCvrAsync(cvr, ct);

            return locations.Count switch
            {
                0 => new CvrLookupResult(null, null),
                1 => new CvrLookupResult($"/find/{cvr}/{locations[0].Navnelbnr}", null),
                _ => new CvrLookupResult(null, FindPageRenderer.LocationChoicePage(cvr, locations))
            };
        });

        if (result!.RedirectTo is not null)
            return Results.Redirect(result.RedirectTo, permanent: true);

        if (result.Html is not null)
            return Results.Content(result.Html, "text/html");

        return Results.Content(
            FindPageRenderer.NotFoundPage($"No establishments found for CVR '{cvr}'."),
            "text/html", statusCode: 404);
    }

    private sealed record CvrLookupResult(string? RedirectTo, string? Html);
    private sealed record DetailLookupResult(string? Html, string? Cvr);
}
