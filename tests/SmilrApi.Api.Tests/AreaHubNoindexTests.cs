using SmilrApi.Api.Endpoints;

namespace SmilrApi.Api.Tests;

/// <summary>Tests ComputeHubListingNoindex — shared by both AreaHubHandlerAsync and
/// CategoryHubHandlerAsync (identical noindex formula, "page N/thin listing/sort-active/filter-active"),
/// so these cases cover both callers.</summary>
public class AreaHubNoindexTests
{
    // Well above CategorySlugThreshold (3) and every other condition false — the one "indexable" case.
    private const int AmpleCount = 50;

    [Fact]
    public void Indexable_when_page_one_ample_count_no_sort_no_filter()
    {
        Assert.False(FindEndpoints.ComputeHubListingNoindex(pageNum: 1, totalCount: AmpleCount, sortNorm: null, hideUnscored: false));
    }

    [Fact]
    public void Noindex_when_page_greater_than_one()
    {
        Assert.True(FindEndpoints.ComputeHubListingNoindex(pageNum: 2, totalCount: AmpleCount, sortNorm: null, hideUnscored: false));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Noindex_when_area_is_too_thin(int totalCount)
    {
        Assert.True(FindEndpoints.ComputeHubListingNoindex(pageNum: 1, totalCount, sortNorm: null, hideUnscored: false));
    }

    [Fact]
    public void Indexable_at_exactly_the_thin_area_threshold()
    {
        // CategorySlugThreshold is 3 — the boundary itself should be indexable, only strictly below it noindex.
        Assert.False(FindEndpoints.ComputeHubListingNoindex(pageNum: 1, totalCount: 3, sortNorm: null, hideUnscored: false));
    }

    [Fact]
    public void Noindex_when_a_sort_is_active()
    {
        Assert.True(FindEndpoints.ComputeHubListingNoindex(pageNum: 1, totalCount: AmpleCount, sortNorm: "score_asc", hideUnscored: false));
    }

    [Fact]
    public void Noindex_when_hide_unscored_is_active()
    {
        Assert.True(FindEndpoints.ComputeHubListingNoindex(pageNum: 1, totalCount: AmpleCount, sortNorm: null, hideUnscored: true));
    }

    [Fact]
    public void Noindex_when_sort_and_filter_and_page_all_combine()
    {
        Assert.True(FindEndpoints.ComputeHubListingNoindex(pageNum: 3, totalCount: AmpleCount, sortNorm: "recent", hideUnscored: true));
    }
}
