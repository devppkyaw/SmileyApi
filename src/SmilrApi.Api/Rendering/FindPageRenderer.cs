using System.Net;
using System.Text;
using SmilrApi.Core.Interfaces;
using SmilrApi.Core.Models;

namespace SmilrApi.Api.Rendering;

/// <summary>
/// Hand-built HTML rendering for the /find directory pages. No templating engine is used here —
/// consistent with the rest of this codebase (CLAUDE.md: "No frontend framework... plain HTML/CSS
/// in wwwroot"). These pages must be crawler-reliable without JS execution, so unlike wwwroot's
/// client-fetch pages, everything here is rendered server-side into the initial HTML response.
/// </summary>
public static class FindPageRenderer
{
    private const string SiteOrigin = "https://smilrhq.dk";

    // Mirrors the SCORES map in wwwroot/widget.js so /find pages show the same badge as the widget.
    private static readonly Dictionary<int, string> ScoreImages = new()
    {
        [1] = "Sm1bg",
        [2] = "Sm3bg",
        [3] = "Sm3bg",
        [4] = "Sm4bg"
    };

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

        var pagerHtml = BuildPager(query, page, hasMore: results.Count >= limit);

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
            canonicalPath: $"/find/search?q={Uri.EscapeDataString(query)}",
            bodyHtml: body);
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
            canonicalPath: $"/find/{Uri.EscapeDataString(cvr)}",
            bodyHtml: body);
    }

    public static string DetailPage(Establishment est)
    {
        var badge = ScoreBadgeHtml(est.LatestScore, est.VirksomhedsType);
        var addressLine = string.Join(", ", new[] { est.Address, est.PostalCode, est.City }
            .Where(s => !string.IsNullOrWhiteSpace(s)));

        var historyRows = est.Inspections
            .OrderByDescending(i => i.InspectedOn)
            .Select(i => $"<tr><td>{i.InspectedOn:dd/MM/yyyy}</td><td>{i.SmileyScore}</td></tr>");
        var historyHtml = est.Inspections.Count == 0
            ? "<p>No inspection history recorded yet.</p>"
            : $"""
              <table class="find-history">
                <thead><tr><th>Date</th><th>Score</th></tr></thead>
                <tbody>{string.Join("\n", historyRows)}</tbody>
              </table>
              """;

        var reportLink = string.IsNullOrWhiteSpace(est.ReportUrl)
            ? ""
            : $"""<p><a href="{E(est.ReportUrl)}" target="_blank" rel="noopener noreferrer">View official inspection report →</a></p>""";

        var body = $"""
            <nav aria-label="breadcrumb" style="margin-bottom:16px;font-size:0.9rem">
              <a href="/find">Find</a> › <a href="/find/{E(est.CvrNumber)}">CVR {E(est.CvrNumber)}</a> › {E(est.Name)}
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
            <h2 class="section-title" style="margin-top:40px">Inspection history</h2>
            {historyHtml}
            """;

        var description = est.LatestScore is not null
            ? $"{est.Name} — current Smilr food inspection score {est.LatestScore}/4. Official data from Fødevarestyrelsen."
            : $"{est.Name} — Danish food inspection (smiley) score and history. Official data from Fødevarestyrelsen.";

        return Layout(
            title: $"{est.Name} — Smilr score — SmilrHQ",
            description: description,
            canonicalPath: $"/find/{Uri.EscapeDataString(est.CvrNumber ?? "")}/{est.Navnelbnr}",
            bodyHtml: body);
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

    public static string SitemapXml(IReadOnlyList<SitemapEntry> entries)
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8"?>""").Append('\n');
        sb.Append("""<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">""").Append('\n');
        foreach (var e in entries)
        {
            sb.Append("  <url><loc>")
              .Append(SiteOrigin).Append("/find/").Append(Uri.EscapeDataString(e.CvrNumber)).Append('/').Append(e.Navnelbnr)
              .Append("</loc><lastmod>")
              .Append(e.UpdatedAt.ToString("yyyy-MM-dd"))
              .Append("</lastmod></url>\n");
        }
        sb.Append("</urlset>");
        return sb.ToString();
    }

    private static string ResultRowHtml(Establishment e)
    {
        var addressLine = string.Join(", ", new[] { e.Address, e.City }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var scoreLabel = e.LatestScore is not null ? $"Score {e.LatestScore}/4" : "No score yet";
        return $"""
            <a class="find-result-row" href="/find/{Uri.EscapeDataString(e.CvrNumber!)}/{e.Navnelbnr}">
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

    private static string BuildPager(string query, int page, bool hasMore)
    {
        var q = Uri.EscapeDataString(query);
        var prev = page > 1 ? $"""<a href="/find/search?q={q}&page={page - 1}">← Previous</a>""" : "";
        var next = hasMore ? $"""<a href="/find/search?q={q}&page={page + 1}">Next →</a>""" : "";
        if (string.IsNullOrEmpty(prev) && string.IsNullOrEmpty(next)) return "";
        return $"""<div class="find-pager">{prev}{next}</div>""";
    }

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
            var img = ScoreImages.GetValueOrDefault(score.Value, "Sm4bg");
            imgSrc  = $"/Smiley_figurer/150/{img}.jpg";
            alt     = "Smilr";
        }
        return $"""<img src="{imgSrc}" alt="{E(alt)}" width="150" height="150" style="object-fit:contain" />""";
    }

    private static string Layout(string title, string description, string canonicalPath, string bodyHtml)
    {
        var canonicalUrl = SiteOrigin + canonicalPath;
        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="UTF-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0" />
              <meta name="description" content="{E(description)}" />
              <link rel="canonical" href="{canonicalUrl}" />
              <meta property="og:type" content="website" />
              <meta property="og:title" content="{E(title)}" />
              <meta property="og:description" content="{E(description)}" />
              <meta property="og:url" content="{canonicalUrl}" />
              <title>{E(title)}</title>
              <link rel="stylesheet" href="/style.css" />
            </head>
            <body>
            <header class="hero" style="padding-bottom:32px">
              <nav class="nav">
                <a href="/" class="nav-logo" style="text-decoration:none">Smilr<span class="accent">HQ</span></a>
                <div class="nav-links">
                  <a href="/find" class="nav-link">Find a restaurant</a>
                  <a href="/developers.html" class="nav-link">Developers</a>
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
