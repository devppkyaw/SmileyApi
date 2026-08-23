using System.Text.Json;
using System.Text.RegularExpressions;
using SmilrApi.Api.Rendering;
using SmilrApi.Core.Interfaces;
using SmilrApi.Core.Models;

namespace SmilrApi.Api.Tests;

/// <summary>
/// AreaHubPage is a pure function (establishments/categories/sort/page state in, an HTML string out — no
/// DB or HTTP involved), so its SEO-relevant output can be asserted on directly with hand-built data.
/// Note: these rely on FindPageRenderer.SiteOrigin's default ("https://smilrhq.dk") — nothing in this
/// assembly currently calls Configure() to change it, but since it's a shared mutable static, a future
/// test that does call Configure() would need to reset it afterward or these tests could start failing
/// for an unrelated reason.
/// </summary>
public class AreaHubPageSeoTests
{
    private static readonly IReadOnlyList<(string Category, string CategorySlug, int Count)> NoCategories = [];

    private static IReadOnlyList<Establishment> SampleEstablishments(int count) =>
        Enumerable.Range(1, count)
            .Select(i => new Establishment
            {
                Navnelbnr = i,
                Name = $"Test Establishment {i}",
                Address = "Testvej 1",
                City = "Aarhus",
                LatestScore = 1
            })
            .ToList();

    private static string RenderPage(
        int page = 1, int pageSize = 20, int totalCount = 5, bool noindex = false,
        string? sort = null, bool hideUnscored = false, int establishmentCount = 5) =>
        FindPageRenderer.AreaHubPage(
            displaySpelling: "Aarhus",
            page: page, pageSize: pageSize, totalCount: totalCount, noindex: noindex,
            sort: sort, hideUnscored: hideUnscored,
            snapshot: new AreaScoreSnapshot(TotalScored: establishmentCount, TopScoreCount: establishmentCount),
            establishments: SampleEstablishments(establishmentCount),
            categoriesInArea: NoCategories);

    [Theory]
    [InlineData(null, false, 1)]
    [InlineData("score_desc", false, 1)]
    [InlineData(null, true, 1)]
    [InlineData("recent", true, 3)]
    public void Canonical_tag_always_points_at_the_bare_hub_path_with_no_query_string(
        string? sort, bool hideUnscored, int page)
    {
        var html = RenderPage(page: page, sort: sort, hideUnscored: hideUnscored);

        Assert.Contains("""<link rel="canonical" href="https://smilrhq.dk/find/aarhus/" />""", html);
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
    public void No_prev_or_next_link_tags_on_page_one_with_no_further_pages()
    {
        var html = RenderPage(page: 1, pageSize: 20, totalCount: 5);

        Assert.DoesNotContain("""rel="prev" """, html);
        Assert.DoesNotContain("""rel="next" """, html);
    }

    [Fact]
    public void Prev_and_next_link_tags_present_on_a_middle_page()
    {
        // page 2 of 3 (pageSize=20, totalCount=41 -> 3 pages) has both a previous and a next page.
        var html = RenderPage(page: 2, pageSize: 20, totalCount: 41);

        Assert.Contains("""<link rel="prev" href="https://smilrhq.dk/find/aarhus/?page=1" />""", html);
        Assert.Contains("""<link rel="next" href="https://smilrhq.dk/find/aarhus/?page=3" />""", html);
    }

    [Fact]
    public void Visible_breadcrumb_nav_contains_the_area_name()
    {
        var html = RenderPage();

        var navStart = html.IndexOf("""<nav aria-label="breadcrumb" """, StringComparison.Ordinal);
        Assert.True(navStart >= 0, "breadcrumb <nav> not found");
        var navEnd = html.IndexOf("</nav>", navStart, StringComparison.Ordinal);
        var navHtml = html[navStart..navEnd];

        Assert.Contains("Aarhus", navHtml);
    }

    private static List<JsonElement> ExtractJsonLdBlocks(string html) =>
        Regex.Matches(html, """<script type="application/ld\+json">(.*?)</script>""", RegexOptions.Singleline)
            .Select(m => JsonDocument.Parse(m.Groups[1].Value).RootElement)
            .ToList();

    [Fact]
    public void Emits_exactly_a_BreadcrumbList_and_an_ItemList_block()
    {
        var blocks = ExtractJsonLdBlocks(RenderPage());

        Assert.Equal(2, blocks.Count);
        Assert.Equal("BreadcrumbList", blocks[0].GetProperty("@type").GetString());
        Assert.Equal("ItemList", blocks[1].GetProperty("@type").GetString());
    }

    [Fact]
    public void BreadcrumbList_has_two_items_Find_then_the_area()
    {
        var breadcrumb = ExtractJsonLdBlocks(RenderPage())[0];
        var items = breadcrumb.GetProperty("itemListElement").EnumerateArray().ToList();

        Assert.Equal(2, items.Count);
        Assert.Equal("Find", items[0].GetProperty("name").GetString());
        Assert.Equal("https://smilrhq.dk/find", items[0].GetProperty("item").GetString());
        Assert.Equal("Aarhus", items[1].GetProperty("name").GetString());
        Assert.Equal("https://smilrhq.dk/find/aarhus/", items[1].GetProperty("item").GetString());
    }

    [Fact]
    public void ItemList_count_matches_the_establishments_shown_on_the_page()
    {
        var itemList = ExtractJsonLdBlocks(RenderPage(establishmentCount: 4))[1];
        var items = itemList.GetProperty("itemListElement").EnumerateArray().ToList();

        Assert.Equal(4, items.Count);
        Assert.Equal(1, items[0].GetProperty("position").GetInt32());
        Assert.Equal("Test Establishment 1", items[0].GetProperty("name").GetString());
    }
}
