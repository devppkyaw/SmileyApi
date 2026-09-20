using SmilrApi.Core.Utils;

namespace SmilrApi.Api.Tests;

public class LocationListPagingTests
{
    [Theory]
    [InlineData(null, null, 1, 25)]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-5, -1, 1, 1)]
    [InlineData(3, 50, 3, 50)]
    [InlineData(2, 10_000, 2, 100)]  // page size is capped, never trusted from the client
    public void Normalize_defaults_and_clamps_page_and_page_size(int? page, int? pageSize, int expectedPage, int expectedSize)
    {
        var (p, s) = LocationListPaging.Normalize(page, pageSize);
        Assert.Equal(expectedPage, p);
        Assert.Equal(expectedSize, s);
    }

    [Theory]
    [InlineData("score-asc", "score-asc")]
    [InlineData("score-desc", "score-desc")]
    [InlineData("risk", "risk")]
    [InlineData("cvr", "cvr")]
    [InlineData(null, "cvr")]
    [InlineData("name; DROP TABLE", "cvr")]  // unknown keys fall back to the default, not an error
    public void NormalizeSort_only_accepts_known_keys(string? input, string expected) =>
        Assert.Equal(expected, LocationListPaging.NormalizeSort(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeSearch_returns_null_for_blank_input(string? input) =>
        Assert.Null(LocationListPaging.NormalizeSearch(input));

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(" 29189420 ", "29189420")]
    [InlineData("none", "none")]
    [InlineData(" NONE ", "none")]  // the no-CVR sentinel is case-insensitive and normalized
    public void NormalizeCvr_trims_and_recognizes_the_no_cvr_sentinel(string? input, string? expected) =>
        Assert.Equal(expected, LocationListPaging.NormalizeCvr(input));

    [Fact]
    public void NormalizeCvr_caps_length()
    {
        Assert.Equal(20, LocationListPaging.NormalizeCvr(new string('9', 200))!.Length);
    }

    [Fact]
    public void NormalizeSearch_trims_and_caps_length()
    {
        Assert.Equal("cafe", LocationListPaging.NormalizeSearch("  cafe "));
        Assert.Equal(100, LocationListPaging.NormalizeSearch(new string('x', 500))!.Length);
    }
}
