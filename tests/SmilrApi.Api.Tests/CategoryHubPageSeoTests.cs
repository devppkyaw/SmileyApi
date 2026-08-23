using System.Text.Json;
using System.Text.RegularExpressions;
using SmilrApi.Api.Rendering;
using SmilrApi.Core.Interfaces;
using SmilrApi.Core.Models;

namespace SmilrApi.Api.Tests;

/// <summary>
/// Mirrors AreaHubPageSeoTests.cs — CategoryHubPage is likewise a pure function (establishments/state in,
/// an HTML string out, no DB/HTTP), so its SEO-relevant output can be asserted on directly. Same
/// SiteOrigin-default caveat as AreaHubPageSeoTests applies here too.
/// </summary>
public class CategoryHubPageSeoTests
{
    private static IReadOnlyList<Establishment> SampleEstablishments(int count) =>
        Enumerable.Range(1, count)
            .Select(i => new Establishment
            {
                Navnelbnr = i,
                Name = $"Test Establishment {i}",
                Address = "Testvej 1",
                City = "Aarhus",
                Pixibranche = "Restauranter",
                LatestScore = 1
            })
            .ToList();

    private static string RenderPage(
        int page = 1, int pageSize = 20, int totalCount = 5, bool noindex = false,
        string? sort = null, bool hideUnscored = false, int establishmentCount = 5) =>
        FindPageRenderer.CategoryHubPage(
            displayCity: "Aarhus", displayCategory: "Restauranter", areaSlug: "aarhus", categorySlug: "restauranter",
            page: page, pageSize: pageSize, totalCount: totalCount, noindex: noindex,
            sort: sort, hideUnscored: hideUnscored,
            snapshot: new AreaScoreSnapshot(TotalScored: establishmentCount, TopScoreCount: establishmentCount),
            establishments: SampleEstablishments(establishmentCount));

    [Theory]
    [InlineData(null, false, 1)]
    [InlineData("score_desc", false, 1)]
    [InlineData(null, true, 1)]
    [InlineData("score_asc", true, 3)]
    public void Canonical_tag_always_points_at_the_bare_category_path_with_no_query_string(
        string? sort, bool hideUnscored, int page)
    {
        var html = RenderPage(page: page, sort: sort, hideUnscored: hideUnscored);

        Assert.Contains("""<link rel="canonical" href="https://smilrhq.dk/find/aarhus/restauranter" />""", html);
    }

    [Fact]
    public void Robots_noindex_meta_present_when_noindex_true()
    {
        var html = RenderPage(noindex: true);

        Assert.Contains("""<meta name="robots" content="noindex,follow" />""", html);
    }

    [Fact]
    public void Robots_noindex_meta_absent_when_noindex_false()
    {
        var html = RenderPage(noindex: false);

        Assert.DoesNotContain("noindex", html);
    }

    [Fact]
    public void Prev_and_next_link_tags_preserve_the_active_sort_and_filter()
    {
        // page 2 of 3 (pageSize=20, totalCount=41), with a sort and the filter both active.
        var html = RenderPage(page: 2, pageSize: 20, totalCount: 41, sort: "score_desc", hideUnscored: true);

        Assert.Contains(
            """<link rel="prev" href="https://smilrhq.dk/find/aarhus/restauranter?sort=score_desc&hide_unscored=1&page=1" />""",
            html);
        Assert.Contains(
            """<link rel="next" href="https://smilrhq.dk/find/aarhus/restauranter?sort=score_desc&hide_unscored=1&page=3" />""",
            html);
    }

    [Fact]
    public void Sort_bar_renders_with_the_active_sort_marked()
    {
        var html = RenderPage(sort: "score_asc");

        Assert.Contains("class=\"find-sort-bar\"", html);
        Assert.Contains("find-sort-link--active", html);
    }

    [Fact]
    public void Visible_breadcrumb_nav_contains_the_city_and_category_name()
    {
        var html = RenderPage();

        var navStart = html.IndexOf("""<nav aria-label="breadcrumb" """, StringComparison.Ordinal);
        Assert.True(navStart >= 0, "breadcrumb <nav> not found");
        var navEnd = html.IndexOf("</nav>", navStart, StringComparison.Ordinal);
        var navHtml = html[navStart..navEnd];

        Assert.Contains("Aarhus", navHtml);
        Assert.Contains("Restauranter", navHtml);
    }

    private static List<JsonElement> ExtractJsonLdBlocks(string html) =>
        Regex.Matches(html, """<script type="application/ld\+json">(.*?)</script>""", RegexOptions.Singleline)
            .Select(m => JsonDocument.Parse(m.Groups[1].Value).RootElement)
            .ToList();

    [Fact]
    public void Emits_a_BreadcrumbList_block_with_three_crumbs()
    {
        var blocks = ExtractJsonLdBlocks(RenderPage());

        Assert.Single(blocks);
        Assert.Equal("BreadcrumbList", blocks[0].GetProperty("@type").GetString());
        var items = blocks[0].GetProperty("itemListElement").EnumerateArray().ToList();
        Assert.Equal(3, items.Count);
        Assert.Equal("Find", items[0].GetProperty("name").GetString());
        Assert.Equal("Aarhus", items[1].GetProperty("name").GetString());
        Assert.Equal("Restauranter", items[2].GetProperty("name").GetString());
    }

    [Fact]
    public void Health_snapshot_card_renders_when_there_are_scored_establishments()
    {
        var html = RenderPage(establishmentCount: 4);

        Assert.Contains("Restauranter in Aarhus health snapshot", html);
        Assert.Contains("""<a href="/find/aarhus/changes" class="city-tag">""", html);
    }

    [Fact]
    public void Health_snapshot_card_omitted_when_no_scored_establishments()
    {
        var html = RenderPage(establishmentCount: 0);

        Assert.DoesNotContain("health snapshot", html);
    }
}
