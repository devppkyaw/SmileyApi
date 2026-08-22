using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SmilrApi.Api.Endpoints;
using SmilrApi.Core.Interfaces;
using SmilrApi.Core.Models;
using SmilrApi.Core.Utils;

namespace SmilrApi.Api.Rendering;

/// <summary>
/// Hand-built HTML rendering for the /find directory pages. No templating engine is used here —
/// consistent with the rest of this codebase (CLAUDE.md: "No frontend framework... plain HTML/CSS
/// in wwwroot"). These pages must be crawler-reliable without JS execution, so unlike wwwroot's
/// client-fetch pages, everything here is rendered server-side into the initial HTML response.
/// </summary>
public static class FindPageRenderer
{
    // Set once at startup from Seo:CanonicalOrigin (see Program.cs). This file is a fully static,
    // non-DI rendering module by design, so a settable static — written exactly once before any
    // request is served — is used here instead of threading the origin through every render method.
    internal static string SiteOrigin { get; private set; } = "https://smilrhq.dk";

    internal static void Configure(string canonicalOrigin) => SiteOrigin = canonicalOrigin.TrimEnd('/');

    // Mirrors the SCORES map in wwwroot/widget.js and smilr-icons.js so /find pages show the same
    // badge as the widget: four score values, three faces (2 and 3 both render as the neutral Sm3).
    private static readonly Dictionary<int, string> ScoreFace = new()
    {
        [1] = "Sm1",
        [2] = "Sm3",
        [3] = "Sm3",
        [4] = "Sm4"
    };

    /// <summary>Smiley_figurer path for a score at a given size, following smilr-icons.js's
    /// dir/suffix convention: 150px uses the "bg" (big) suffix, 32px uses "cg" (compact).</summary>
    private static string ScoreImagePath(int score, int size) =>
        $"/Smiley_figurer/{size}/{ScoreFace.GetValueOrDefault(score, "Sm4")}{(size == 150 ? "bg" : "cg")}.jpg";

    public static string SearchLandingPage()
    {
        var body = $"""
            <h1>Find a restaurant's food inspection score</h1>
            <p class="section-sub">Search by CVR number or business name. Data is official, sourced from Fødevarestyrelsen.</p>
            {SearchFormHtml(query: null)}
            """;
        return Layout(
            title: "Find a restaurant's Smilr score — SmilrHQ",
            description: "Search Danish food inspection (smiley) scores by restaurant name or CVR number. Official data from Fødevarestyrelsen.",
            canonicalPath: "/find",
            bodyHtml: body);
    }

    public static string SearchResultsPage(string query, int page, int limit, IReadOnlyList<Establishment> results)
    {
        var linkable = results.Where(e => e.CvrNumber is not null).ToList();

        var rowsHtml = linkable.Count == 0
            ? "<p>No matching establishments found.</p>"
            : string.Join("\n", linkable.Select(ResultRowHtml));

        var searchBasePath = $"/find/search?q={Uri.EscapeDataString(query)}&";
        var hasMore = results.Count >= limit;
        var pagerHtml = BuildPager(searchBasePath, page, hasMore);
        var (prevPath, nextPath) = PagerLinks(searchBasePath, page, hasMore);

        var body = $"""
            <h1>Search results for "{E(query)}"</h1>
            {SearchFormHtml(query)}
            <div class="find-results">
            {rowsHtml}
            </div>
            {pagerHtml}
            """;

        return Layout(
            title: $"\"{query}\" — Search results — SmilrHQ",
            description: $"Food inspection scores matching \"{query}\" — official Fødevarestyrelsen data.",
            canonicalPath: page > 1
                ? $"/find/search?q={Uri.EscapeDataString(query)}&page={page}"
                : $"/find/search?q={Uri.EscapeDataString(query)}",
            bodyHtml: body,
            prevPath: prevPath,
            nextPath: nextPath);
    }

    public static string LocationChoicePage(string cvr, IReadOnlyList<Establishment> locations)
    {
        var rowsHtml = string.Join("\n", locations.Select(ResultRowHtml));
        var body = $"""
            <h1>Locations for CVR {E(cvr)}</h1>
            <p class="section-sub">This CVR has {locations.Count} registered locations. Choose one to see its score and inspection history.</p>
            <div class="find-results">
            {rowsHtml}
            </div>
            """;

        return Layout(
            title: $"CVR {cvr} — {locations.Count} locations — SmilrHQ",
            description: $"CVR {cvr} has {locations.Count} registered food establishments in Denmark. View each location's inspection score.",
            canonicalPath: $"/find/search?q={Uri.EscapeDataString(cvr)}",
            bodyHtml: body);
    }

    public static string AreaHubPage(
        string displaySpelling, int page, int pageSize, int totalCount, bool noindex,
        IReadOnlyList<Establishment> establishments,
        IReadOnlyList<(string Category, string CategorySlug, int Count)> categoriesInArea)
    {
        var rowsHtml = establishments.Count == 0
            ? "<p>No establishments found for this area yet.</p>"
            : string.Join("\n", establishments.Select(ResultRowHtml));

        var hasMore = (long)page * pageSize < totalCount;
        var hubPath = FindUrlBuilder.HubPath(displaySpelling);
        var pagerHtml = BuildPager($"{hubPath}?", page, hasMore);
        var (prevPath, nextPath) = PagerLinks($"{hubPath}?", page, hasMore);
        var categoryNavHtml = CategoryNavHtml(displaySpelling, categoriesInArea);

        var body = $"""
            <h1>Restaurants &amp; food businesses in {E(displaySpelling)}</h1>
            <p class="section-sub">{totalCount} registered establishment{(totalCount == 1 ? "" : "s")} with official Fødevarestyrelsen inspection scores.</p>
            <div style="display:flex;flex-wrap:wrap;gap:8px;margin-bottom:24px">
              <a href="{FindUrlBuilder.RecentlyInspectedPath(displaySpelling)}" class="city-tag">Recently inspected in {E(displaySpelling)} →</a>
              <a href="{FindUrlBuilder.ChangesPath(displaySpelling)}" class="city-tag">Recent score changes in {E(displaySpelling)} →</a>
            </div>
            {categoryNavHtml}
            <div class="find-results">
            {rowsHtml}
            </div>
            {pagerHtml}
            """;

        return Layout(
            title: $"Food inspection scores in {displaySpelling} — SmilrHQ",
            description: $"Browse official Fødevarestyrelsen food inspection (smiley) scores for restaurants and food businesses in {displaySpelling}.",
            canonicalPath: hubPath,
            bodyHtml: body,
            noindex: noindex,
            prevPath: prevPath,
            nextPath: nextPath);
    }

    public static string CategoryHubPage(
        string displayCity, string displayCategory, string areaSlug, string categorySlug,
        int page, int pageSize, int totalCount, bool noindex, IReadOnlyList<Establishment> establishments)
    {
        var rowsHtml = establishments.Count == 0
            ? "<p>No establishments found for this category in this area yet.</p>"
            : string.Join("\n", establishments.Select(ResultRowHtml));

        var hasMore = (long)page * pageSize < totalCount;
        var categoryPath = FindUrlBuilder.CategoryHubPath(displayCity, displayCategory);
        var pagerHtml = BuildPager($"{categoryPath}?", page, hasMore);
        var (prevPath, nextPath) = PagerLinks($"{categoryPath}?", page, hasMore);

        var jsonLdCrumbs = new List<(string Name, string Path)>
        {
            ("Find", "/find"),
            (displayCity, FindUrlBuilder.HubPath(displayCity)),
            (displayCategory, categoryPath)
        };

        var body = $"""
            <nav aria-label="breadcrumb" style="margin-bottom:16px;font-size:0.9rem">
              <a href="/find">Find</a> › <a href="{FindUrlBuilder.HubPath(displayCity)}">{E(displayCity)}</a> › {E(displayCategory)}
            </nav>
            <h1>{E(displayCategory)} in {E(displayCity)}</h1>
            <p class="section-sub">{totalCount} registered establishment{(totalCount == 1 ? "" : "s")} with official Fødevarestyrelsen inspection scores.</p>
            <div class="find-results">
            {rowsHtml}
            </div>
            {pagerHtml}
            """;

        return Layout(
            title: $"{displayCategory} in {displayCity} — SmilrHQ",
            description: $"Browse official Fødevarestyrelsen food inspection (smiley) scores for {displayCategory.ToLowerInvariant()} in {displayCity}.",
            canonicalPath: categoryPath,
            bodyHtml: body,
            noindex: noindex,
            extraHeadHtml: BreadcrumbJsonLd(jsonLdCrumbs),
            prevPath: prevPath,
            nextPath: nextPath);
    }

    private static string CategoryNavHtml(
        string displayCity, IReadOnlyList<(string Category, string CategorySlug, int Count)> categoriesInArea)
    {
        if (categoriesInArea.Count == 0) return "";

        var links = categoriesInArea.Select(c =>
            $"""<a href="{FindUrlBuilder.CategoryHubPath(displayCity, c.Category)}">{E(c.Category)} ({c.Count})</a>""");

        return $"""
            <div class="find-category-nav">
              <span class="find-category-nav-label">Browse by category:</span>
              {string.Join("\n  ", links)}
            </div>
            """;
    }

    /// <summary>/find/{area-slug}/recently-inspected — establishments in an area ordered by their most
    /// recent recorded inspection date. Every figure here (city name, counts, dates, rows) comes from
    /// the caller's live repository query results for this request; nothing is hard-coded.</summary>
    public static string RecentlyInspectedPage(
        string displaySpelling, int page, int pageSize, int areaTotalCount, RecentlyInspectedSummary summary,
        bool noindex, IReadOnlyList<Establishment> establishments,
        IReadOnlyList<(string Category, string CategorySlug, int Count)> categoriesInArea)
    {
        var rowsHtml = establishments.Count == 0
            ? """<tr><td colspan="4">No inspection records are currently available for this area.</td></tr>"""
            : RecentlyInspectedRowsHtml(establishments);

        var hasMore = (long)page * pageSize < summary.TotalWithInspection;
        var recentPath = FindUrlBuilder.RecentlyInspectedPath(displaySpelling);
        var pagerHtml = BuildPager($"{recentPath}?", page, hasMore);
        var (prevPath, nextPath) = PagerLinks($"{recentPath}?", page, hasMore);
        var categoryNavHtml = CategoryNavHtml(displaySpelling, categoriesInArea);

        var latestDateText = summary.LatestInspectionDate is { } d ? FormatDate(d) : null;
        var introText = latestDateText is null
            ? $"Smilr lists food establishments in {E(displaySpelling)} by the date of their latest recorded food inspection."
            : $"""Smilr lists food establishments in {E(displaySpelling)} by the date of their latest recorded food inspection. The latest recorded inspection in this list took place on <strong>{latestDateText}</strong>.""";

        var jsonLdCrumbs = new List<(string Name, string Path)>
        {
            ("Find", "/find"),
            (displaySpelling, FindUrlBuilder.HubPath(displaySpelling)),
            ("Recently inspected", recentPath)
        };

        var body = $"""
            <nav aria-label="breadcrumb" style="margin-bottom:16px;font-size:0.9rem">
              <a href="/find">Find</a> › <a href="{FindUrlBuilder.HubPath(displaySpelling)}">{E(displaySpelling)}</a> › Recently inspected
            </nav>
            <h1>Recently inspected food businesses in {E(displaySpelling)}</h1>
            <p class="section-sub">See the establishments in {E(displaySpelling)} with the most recent recorded food inspections.</p>
            <p class="section-sub">Updated from the latest available Fødevarestyrelsen data.</p>

            <div class="find-stats-card">
              <div class="find-stats-label">{E(displaySpelling)}</div>
              <div class="find-stats-grid">
                <div class="find-stat">
                  <div class="find-stat-value">{areaTotalCount}</div>
                  <div class="find-stat-label">food establishment{(areaTotalCount == 1 ? "" : "s")}</div>
                </div>
                <div class="find-stat">
                  <div class="find-stat-value">{latestDateText ?? "—"}</div>
                  <div class="find-stat-label">latest inspection</div>
                </div>
                <div class="find-stat">
                  <div class="find-stat-value">{summary.Last30DaysCount}</div>
                  <div class="find-stat-label">inspection{(summary.Last30DaysCount == 1 ? "" : "s")} in the last 30 days</div>
                </div>
                <div class="find-stat">
                  <div class="find-stat-value">{establishments.Count}</div>
                  <div class="find-stat-label">shown on this page</div>
                </div>
              </div>
            </div>

            <p class="section-sub">{introText}</p>

            <div class="find-recent-table-wrap">
              <table class="find-recent-table">
                <thead>
                  <tr><th class="n">#</th><th>Business</th><th class="cat-col">Category</th><th class="score-col">Score</th><th class="chev-col"></th></tr>
                </thead>
                <tbody>
                {rowsHtml}
                </tbody>
              </table>
            </div>
            {pagerHtml}
            <div style="display:flex;flex-wrap:wrap;gap:8px;margin:16px 0">
              <a href="{FindUrlBuilder.HubPath(displaySpelling)}" class="city-tag">View all food businesses in {E(displaySpelling)} →</a>
              <a href="{FindUrlBuilder.ChangesPath(displaySpelling)}" class="city-tag">See businesses whose inspection score recently changed →</a>
            </div>
            {categoryNavHtml}
            <div class="find-business-cta">
              <h2>Own a food business?</h2>
              <p>Monitor your inspection score automatically with Smilr Business.</p>
              <a href="/register.html" class="btn-primary">Learn about Smilr Business →</a>
            </div>
            """;

        return Layout(
            title: $"Recently Inspected Food Businesses in {displaySpelling} — SmilrHQ",
            description: $"See the food businesses most recently inspected in {displaySpelling}. Browse official food inspection scores, inspection dates and historical results on Smilr.",
            canonicalPath: recentPath,
            bodyHtml: body,
            noindex: noindex,
            extraHeadHtml: BreadcrumbJsonLd(jsonLdCrumbs),
            prevPath: prevPath,
            nextPath: nextPath);
    }

    /// <summary>Renders the recently-inspected table body, inserting a full-width date-header row
    /// whenever LatestScoreDate changes from the row before it — same-date establishments stay grouped
    /// under one heading, ordered alphabetically beneath it (see the repository's ThenBy(Name) sort).</summary>
    private static string RecentlyInspectedRowsHtml(IReadOnlyList<Establishment> establishments)
    {
        var sb = new StringBuilder();
        DateOnly? lastDate = null;
        var n = 0;
        foreach (var e in establishments)
        {
            n++;
            if (e.LatestScoreDate is { } d && d != lastDate)
            {
                sb.Append($"""<tr class="find-recent-date-row"><td colspan="5"><time datetime="{d:yyyy-MM-dd}">{FormatDate(d)}</time></td></tr>""").Append('\n');
                lastDate = d;
            }
            sb.Append(RecentlyInspectedRowHtml(e, n)).Append('\n');
        }
        return sb.ToString();
    }

    // Category -> line-icon SVG path, matched by keyword against the raw Pixibranche value. Falls back to
    // a generic storefront icon for categories outside this list (e.g. "Transport", "Emballager m.v.").
    private static readonly (Regex Pattern, string Path)[] CategoryIcons =
    [
        (new Regex("restaurant|pizzeria|kantine", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "M6 3v7a2 2 0 0 0 4 0V3M8 10v11M15 3c-1.5 1.5-1.5 4 0 5.5V21"),
        (new Regex("dagligvarer|grocery", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "M3 6h2l2.5 9h10L20 8H6.5M9 20a1 1 0 1 0 2 0 1 1 0 1 0-2 0m6 0a1 1 0 1 0 2 0 1 1 0 1 0-2 0"),
        (new Regex("hospital|institution", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "M4 21V9l8-5 8 5v12M4 21h16M9 21v-5h6v5M9 12h6"),
        (new Regex("bager|bakel|bakeri", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "M4 15c0-3.5 3.5-6 8-6s8 2.5 8 6v3H4zM9 9.5V18M15 9.5V18"),
        (new Regex("delikatesse|smørrebrød", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "M4 17h16M5 17c0-3 3-5.5 7-5.5s7 2.5 7 5.5M8 11.5l1-3M15 11.5l1-3"),
        (new Regex("slagter|butcher", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "M3 5h11v8H3zM14 8h7"),
        (new Regex("fisk|fish|vildt", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "M21 12c-2-3-5-4.5-8.5-4.5S6 9 4 12c2 3 5 4.5 8.5 4.5S19 15 21 12zM4 12 1 8v8zM17 11.4h.01")
    ];

    private const string DefaultCategoryIconPath = "M3 21h18M5 21V8l7-5 7 5v13M9.5 21v-6h5v6";

    private static string CategoryIconHtml(string? category)
    {
        var path = DefaultCategoryIconPath;
        if (!string.IsNullOrEmpty(category))
        {
            var match = CategoryIcons.FirstOrDefault(c => c.Pattern.IsMatch(category));
            if (match.Path is not null) path = match.Path;
        }

        return $"""
            <span class="find-recent-icon">
              <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="{path}"></path></svg>
            </span>
            """;
    }

    private static string RecentlyInspectedRowHtml(Establishment e, int n)
    {
        var addressLine = string.Join(", ", new[] { e.Address, e.City }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var score = e.LatestScore;
        var badge = score is not null
            ? $"""<img src="{ScoreImagePath(score.Value, 32)}" alt="Inspection score {score}" width="32" height="32" style="border-radius:4px;object-fit:contain" />"""
            : "";
        var scoreLabel = score is not null ? $"Score {score}/4" : "No score yet";
        var path = FindUrlBuilder.DetailPath(e);

        return $"""
            <tr class="find-recent-row">
              <td class="n">{n}</td>
              <td class="biz">
                <div class="find-recent-biz">
                  {CategoryIconHtml(e.Pixibranche)}
                  <span>
                    <a href="{path}">{E(e.Name)}</a>
                    <div class="addr">{E(addressLine)}</div>
                  </span>
                </div>
              </td>
              <td class="cat-col" data-label="Category">{E(e.Pixibranche)}</td>
              <td class="score-col" data-label="Score"><span class="find-recent-score">{badge}<span>{E(scoreLabel)}</span></span></td>
              <td class="chev-col"><a href="{path}" aria-label="View {E(e.Name)}">›</a></td>
            </tr>
            """;
    }

    private static string FormatDate(DateOnly d) => d.ToString("d MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);

    // Score-chip color for the previous/new score badges on the changes page — mirrors ScoreFace's
    // score->face grouping, mapped onto the same three-tier color scale used across the site.
    private static readonly Dictionary<string, string> ScoreFaceColor = new()
    {
        ["Sm1"] = "var(--accent)",
        ["Sm3"] = "var(--amber)",
        ["Sm4"] = "var(--red)"
    };

    private static string ScoreChipHtml(int score, bool dim)
    {
        var color = ScoreFaceColor.GetValueOrDefault(ScoreFace.GetValueOrDefault(score, "Sm4"), "var(--red)");
        var opacityStyle = dim ? "opacity:0.7;" : "";
        return $"""<span class="find-score-chip" style="background:{color};{opacityStyle}" role="img" aria-label="Score {score}">{score}</span>""";
    }

    private const string DirectionUpIconPath = "M22 7 13.5 15.5l-4-4L2 19M16 7h6v6";
    private const string DirectionDownIconPath = "M22 17 13.5 8.5l-4 4L2 5M16 17h6v-6";

    /// <summary>Icon + text pill used per row — never color alone, per spec accessibility requirement.
    /// Improved and downgraded use identical visual weight; only the color/icon/word differ.</summary>
    private static string DirectionBadgeHtml(bool improved)
    {
        var modifier = improved ? "up" : "down";
        var path = improved ? DirectionUpIconPath : DirectionDownIconPath;
        var label = improved ? "Improved" : "Downgraded";
        return $"""
            <span class="find-direction-badge find-direction-badge--{modifier}">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="{path}"></path></svg>
              {label}
            </span>
            """;
    }

    private static string DirectionIconHtml(bool improved) => $"""
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" style="vertical-align:-3px;margin-right:6px"><path d="{(improved ? DirectionUpIconPath : DirectionDownIconPath)}"></path></svg>
        """;

    /// <summary>/find/{area-slug}/changes — establishments in an area whose most recent recorded score
    /// transition falls within the trailing window (FindEndpoints.ChangesWindowDays). Every figure here
    /// comes from the caller's live repository query results for this request; nothing is hard-coded.
    /// Tone matters more on this page than any other /find page (spec §25): improved and downgraded
    /// entries get equal visual weight, "downgraded" is the only word used for a worse score, and there
    /// is no ranking or leaderboard framing anywhere.</summary>
    public static string ChangesPage(
        string displaySpelling, int page, int pageSize, ChangesSummary summary,
        bool noindex, IReadOnlyList<ScoreChangeRow> changes,
        IReadOnlyList<(string Category, string CategorySlug, int Count)> categoriesInArea)
    {
        var changesPath = FindUrlBuilder.ChangesPath(displaySpelling);
        var categoryNavHtml = CategoryNavHtml(displaySpelling, categoriesInArea);

        var jsonLdCrumbs = new List<(string Name, string Path)>
        {
            ("Find", "/find"),
            (displaySpelling, FindUrlBuilder.HubPath(displaySpelling)),
            ("Changes", changesPath)
        };

        var breadcrumbHtml = $"""
            <nav aria-label="breadcrumb" style="margin-bottom:16px;font-size:0.9rem">
              <a href="/find">Find</a> › <a href="{FindUrlBuilder.HubPath(displaySpelling)}">{E(displaySpelling)}</a> › Changes
            </nav>
            <h1>Recent smiley score changes in {E(displaySpelling)}</h1>
            <p class="section-sub">See the food businesses in {E(displaySpelling)} whose official inspection score recently changed.</p>
            <p class="section-sub">Updated from the latest available Fødevarestyrelsen data.</p>
            """;

        var exploreLinksHtml = $"""
            <div style="display:flex;flex-wrap:wrap;gap:8px;margin:16px 0">
              <a href="{FindUrlBuilder.RecentlyInspectedPath(displaySpelling)}" class="city-tag">Recently inspected in {E(displaySpelling)} →</a>
              <a href="{FindUrlBuilder.HubPath(displaySpelling)}" class="city-tag">View all food businesses in {E(displaySpelling)} →</a>
            </div>
            """;

        var ctaHtml = $"""
            <div class="find-business-cta">
              <h2>Own a food business?</h2>
              <p>Get notified automatically the moment your inspection score changes — instead of finding out from a customer.</p>
              <a href="/register.html" class="btn-primary">Learn about Smilr Business →</a>
            </div>
            """;

        string body;
        string? prevPath = null, nextPath = null;
        if (changes.Count == 0)
        {
            // Spec §14: a quiet city is a normal, honest state, not an error — no stats card (there's
            // nothing to summarize), a plain explanatory block instead of an empty table.
            body = $"""
                {breadcrumbHtml}
                <div class="find-empty-state">
                  <span class="find-recent-icon">
                    <svg width="19" height="19" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 8v4l3 2M3 12a9 9 0 1 0 9-9 9 9 0 0 0-9 9z"></path></svg>
                  </span>
                  <div>
                    <p>No smiley score changes have been recorded recently for food businesses in {E(displaySpelling)}.</p>
                    <p class="section-sub">Smilr checks {E(displaySpelling)} on every sync. Businesses appear here when an inspection produces a different score than the one before it — not every inspection does.</p>
                  </div>
                </div>
                {exploreLinksHtml}
                {categoryNavHtml}
                {ctaHtml}
                """;
        }
        else
        {
            var hasMore = (long)page * pageSize < summary.TotalChanges;
            var pagerHtml = BuildPager($"{changesPath}?", page, hasMore);
            (prevPath, nextPath) = PagerLinks($"{changesPath}?", page, hasMore);
            var mostRecentText = summary.MostRecentChangeDate is { } d ? FormatDate(d) : null;
            var introText = mostRecentText is null
                ? $"Smilr tracks changes to official food inspection scores in {E(displaySpelling)}."
                : $"""Smilr tracks changes to official food inspection scores in {E(displaySpelling)}. This page lists food businesses whose smiley score recently changed, most recent first, together with the previous score, the new score, and a link to their full inspection history. The most recent change in this list was recorded on <strong>{mostRecentText}</strong>. Only score changes recorded in the last {FindEndpoints.ChangesWindowDays} days are shown, one entry per business.""";

            body = $"""
                {breadcrumbHtml}

                <div class="find-stats-card">
                  <div class="find-stats-label">{E(displaySpelling)}</div>
                  <div class="find-stats-grid">
                    <div class="find-stat">
                      <div class="find-stat-value">{summary.TotalChanges}</div>
                      <div class="find-stat-label">score change{(summary.TotalChanges == 1 ? "" : "s")} in the latest sync</div>
                    </div>
                    <div class="find-stat">
                      <div class="find-stat-value find-stat-value--up">{DirectionIconHtml(true)}{summary.ImprovedCount}</div>
                      <div class="find-stat-label">improved</div>
                    </div>
                    <div class="find-stat">
                      <div class="find-stat-value find-stat-value--down">{DirectionIconHtml(false)}{summary.DowngradedCount}</div>
                      <div class="find-stat-label">downgraded</div>
                    </div>
                    <div class="find-stat">
                      <div class="find-stat-value">{mostRecentText ?? "—"}</div>
                      <div class="find-stat-label">most recent change</div>
                    </div>
                  </div>
                </div>

                <p class="section-sub">{introText}</p>

                <div class="find-recent-table-wrap">
                  <table class="find-recent-table">
                    <thead>
                      <tr><th class="n">#</th><th>Business</th><th class="cat-col">Category</th><th>Score change</th><th class="chev-col"></th></tr>
                    </thead>
                    <tbody>
                    {ChangesRowsHtml(changes)}
                    </tbody>
                  </table>
                </div>
                <p class="section-sub" style="margin-top:8px">Score 1 is the highest inspection result, 4 the lowest.</p>
                {pagerHtml}
                {exploreLinksHtml}
                {categoryNavHtml}
                {ctaHtml}
                """;
        }

        return Layout(
            title: $"Recent Smiley Score Changes in {displaySpelling} — SmilrHQ",
            description: $"See which food businesses in {displaySpelling} recently had their official smiley score change — upgraded or downgraded — based on the latest Fødevarestyrelsen inspection data.",
            canonicalPath: changesPath,
            bodyHtml: body,
            noindex: noindex,
            extraHeadHtml: BreadcrumbJsonLd(jsonLdCrumbs),
            prevPath: prevPath,
            nextPath: nextPath);
    }

    private static string ChangesRowsHtml(IReadOnlyList<ScoreChangeRow> changes)
    {
        var sb = new StringBuilder();
        var n = 0;
        foreach (var row in changes)
        {
            n++;
            sb.Append(ChangeRowHtml(row, n)).Append('\n');
        }
        return sb.ToString();
    }

    private static string ChangeRowHtml(ScoreChangeRow row, int n)
    {
        var e = row.Establishment;
        var addressLine = string.Join(", ", new[] { e.Address, e.City }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var path = FindUrlBuilder.DetailPath(e);

        return $"""
            <tr class="find-recent-row">
              <td class="n">{n}</td>
              <td class="biz">
                <div class="find-recent-biz">
                  {CategoryIconHtml(e.Pixibranche)}
                  <span>
                    <a href="{path}">{E(e.Name)}</a>
                    <div class="addr">{E(addressLine)}</div>
                  </span>
                </div>
              </td>
              <td class="cat-col" data-label="Category">{E(e.Pixibranche)}</td>
              <td class="chg-col" data-label="Score change">
                <span class="find-score-change">
                  {ScoreChipHtml(row.PreviousScore, dim: true)}
                  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="var(--text-muted)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"></path></svg>
                  {ScoreChipHtml(row.NewScore, dim: false)}
                </span>
              </td>
              <td class="chev-col"><a href="{path}" aria-label="View {E(e.Name)}">›</a></td>
            </tr>
            """;
    }

    // Score -> what that score level means, per Fødevarestyrelsen's smiley scheme. Danish sanction terms
    // (indskærpelse, påbud, forbud, bødeforlæg) are kept verbatim even in the English UI — they're legal
    // categories, not translatable descriptions — same convention already validated for
    // /find/{area}/recently-inspected. This describes what the SCORE LEVEL means in general, never a
    // specific claim about what a particular inspection found — the source XML only ever gives us a
    // score and a date, never free-text remarks, so nothing more specific can be said truthfully.
    private static readonly Dictionary<int, string> ScoreOutcome = new()
    {
        [1] = "No remarks",
        [2] = "Indskærpelse (enforcement notice)",
        [3] = "Påbud or forbud (order or ban)",
        [4] = "Bødeforlæg or politianmeldelse (fine or police report)"
    };

    public static string DetailPage(
        Establishment est,
        IReadOnlyList<Establishment> recentlyInspectedInCity,
        IReadOnlyList<ScoreChangeRow> recentChangesInCity,
        IReadOnlyList<Establishment> otherInCategory)
    {
        var badge = ScoreBadgeHtml(est.LatestScore, est.VirksomhedsType);
        var addressLine = string.Join(", ", new[] { est.Address, est.PostalCode, est.City }
            .Where(s => !string.IsNullOrWhiteSpace(s)));

        // Both the changed-row annotation below and the recentChangeBlurb further down derive from the
        // same ScoreChangeCalculator walk over this establishment's already-loaded history, so they can
        // never disagree with each other or with the changes page's own definition of "a change".
        var latestChange = ScoreChangeCalculator.LatestChange(est.Inspections);

        var historyRows = est.Inspections
            .OrderByDescending(i => i.InspectedOn)
            .Select(i =>
            {
                var directionHtml = latestChange is { } lc && i.InspectedOn == lc.ChangeDate
                    ? DirectionBadgeHtml(lc.NewScore < lc.PreviousScore)
                    : "";
                var outcome = ScoreOutcome.GetValueOrDefault(i.SmileyScore, "—");
                return $"""
                    <tr>
                      <td>{i.InspectedOn:dd/MM/yyyy}</td>
                      <td><span style="display:inline-flex;align-items:center;gap:8px">{ScoreChipHtml(i.SmileyScore, dim: false)}{directionHtml}</span></td>
                      <td>{E(outcome)}</td>
                    </tr>
                    """;
            });
        var historyHtml = est.Inspections.Count == 0
            ? "<p>No inspection history recorded yet.</p>"
            : $"""
              <table class="find-history">
                <thead><tr><th>Date</th><th>Score</th><th>Outcome</th></tr></thead>
                <tbody>{string.Join("\n", historyRows)}</tbody>
              </table>
              """;

        var reportLink = string.IsNullOrWhiteSpace(est.ReportUrl)
            ? ""
            : $"""<p><a href="{E(est.ReportUrl)}" target="_blank" rel="noopener noreferrer">View official inspection report →</a></p>""";

        // Spec §23: only shown when this establishment would actually appear on the changes page right
        // now — most businesses most of the time have no recent change, unlike the unconditional
        // recently-inspected link above.
        var changesWindowStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-FindEndpoints.ChangesWindowDays));
        var recentChangeBlurb = !string.IsNullOrWhiteSpace(est.City) && latestChange is { } lcb && lcb.ChangeDate >= changesWindowStart
            ? $"""
              <p class="section-sub">This establishment's score recently changed — <a href="{FindUrlBuilder.ChangesPath(est.City!)}">see all recent changes in {E(est.City)} →</a></p>
              """
            : "";

        var hasCategory = !string.IsNullOrWhiteSpace(est.City) && !string.IsNullOrWhiteSpace(est.Pixibranche)
            && !PixibrancheCategories.IsPlaceholder(est.Pixibranche);

        var cityCrumb = string.IsNullOrWhiteSpace(est.City)
            ? ""
            : $"""<a href="{FindUrlBuilder.HubPath(est.City)}">{E(est.City)}</a> › """;
        var categoryCrumb = hasCategory
            ? $"""<a href="{FindUrlBuilder.CategoryHubPath(est.City!, est.Pixibranche!)}">{E(est.Pixibranche)}</a> › """
            : "";

        // Same Area/Category presence rules as the visible breadcrumb above, so the two never disagree.
        var jsonLdCrumbs = new List<(string Name, string Path)> { ("Find", "/find") };
        if (!string.IsNullOrWhiteSpace(est.City))
            jsonLdCrumbs.Add((est.City, FindUrlBuilder.HubPath(est.City)));
        if (hasCategory)
            jsonLdCrumbs.Add((est.Pixibranche!, FindUrlBuilder.CategoryHubPath(est.City!, est.Pixibranche!)));
        jsonLdCrumbs.Add((est.Name, FindUrlBuilder.DetailPath(est)));
        var extraHeadHtml = BreadcrumbJsonLd(jsonLdCrumbs) + FoodEstablishmentJsonLd(est, addressLine) + FaqJsonLd();

        // ── Quick facts + Location ──────────────────────────────────────────────────────────────
        var quickFacts = new List<(string Value, string Label)>
        {
            (est.LatestScore?.ToString() ?? "—", "current smiley"),
            (est.Inspections.Count == 0
                ? "0"
                : $"{est.Inspections.Count} since {est.Inspections.Min(i => i.InspectedOn).Year}", "inspections")
        };
        if (latestChange is { } lc2)
            quickFacts.Add(($"{lc2.PreviousScore} → {lc2.NewScore} in {lc2.ChangeDate.Year}", "last change"));

        var quickFactsHtml = $"""
            <div class="find-stats-card">
              <div class="find-stats-grid">
                {string.Join("\n", quickFacts.Select(f => $"""
                <div class="find-stat">
                  <div class="find-stat-value">{E(f.Value)}</div>
                  <div class="find-stat-label">{E(f.Label)}</div>
                </div>
                """))}
              </div>
            </div>
            """;

        var mapHref = est.GeoLat is { } lat && est.GeoLng is { } lng
            ? $"https://www.google.com/maps/search/?api=1&query={lat.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)},{lng.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)}"
            : $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString((addressLine.Length > 0 ? addressLine + ", " : "") + "Denmark")}";
        var locationHtml = $"""
            <div class="find-stats-card">
              <div class="find-stats-label">Location</div>
              <p class="section-sub" style="margin:0 0 10px">{E(addressLine)}</p>
              <a href="{mapHref}" target="_blank" rel="noopener noreferrer">Open in Google Maps →</a>
            </div>
            """;

        // ── Business identity ───────────────────────────────────────────────────────────────────
        var identityRows = new List<(string Label, string Value)> { ("Business name", est.Name) };
        if (!string.IsNullOrWhiteSpace(addressLine)) identityRows.Add(("Address", addressLine));
        if (!string.IsNullOrWhiteSpace(est.City)) identityRows.Add(("City", est.City));
        if (hasCategory) identityRows.Add(("Category", est.Pixibranche!));
        if (!string.IsNullOrWhiteSpace(est.CvrNumber)) identityRows.Add(("CVR", est.CvrNumber));
        if (!string.IsNullOrWhiteSpace(est.PNumber)) identityRows.Add(("P-number", est.PNumber));
        if (est.Inspections.Count > 0)
            identityRows.Add(("First published inspection", FormatDate(est.Inspections.Min(i => i.InspectedOn))));

        var identityHtml = $"""
            <h2 class="section-title" style="margin-top:40px">Business identity</h2>
            <dl class="find-stats-card" style="margin:0">
              {string.Join("\n", identityRows.Select(r => $"""<div class="find-identity-row"><dt>{E(r.Label)}</dt><dd>{E(r.Value)}</dd></div>"""))}
            </dl>
            """;

        // ── Related discovery ───────────────────────────────────────────────────────────────────
        var discoveryCards = "";
        if (!string.IsNullOrWhiteSpace(est.City))
        {
            var recentCardItems = recentlyInspectedInCity
                .Select(e => (e.Name, Meta: e.LatestScoreDate is { } d ? FormatDate(d) : "", Href: FindUrlBuilder.DetailPath(e)))
                .ToList();
            var changesCardItems = recentChangesInCity
                .Select(r => (r.Establishment.Name, Meta: $"{r.PreviousScore} → {r.NewScore}", Href: FindUrlBuilder.DetailPath(r.Establishment)))
                .ToList();
            var categoryCardItems = hasCategory
                ? otherInCategory.Select(e => (e.Name, Meta: e.LatestScore is { } s ? $"Score {s}" : "", Href: FindUrlBuilder.DetailPath(e))).ToList()
                : [];

            var cards = new List<string>();
            if (recentCardItems.Count > 0)
                cards.Add(DiscoveryCardHtml($"Recently inspected in {est.City}", recentCardItems,
                    FindUrlBuilder.RecentlyInspectedPath(est.City), $"Recently inspected in {est.City}"));
            if (changesCardItems.Count > 0)
                cards.Add(DiscoveryCardHtml($"Recent changes in {est.City}", changesCardItems,
                    FindUrlBuilder.ChangesPath(est.City), $"Recent changes in {est.City}"));
            if (categoryCardItems.Count > 0)
                cards.Add(DiscoveryCardHtml(est.Pixibranche!, categoryCardItems,
                    FindUrlBuilder.CategoryHubPath(est.City, est.Pixibranche!), "Browse the category"));

            if (cards.Count > 0)
                discoveryCards = $"""
                    <h2 class="section-title" style="margin-top:40px">Related discovery</h2>
                    <div class="feature-grid">
                    {string.Join("\n", cards)}
                    </div>
                    """;
        }

        // ── FAQ ──────────────────────────────────────────────────────────────────────────────────
        var faqHtml = $"""
            <h2 class="section-title" style="margin-top:40px">About the smiley scheme</h2>
            <div class="find-stats-card" style="display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:18px 28px">
              <div>
                <div style="font-weight:700;margin-bottom:4px">What do the smiley scores mean?</div>
                <p class="section-sub" style="margin:0">Score 1 means no remarks. Score 2 is an indskærpelse (enforcement notice). Score 3 is a påbud or forbud (order or ban). Score 4 is a bødeforlæg or politianmeldelse (fine or police report). 1 is the best result, 4 the worst.</p>
              </div>
              <div>
                <div style="font-weight:700;margin-bottom:4px">How often is this page updated?</div>
                <p class="section-sub" style="margin:0">Updated from the latest available Fødevarestyrelsen data, synced daily.</p>
              </div>
            </div>
            """;

        var body = $"""
            <nav aria-label="breadcrumb" style="margin-bottom:16px;font-size:0.9rem">
              <a href="/find">Find</a> › {cityCrumb}{categoryCrumb}{E(est.Name)}
            </nav>
            <div class="find-detail">
              <div class="find-detail-badge">{badge}</div>
              <div class="find-detail-info">
                <h1>{E(est.Name)}</h1>
                <p class="section-sub">{E(addressLine)}</p>
                <p>CVR: {E(est.CvrNumber)} &middot; Navnelbnr: {est.Navnelbnr}</p>
                {reportLink}
                <a href="/register.html?claim_cvr={Uri.EscapeDataString(est.CvrNumber ?? "")}" class="btn-primary">Claim this listing →</a>
              </div>
            </div>
            {quickFactsHtml}
            {locationHtml}
            {recentChangeBlurb}
            <h2 class="section-title" style="margin-top:40px">Inspection history</h2>
            {historyHtml}
            {identityHtml}
            {discoveryCards}
            {faqHtml}
            """;

        var description = est.LatestScore is not null
            ? $"{est.Name} — current Smilr food inspection score {est.LatestScore}/4. Official data from Fødevarestyrelsen."
            : $"{est.Name} — Danish food inspection (smiley) score and history. Official data from Fødevarestyrelsen.";

        return Layout(
            title: $"{est.Name} — Smilr score — SmilrHQ",
            description: description,
            canonicalPath: FindUrlBuilder.DetailPath(est),
            bodyHtml: body,
            extraHeadHtml: extraHeadHtml);
    }

    /// <summary>One "Related discovery" card — a title, up to a handful of name+meta links, and a
    /// "View all" link to the full page. Returns "" if there's nothing to show (caller already checks
    /// this, but kept defensive since an empty card would otherwise render as a bare title).</summary>
    private static string DiscoveryCardHtml(
        string title, IReadOnlyList<(string Name, string Meta, string Href)> items, string viewAllHref, string viewAllLabel)
    {
        if (items.Count == 0) return "";
        var itemsHtml = string.Join("\n", items.Select(i => $"""
            <a href="{i.Href}" style="display:grid;grid-template-columns:minmax(0,1fr) auto;gap:12px;align-items:center;padding:8px 0;border-top:1px solid var(--border);font-size:0.85rem">
              <span style="font-weight:600;color:var(--text)">{E(i.Name)}</span>
              <span style="font-size:0.78rem;color:var(--text-muted)">{E(i.Meta)}</span>
            </a>
            """));
        return $"""
            <div class="feature-card">
              <h3>{E(title)}</h3>
              {itemsHtml}
              <a href="{viewAllHref}" style="font-size:0.85rem;font-weight:600;margin-top:10px;display:inline-block">{E(viewAllLabel)} →</a>
            </div>
            """;
    }

    /// <summary>schema.org FoodEstablishment — deliberately not "Restaurant" (the mockup's choice),
    /// since Pixibranche spans bakeries, grocery stores, butchers etc. — FoodEstablishment is the
    /// accurate general type for all of them. geo/identifier are omitted, not emitted as null, when the
    /// underlying field isn't present.</summary>
    private static string FoodEstablishmentJsonLd(Establishment est, string addressLine)
    {
        var address = new Dictionary<string, object?> { ["@type"] = "PostalAddress" };
        if (!string.IsNullOrWhiteSpace(est.Address)) address["streetAddress"] = est.Address;
        if (!string.IsNullOrWhiteSpace(est.PostalCode)) address["postalCode"] = est.PostalCode;
        if (!string.IsNullOrWhiteSpace(est.City)) address["addressLocality"] = est.City;
        address["addressCountry"] = "DK";

        var schema = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "FoodEstablishment",
            ["name"] = est.Name,
            ["url"] = SiteOrigin + FindUrlBuilder.DetailPath(est),
            ["address"] = address
        };

        if (est.GeoLat is { } lat && est.GeoLng is { } lng)
            schema["geo"] = new Dictionary<string, object?> { ["@type"] = "GeoCoordinates", ["latitude"] = lat, ["longitude"] = lng };

        var identifiers = new List<Dictionary<string, object?>>();
        if (!string.IsNullOrWhiteSpace(est.CvrNumber))
            identifiers.Add(new() { ["@type"] = "PropertyValue", ["name"] = "CVR", ["value"] = est.CvrNumber });
        if (!string.IsNullOrWhiteSpace(est.PNumber))
            identifiers.Add(new() { ["@type"] = "PropertyValue", ["name"] = "P-number", ["value"] = est.PNumber });
        if (identifiers.Count > 0) schema["identifier"] = identifiers;

        return $"""<script type="application/ld+json">{JsonSerializer.Serialize(schema)}</script>""";
    }

    /// <summary>FAQPage JSON-LD for the two generic, business-agnostic FAQ entries — accurate here since
    /// the content is genuinely generic Q&amp;A about the scheme, not a dressed-up review/rating.</summary>
    private static string FaqJsonLd()
    {
        var schema = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "FAQPage",
            ["mainEntity"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["@type"] = "Question",
                    ["name"] = "What do the smiley scores mean?",
                    ["acceptedAnswer"] = new Dictionary<string, object?>
                    {
                        ["@type"] = "Answer",
                        ["text"] = "Score 1 means no remarks. Score 2 is an indskærpelse (enforcement notice). Score 3 is a påbud or forbud (order or ban). Score 4 is a bødeforlæg or politianmeldelse (fine or police report). 1 is the best result, 4 the worst."
                    }
                },
                new Dictionary<string, object?>
                {
                    ["@type"] = "Question",
                    ["name"] = "How often is this page updated?",
                    ["acceptedAnswer"] = new Dictionary<string, object?>
                    {
                        ["@type"] = "Answer",
                        ["text"] = "This page is updated from the latest available Fødevarestyrelsen data, synced daily."
                    }
                }
            }
        };
        return $"""<script type="application/ld+json">{JsonSerializer.Serialize(schema)}</script>""";
    }

    /// <summary>Renders a schema.org BreadcrumbList JSON-LD block for search-engine rich snippets — kept
    /// in sync with whatever breadcrumb trail the caller builds for the visible &lt;nav&gt; above the page
    /// body, so the two never disagree.</summary>
    private static string BreadcrumbJsonLd(IReadOnlyList<(string Name, string Path)> crumbs)
    {
        var itemListElement = crumbs.Select((c, i) => new Dictionary<string, object?>
        {
            ["@type"] = "ListItem",
            ["position"] = i + 1,
            ["name"] = c.Name,
            ["item"] = SiteOrigin + c.Path
        });

        var schema = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = itemListElement
        };

        // Default JsonSerializer escaping is HTML-safe (escapes <, >, &), so this is safe to embed
        // directly inside a <script> tag even if a business name contains those characters.
        return $"""<script type="application/ld+json">{JsonSerializer.Serialize(schema)}</script>""";
    }

    public static string NotFoundPage(string message)
    {
        var body = $"""
            <h1>Not found</h1>
            <p>{E(message)}</p>
            <p><a href="/find">← Back to search</a></p>
            """;
        return Layout("Not found — SmilrHQ", "The requested page could not be found.", "/find", body);
    }

    public static string SitemapXml(
        IReadOnlyList<SitemapEntry> entries,
        IReadOnlyList<(string City, string Category, int Count)> categoryCounts,
        IReadOnlyList<(string City, int Count)> changeCounts,
        int categorySlugThreshold)
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8"?>""").Append('\n');
        sb.Append("""<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">""").Append('\n');

        foreach (var e in entries)
        {
            var path = FindUrlBuilder.DetailPath(e.Name, e.City, e.Navnelbnr);
            sb.Append("  <url><loc>")
              .Append(SiteOrigin).Append(path)
              .Append("</loc><lastmod>")
              .Append(e.UpdatedAt.ToString("yyyy-MM-dd"))
              .Append("</lastmod></url>\n");
        }

        // One hub-page entry per area, derived from the same projection — no extra repository call needed.
        var hubGroups = entries
            .Where(e => !string.IsNullOrWhiteSpace(e.City))
            .GroupBy(e => FindUrlBuilder.AreaSlug(e.City!))
            .ToList();

        var areaLastmod = new Dictionary<string, DateTime>();
        foreach (var group in hubGroups)
        {
            var displaySpelling = group.First().City!;
            var lastmod = group.Max(e => e.UpdatedAt);
            areaLastmod[group.Key] = lastmod;
            sb.Append("  <url><loc>")
              .Append(SiteOrigin).Append(FindUrlBuilder.HubPath(displaySpelling))
              .Append("</loc><lastmod>")
              .Append(lastmod.ToString("yyyy-MM-dd"))
              .Append("</lastmod></url>\n");
        }

        // One recently-inspected entry per area whose count of establishments with a recorded inspection
        // date meets the same indexing threshold used by FindEndpoints.RecentlyInspectedHandlerAsync —
        // keeps 1-2-establishment thin pages (rendered, but noindexed) out of the sitemap.
        foreach (var group in hubGroups)
        {
            var withInspectionCount = group.Count(e => e.HasInspectionDate);
            if (withInspectionCount < categorySlugThreshold) continue;

            var displaySpelling = group.First().City!;
            sb.Append("  <url><loc>")
              .Append(SiteOrigin).Append(FindUrlBuilder.RecentlyInspectedPath(displaySpelling))
              .Append("</loc><lastmod>")
              .Append(areaLastmod[group.Key].ToString("yyyy-MM-dd"))
              .Append("</lastmod></url>\n");
        }

        // One changes entry per area whose count of establishments with an in-window score transition
        // meets the same indexing threshold used by FindEndpoints.ChangesHandlerAsync — re-evaluated
        // fresh every time the sitemap is rebuilt (this whole method reruns on each IMemoryCache TTL
        // expiry), so a city can't get stuck with a stale indexability decision from a previous sync.
        var changeCountByAreaSlug = changeCounts
            .GroupBy(t => FindUrlBuilder.AreaSlug(t.City))
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Count));

        foreach (var group in hubGroups)
        {
            if (!changeCountByAreaSlug.TryGetValue(group.Key, out var changeCount) || changeCount < categorySlugThreshold)
                continue;

            var displaySpelling = group.First().City!;
            sb.Append("  <url><loc>")
              .Append(SiteOrigin).Append(FindUrlBuilder.ChangesPath(displaySpelling))
              .Append("</loc><lastmod>")
              .Append(areaLastmod[group.Key].ToString("yyyy-MM-dd"))
              .Append("</lastmod></url>\n");
        }

        // Area x category entries, only for combinations meeting the minimum-establishment-count indexing
        // threshold (see CategorySlugThreshold in FindEndpoints.cs). categoryCounts carries no per-entry
        // timestamp of its own, so this borrows the containing area's lastmod as an approximation — the
        // area-slug set here is always a subset of hubGroups' (both ultimately filter on CvrNumber/City
        // non-null; category adds a stricter Pixibranche filter on top), so the lookup below shouldn't
        // ever miss, but falls through safely if it somehow does.
        var categoryGroups = categoryCounts
            .Where(t => t.Count >= categorySlugThreshold)
            .GroupBy(t => FindUrlBuilder.AreaSlug(t.City));

        foreach (var areaGroup in categoryGroups)
        {
            if (!areaLastmod.TryGetValue(areaGroup.Key, out var lastmod)) continue;
            var displayCity = areaGroup.First().City;
            foreach (var t in areaGroup)
            {
                sb.Append("  <url><loc>")
                  .Append(SiteOrigin).Append(FindUrlBuilder.CategoryHubPath(displayCity, t.Category))
                  .Append("</loc><lastmod>")
                  .Append(lastmod.ToString("yyyy-MM-dd"))
                  .Append("</lastmod></url>\n");
            }
        }

        sb.Append("</urlset>");
        return sb.ToString();
    }

    private static string ResultRowHtml(Establishment e)
    {
        var addressLine = string.Join(", ", new[] { e.Address, e.City }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var scoreLabel = e.LatestScore is not null ? $"Score {e.LatestScore}/4" : "No score yet";
        return $"""
            <a class="find-result-row" href="{FindUrlBuilder.DetailPath(e)}">
              <span class="find-result-name">{E(e.Name)}</span>
              <span class="find-result-address">{E(addressLine)}</span>
              <span class="find-result-score">{E(scoreLabel)}</span>
            </a>
            """;
    }

    private static string SearchFormHtml(string? query) => $"""
        <form action="/find/search" method="get" class="find-search-form">
          <input type="text" name="q" value="{E(query)}" placeholder="CVR number or business name" required />
          <button type="submit" class="btn-primary">Search</button>
        </form>
        """;

    /// <summary>
    /// Renders a Previous/Next pager. <paramref name="basePathWithTrailingSeparator"/> must already end in
    /// "?" or "&amp;" so "page=N" can be appended directly (e.g. "/find/search?q=x&amp;" or "/find/kobenhavn/?").
    /// </summary>
    private static string BuildPager(string basePathWithTrailingSeparator, int page, bool hasMore)
    {
        var (prevUrl, nextUrl) = PagerLinks(basePathWithTrailingSeparator, page, hasMore);
        var prev = prevUrl is not null ? $"""<a href="{prevUrl}">← Previous</a>""" : "";
        var next = nextUrl is not null ? $"""<a href="{nextUrl}">Next →</a>""" : "";
        if (string.IsNullOrEmpty(prev) && string.IsNullOrEmpty(next)) return "";
        return $"""<div class="find-pager">{prev}{next}</div>""";
    }

    /// <summary>Shared prev/next URL computation for paginated /find pages — used both for the visible
    /// pager links (via BuildPager) and for the rel="prev"/rel="next" &lt;link&gt; tags in Layout(), so a
    /// page's canonical tag (always page 1) doesn't leave crawlers with no path to the other pages.</summary>
    private static (string? Prev, string? Next) PagerLinks(string basePathWithTrailingSeparator, int page, bool hasMore) =>
    (
        page > 1 ? $"{basePathWithTrailingSeparator}page={page - 1}" : null,
        hasMore ? $"{basePathWithTrailingSeparator}page={page + 1}" : null
    );

    private static string ScoreBadgeHtml(int? score, string? virksomhedsType)
    {
        string imgSrc, alt;
        if (score is null)
        {
            imgSrc = "/Smiley_figurer/150/kontrolpaaVej.png";
            alt    = "Kontrol på vej";
        }
        else if (virksomhedsType != "Detail")
        {
            imgSrc = "/Smiley_figurer/150/engroIcon.png";
            alt    = "Engros kontrol";
        }
        else
        {
            imgSrc = ScoreImagePath(score.Value, 150);
            alt    = "Smilr";
        }
        return $"""<img src="{imgSrc}" alt="{E(alt)}" width="150" height="150" style="object-fit:contain" />""";
    }

    private static string Layout(
        string title, string description, string canonicalPath, string bodyHtml,
        bool noindex = false, string extraHeadHtml = "", string? prevPath = null, string? nextPath = null)
    {
        var canonicalUrl = SiteOrigin + canonicalPath;
        var robotsTag = noindex ? """<meta name="robots" content="noindex,follow" />""" + "\n  " : "";
        var prevTag = prevPath is not null ? $"""<link rel="prev" href="{SiteOrigin + prevPath}" />""" + "\n  " : "";
        var nextTag = nextPath is not null ? $"""<link rel="next" href="{SiteOrigin + nextPath}" />""" + "\n  " : "";
        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="UTF-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0" />
              <meta name="description" content="{E(description)}" />
              {robotsTag}<link rel="canonical" href="{canonicalUrl}" />
              {prevTag}{nextTag}<meta property="og:type" content="website" />
              <meta property="og:title" content="{E(title)}" />
              <meta property="og:description" content="{E(description)}" />
              <meta property="og:url" content="{canonicalUrl}" />
              <meta property="og:image" content="{SiteOrigin}/images/hero.jpg" />
              <meta name="twitter:card" content="summary_large_image" />
              <meta name="twitter:title" content="{E(title)}" />
              <meta name="twitter:description" content="{E(description)}" />
              <meta name="twitter:image" content="{SiteOrigin}/images/hero.jpg" />
              <title>{E(title)}</title>
              <link rel="stylesheet" href="/style.css" />
              {extraHeadHtml}
            </head>
            <body>
            <header class="hero" style="padding-bottom:32px">
              <nav class="nav">
                <a href="/" class="nav-logo" style="text-decoration:none">Smilr<span class="accent">HQ</span></a>
                <div class="nav-links">
                  <a href="/find" class="nav-link">Find a restaurant</a>
                  <a href="/login.html" class="nav-link">Log in</a>
                </div>
              </nav>
            </header>
            <main class="container" style="padding:32px 20px 60px">
            {bodyHtml}
            </main>
            <!-- Reserved for future ad-network integration; intentionally unwired. -->
            <div class="ad-slot" style="display:none"></div>
            <footer class="footer">
              <div class="container">
                <p>Data provided by <a href="https://www.foedevarestyrelsen.dk" target="_blank" rel="noopener">Fødevarestyrelsen</a> under Danish open data licence.</p>
                <p>Questions? <a href="mailto:info@smilrhq.dk">info@smilrhq.dk</a></p>
                <p>
                  <a href="/about.html">About us</a> &middot; <a href="/contact.html">Contact us</a> &middot;
                  <a href="/scores.html">Scores explained</a> &middot; <a href="/terms.html">Terms</a> &middot;
                  <a href="/privacy.html">Privacy</a>
                </p>
              </div>
            </footer>
            </body>
            </html>
            """;
    }

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
}
