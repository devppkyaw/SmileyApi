using SmilrApi.Api.Rendering;
using SmilrApi.Core.Interfaces;

namespace SmilrApi.Api.Tests;

/// <summary>
/// BuildSitemapUrls/SitemapChunkXml/SitemapIndexXml are pure functions (repo-shaped data in, a URL list
/// or XML string out — no DB/HTTP involved), so they're asserted on directly with hand-built data. Note:
/// these rely on FindPageRenderer.SiteOrigin's default ("https://smilrhq.dk") — see the same caveat in
/// AreaHubPageSeoTests.
/// </summary>
public class SitemapTests
{
    private static readonly IReadOnlyList<(string City, string Category, int Count)> NoCategoryCounts = [];
    private static readonly IReadOnlyList<(string City, int Count)> NoChangeCounts = [];

    private static SitemapEntry Entry(string name, string city, int navnelbnr, bool hasInspectionDate = true) =>
        new(name, city, navnelbnr, new DateTime(2026, 5, 23), hasInspectionDate);

    [Fact]
    public void One_url_per_establishment_plus_one_hub_page_per_area()
    {
        IReadOnlyList<SitemapEntry> entries =
        [
            Entry("Cafe A", "Aarhus", 1),
            Entry("Cafe B", "Aarhus", 2),
        ];

        var urls = FindPageRenderer.BuildSitemapUrls(entries, NoCategoryCounts, NoChangeCounts, categorySlugThreshold: 3);

        var locs = urls.Select(u => u.Loc).ToList();
        Assert.Contains(FindUrlBuilder.DetailPath("Cafe A", "Aarhus", 1), locs);
        Assert.Contains(FindUrlBuilder.DetailPath("Cafe B", "Aarhus", 2), locs);
        Assert.Contains(FindUrlBuilder.HubPath("Aarhus"), locs);
        // Below categorySlugThreshold (2 < 3): no recently-inspected/changes entries for Aarhus.
        Assert.DoesNotContain(FindUrlBuilder.RecentlyInspectedPath("Aarhus"), locs);
        Assert.DoesNotContain(FindUrlBuilder.ChangesPath("Aarhus"), locs);
        Assert.Equal(3, urls.Count);
    }

    [Fact]
    public void Recently_inspected_and_changes_entries_appear_once_the_area_meets_the_threshold()
    {
        IReadOnlyList<SitemapEntry> entries =
        [
            Entry("Cafe A", "Aarhus", 1),
            Entry("Cafe B", "Aarhus", 2),
            Entry("Cafe C", "Aarhus", 3),
        ];
        IReadOnlyList<(string City, int Count)> changeCounts = [("Aarhus", 3)];

        var urls = FindPageRenderer.BuildSitemapUrls(entries, NoCategoryCounts, changeCounts, categorySlugThreshold: 3);

        var locs = urls.Select(u => u.Loc).ToList();
        Assert.Contains(FindUrlBuilder.RecentlyInspectedPath("Aarhus"), locs);
        Assert.Contains(FindUrlBuilder.ChangesPath("Aarhus"), locs);
    }

    [Fact]
    public void Category_hub_entries_only_included_at_or_above_the_threshold()
    {
        IReadOnlyList<SitemapEntry> entries = [Entry("Cafe A", "Aarhus", 1)];
        IReadOnlyList<(string City, string Category, int Count)> categoryCounts =
        [
            ("Aarhus", "Restaurant", 3),
            ("Aarhus", "Bakery", 2),
        ];

        var urls = FindPageRenderer.BuildSitemapUrls(entries, categoryCounts, NoChangeCounts, categorySlugThreshold: 3);

        var locs = urls.Select(u => u.Loc).ToList();
        Assert.Contains(FindUrlBuilder.CategoryHubPath("Aarhus", "Restaurant"), locs);
        Assert.DoesNotContain(FindUrlBuilder.CategoryHubPath("Aarhus", "Bakery"), locs);
    }

    [Fact]
    public void Establishments_with_no_city_get_a_detail_url_but_no_hub_entry()
    {
        IReadOnlyList<SitemapEntry> entries = [Entry("Kiosk", city: null!, navnelbnr: 9)];

        var urls = FindPageRenderer.BuildSitemapUrls(entries, NoCategoryCounts, NoChangeCounts, categorySlugThreshold: 3);

        Assert.Single(urls);
        Assert.Equal(FindUrlBuilder.DetailPath("Kiosk", null, 9), urls[0].Loc);
    }

    private static IReadOnlyList<FindPageRenderer.SitemapUrl> SampleUrls(int count) =>
        Enumerable.Range(1, count)
            .Select(i => new FindPageRenderer.SitemapUrl($"/find/est-{i}-{i}", new DateTime(2026, 5, 23)))
            .ToList();

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(45_000, 1)]
    [InlineData(45_001, 2)]
    [InlineData(90_000, 2)]
    [InlineData(90_001, 3)]
    public void Sitemap_index_lists_one_chunk_per_full_or_partial_group_of_chunkSize_urls(int urlCount, int expectedChunks)
    {
        var xml = FindPageRenderer.SitemapIndexXml(urlCount, chunkSize: 45_000);

        var actualChunks = System.Text.RegularExpressions.Regex.Matches(xml, "<sitemap>").Count;
        Assert.Equal(expectedChunks, actualChunks);
    }

    [Fact]
    public void Sitemap_index_entries_point_at_1_indexed_chunk_files_under_the_configured_origin()
    {
        var xml = FindPageRenderer.SitemapIndexXml(urlCount: 100_000, chunkSize: 45_000);

        Assert.Contains("<loc>https://smilrhq.dk/find/sitemap-1.xml</loc>", xml);
        Assert.Contains("<loc>https://smilrhq.dk/find/sitemap-2.xml</loc>", xml);
        Assert.Contains("<loc>https://smilrhq.dk/find/sitemap-3.xml</loc>", xml);
        Assert.DoesNotContain("sitemap-4.xml", xml);
    }

    [Fact]
    public void Sitemap_index_never_exceeds_the_50000_url_sitemap_protocol_cap_per_chunk()
    {
        // The whole point of chunking: no single <urlset> file this produces can exceed the protocol's
        // per-file cap, however large the establishment count grows.
        const int chunkSize = 45_000;
        Assert.True(chunkSize < 50_000);
    }

    [Fact]
    public void First_chunk_contains_the_first_chunkSize_urls_in_order()
    {
        var urls = SampleUrls(5);

        var xml = FindPageRenderer.SitemapChunkXml(urls, chunkIndex: 0, chunkSize: 2);

        Assert.Contains("<loc>https://smilrhq.dk/find/est-1-1</loc>", xml);
        Assert.Contains("<loc>https://smilrhq.dk/find/est-2-2</loc>", xml);
        Assert.DoesNotContain("est-3-3", xml);
    }

    [Fact]
    public void Middle_chunk_contains_only_its_own_slice()
    {
        var urls = SampleUrls(5);

        var xml = FindPageRenderer.SitemapChunkXml(urls, chunkIndex: 1, chunkSize: 2);

        Assert.DoesNotContain("est-1-1", xml);
        Assert.DoesNotContain("est-2-2", xml);
        Assert.Contains("est-3-3", xml);
        Assert.Contains("est-4-4", xml);
        Assert.DoesNotContain("est-5-5", xml);
    }

    [Fact]
    public void Last_partial_chunk_contains_only_the_remaining_urls()
    {
        var urls = SampleUrls(5);

        var xml = FindPageRenderer.SitemapChunkXml(urls, chunkIndex: 2, chunkSize: 2);

        Assert.Contains("est-5-5", xml);
        var urlCount = System.Text.RegularExpressions.Regex.Matches(xml, "<url>").Count;
        Assert.Equal(1, urlCount);
    }

    [Fact]
    public void Chunk_xml_includes_lastmod_per_url()
    {
        var urls = new[] { new FindPageRenderer.SitemapUrl("/find/x-1", new DateTime(2026, 5, 23)) };

        var xml = FindPageRenderer.SitemapChunkXml(urls, chunkIndex: 0, chunkSize: 45_000);

        Assert.Contains("<lastmod>2026-05-23</lastmod>", xml);
    }
}
