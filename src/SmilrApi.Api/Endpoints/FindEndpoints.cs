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
/// URL design (see docs/business-opportunities.md "URL structure" and "Category hub pages" sections):
/// area, category, and business name are the human-facing/SEO-facing identifiers, with Navnelbnr (the
/// real, unique key) as a stable suffix:
///   /find/{area-slug}/                          -> hub page listing establishments in that area
///   /find/{area-slug}/{category-slug}           -> hub page, area x Pixibranche category
///   /find/{area-slug}/{business-slug}-{navnelbnr} -> canonical detail page (establishment has a City)
///   /find/{business-slug}-{navnelbnr}             -> canonical detail page (establishment has no City)
/// At both the one-segment and two-segment-under-an-area depth, a hub-page slug and a detail-page slug
/// occupy the same route shape, so each depth is mapped by one route ("/{segment}" and
/// "/{areaSlug}/{segment}" respectively) and disambiguated at runtime by whether the segment matches the
/// "{business-slug}-{navnelbnr}" shape — registering them as separate routes throws
/// AmbiguousMatchException, since ASP.NET Core routing treats those as equally-specific candidates for
/// the same request.
/// Category hubs never host their own detail page (no /find/{area}/{category}/{business}-{id}) — they
/// link out to the flat canonical detail path above. Category is a controlled vocabulary (Pixibranche,
/// ~26 values from the source XML, "not yet assigned" placeholders excluded), so category-slug maps 1:1
/// to a raw value, unlike area-slug's many-raw-City-spellings-per-slug grouping.
/// Area-slug, business-slug, and category-slug are all computed on the fly (FindUrlBuilder) — never
/// persisted — so a URL whose slug text doesn't match what's freshly computed 301-redirects to the
/// canonical form (detail pages only; category/area hubs 404 on an unknown slug instead, since there's no
/// single canonical spelling to redirect to).
/// CVR is no longer part of any /find URL, but /find/search?q={8-digit-cvr} still resolves it as a
/// convenience shortcut (people know their CVR, not their Navnelbnr).
/// Establishments without a CVR are excluded from the directory entirely (no repo method surfaces them
/// here — every repo query below filters CvrNumber IS NOT NULL).
/// </summary>
public static class FindEndpoints
{
    // Danish CVR numbers are always exactly 8 digits — used to detect CVR-shaped free-text search queries.
    private static readonly Regex CvrPattern = new(@"^\d{8}$", RegexOptions.Compiled);

    // Splits "{business-slug}-{navnelbnr}" into its slug and trailing numeric id. Greedy `.+` backtracks
    // to the rightmost run of digits after a hyphen, so it still finds the true navnelbnr boundary even
    // when the slug itself contains digits (e.g. "7-eleven-12345" -> slug "7-eleven", navnelbnr "12345").
    private static readonly Regex DetailSlugPattern = new(@"^.+-(?<navnelbnr>\d+)$", RegexOptions.Compiled);

    // Establishment data only changes once per ~20-24h sync cycle (XmlSyncWorker), so a long TTL is safe.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(12);

    private const int SearchPageSize = 20;
    private const int HubPageSize = 20;

    // Below this establishment count, a category hub page still renders (useful to a direct visitor) but
    // is excluded from the sitemap and marked noindex, to keep thin area x category pages out of Google.
    private const int CategorySlugThreshold = 3;

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
                var categoryCounts = await repo.GetCityCategoryCountsAsync(ct);
                return FindPageRenderer.SitemapXml(entries, categoryCounts, CategorySlugThreshold);
            });
            return Results.Content(xml, "application/xml");
        });

        // Two segments under an area: either a detail page ({business-slug}-{navnelbnr}) or a category hub
        // ({category-slug}) — same shape as the top-level "/{segment}" ambiguity below, same fix: one
        // route, disambiguated at runtime by whether the segment matches the detail-slug shape.
        find.MapGet("/{areaSlug}/{segment}", (
            string areaSlug, string segment, int? page, HttpContext http,
            IMemoryCache cache, IEstablishmentRepository repo, CancellationToken ct) =>
                DetailSlugPattern.IsMatch(segment)
                    ? DetailHandlerAsync(segment, http, cache, repo, ct)
                    : CategoryHubHandlerAsync(areaSlug, segment, page, cache, repo, ct));

        // Single dynamic segment — ambiguous between an area hub ("/find/kobenhavn/") and a detail page
        // for an establishment with no City ("/find/{business-slug}-{navnelbnr}"). ASP.NET Core routing
        // treats a literal-trailing-slash template and a single-parameter template as ambiguous matches
        // for the same request (confirmed via AmbiguousMatchException during implementation), so both
        // cases are handled by one route, disambiguated at runtime by whether the segment ends in
        // "-{digits}" (the business-slug-navnelbnr shape).
        find.MapGet("/{segment}", (
            string segment, int? page, HttpContext http,
            IMemoryCache cache, IEstablishmentRepository repo, CancellationToken ct) =>
                DetailSlugPattern.IsMatch(segment)
                    ? DetailHandlerAsync(segment, http, cache, repo, ct)
                    : AreaHubHandlerAsync(segment, page, cache, repo, ct));
    }

    private static async Task<IResult> AreaHubHandlerAsync(
        string areaSlug, int? page,
        IMemoryCache cache, IEstablishmentRepository repo, CancellationToken ct)
    {
        var areaIndex = await GetAreaIndexAsync(cache, repo, ct);
        if (!areaIndex.TryGetValue(areaSlug, out var area))
            return Results.Content(
                FindPageRenderer.NotFoundPage($"No area found for '{areaSlug}'."),
                "text/html", statusCode: 404);

        var pageNum = Math.Max(page ?? 1, 1);
        var totalCount = await repo.CountByCitiesAsync(area.RawCityValues, ct);
        var establishments = await repo.GetByCitiesAsync(area.RawCityValues, pageNum, HubPageSize, ct);
        var categoriesInArea = await GetCategoriesInAreaAsync(area.RawCityValues, cache, repo, ct);

        return Results.Content(
            FindPageRenderer.AreaHubPage(
                area.DisplaySpelling, pageNum, HubPageSize, totalCount, establishments, categoriesInArea),
            "text/html");
    }

    /// <summary>Categories present anywhere in this area (raw category value + its category-slug + count),
    /// ordered by count desc — drives the area hub page's "Browse by category" section. Internal links here
    /// aren't threshold-gated: a category present at all is worth linking to from a page a visitor already
    /// reached, even if it's too thin to be individually indexed (see CategorySlugThreshold).</summary>
    private static async Task<IReadOnlyList<(string Category, string CategorySlug, int Count)>> GetCategoriesInAreaAsync(
        IReadOnlyList<string> areaCityValues, IMemoryCache cache, IEstablishmentRepository repo, CancellationToken ct)
    {
        var allTriples = await cache.GetOrCreateAsync("find:city-category-counts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await repo.GetCityCategoryCountsAsync(ct);
        });

        var citySet = areaCityValues.ToHashSet();
        return allTriples!
            .Where(t => citySet.Contains(t.City))
            .GroupBy(t => t.Category)
            .Select(g => (Category: g.Key, CategorySlug: FindUrlBuilder.CategorySlug(g.Key), Count: g.Sum(t => t.Count)))
            .OrderByDescending(c => c.Count)
            .ToList();
    }

    private static async Task<IResult> CategoryHubHandlerAsync(
        string areaSlug, string categorySlug, int? page,
        IMemoryCache cache, IEstablishmentRepository repo, CancellationToken ct)
    {
        var areaIndex = await GetAreaIndexAsync(cache, repo, ct);
        if (!areaIndex.TryGetValue(areaSlug, out var area))
            return Results.Content(
                FindPageRenderer.NotFoundPage($"No area found for '{areaSlug}'."),
                "text/html", statusCode: 404);

        var categoryIndex = await GetCategoryIndexAsync(cache, repo, ct);
        if (!categoryIndex.TryGetValue(categorySlug, out var category))
            return Results.Content(
                FindPageRenderer.NotFoundPage($"No category found for '{categorySlug}'."),
                "text/html", statusCode: 404);

        var pageNum = Math.Max(page ?? 1, 1);
        var totalCount = await repo.CountByCitiesAndCategoryAsync(area.RawCityValues, category, ct);
        if (totalCount == 0)
            return Results.Content(
                FindPageRenderer.NotFoundPage($"No establishments found for '{categorySlug}' in '{areaSlug}'."),
                "text/html", statusCode: 404);

        var establishments = await repo.GetByCitiesAndCategoryAsync(area.RawCityValues, category, pageNum, HubPageSize, ct);

        return Results.Content(
            FindPageRenderer.CategoryHubPage(
                area.DisplaySpelling, category, areaSlug, categorySlug,
                pageNum, HubPageSize, totalCount, noindex: totalCount < CategorySlugThreshold, establishments),
            "text/html");
    }

    private static async Task<IResult> DetailHandlerAsync(
        string slugAndId, HttpContext http,
        IMemoryCache cache, IEstablishmentRepository repo, CancellationToken ct)
    {
        var match = DetailSlugPattern.Match(slugAndId);
        if (!match.Success || !int.TryParse(match.Groups["navnelbnr"].Value, out var navnelbnr))
            return Results.Content(
                FindPageRenderer.NotFoundPage("No establishment found for that URL."),
                "text/html", statusCode: 404);

        var result = await cache.GetOrCreateAsync($"find:detail:{navnelbnr}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            var est = await repo.GetHistoryByNavnelbnrAsync(navnelbnr, ct);
            return est is null || est.CvrNumber is null
                ? new DetailLookupResult(null, null)
                : new DetailLookupResult(FindPageRenderer.DetailPage(est), FindUrlBuilder.DetailPath(est));
        });

        if (result!.Html is null)
            return Results.Content(
                FindPageRenderer.NotFoundPage($"No establishment found for Navnelbnr {navnelbnr}."),
                "text/html", statusCode: 404);

        // Canonical-redirect if the request path doesn't match the freshly-computed canonical path —
        // catches a stale business-name slug, a City that was added/changed/removed since the link was
        // generated, or a hand-typed/probing URL. Avoids duplicate-content signals for search engines.
        var requestPath = http.Request.Path.Value ?? "";
        if (!string.Equals(result.CanonicalPath, requestPath, StringComparison.Ordinal))
            return Results.Redirect(result.CanonicalPath!, permanent: true);

        return Results.Content(result.Html, "text/html");
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
                1 => new CvrLookupResult(FindUrlBuilder.DetailPath(locations[0]), null),
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

    private static async Task<IReadOnlyDictionary<string, AreaInfo>> GetAreaIndexAsync(
        IMemoryCache cache, IEstablishmentRepository repo, CancellationToken ct)
    {
        return (await cache.GetOrCreateAsync("find:area-index", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            var cityCounts = await repo.GetCityCountsAsync(ct);

            return cityCounts
                .GroupBy(c => FindUrlBuilder.AreaSlug(c.City))
                .ToDictionary(
                    g => g.Key,
                    g => new AreaInfo(
                        DisplaySpelling: g.OrderByDescending(c => c.Count).First().City,
                        RawCityValues: g.Select(c => c.City).ToList()));
        }))!;
    }

    private static async Task<IReadOnlyDictionary<string, string>> GetCategoryIndexAsync(
        IMemoryCache cache, IEstablishmentRepository repo, CancellationToken ct)
    {
        return (await cache.GetOrCreateAsync("find:category-index", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            var categoryCounts = await repo.GetCategoryCountsAsync(ct);

            // Pixibranche is a controlled vocabulary (unlike City), so this is a 1:1 map, not a grouping —
            // but slugify could still theoretically collapse two distinct category strings together, so
            // last-write-wins here is an accepted, unlikely edge case rather than something to guard on.
            return categoryCounts.ToDictionary(c => FindUrlBuilder.CategorySlug(c.Category), c => c.Category);
        }))!;
    }

    private sealed record CvrLookupResult(string? RedirectTo, string? Html);
    private sealed record DetailLookupResult(string? Html, string? CanonicalPath);
    private sealed record AreaInfo(string DisplaySpelling, IReadOnlyList<string> RawCityValues);
}
