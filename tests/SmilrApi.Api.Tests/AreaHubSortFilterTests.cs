using SmilrApi.Api.Endpoints;

namespace SmilrApi.Api.Tests;

public class AreaHubSortFilterTests
{
    [Theory]
    [InlineData("score_asc", "score_asc")]
    [InlineData("score_desc", "score_desc")]
    [InlineData("recent", "recent")]
    [InlineData("bogus", null)]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("SCORE_ASC", null)] // whitelist is case-sensitive — no silent normalization
    public void NormalizeSort_only_accepts_the_exact_whitelisted_values(string? input, string? expected)
    {
        Assert.Equal(expected, FindEndpoints.NormalizeSort(input));
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData(null, false)]
    [InlineData("true", false)]
    [InlineData("yes", false)]
    [InlineData("", false)]
    public void NormalizeHideUnscored_only_the_literal_1_opts_in(string? input, bool expected)
    {
        Assert.Equal(expected, FindEndpoints.NormalizeHideUnscored(input));
    }
}
