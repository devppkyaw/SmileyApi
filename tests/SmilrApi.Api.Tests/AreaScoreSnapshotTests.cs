using SmilrApi.Core.Interfaces;

namespace SmilrApi.Api.Tests;

public class AreaScoreSnapshotTests
{
    [Fact]
    public void Zero_scored_establishments_yields_zero_percent_not_a_division_error()
    {
        var snapshot = new AreaScoreSnapshot(TotalScored: 0, TopScoreCount: 0);
        Assert.Equal(0, snapshot.TopSharePercent);
    }

    [Theory]
    [InlineData(200, 194, 97.0)]
    [InlineData(3, 1, 33.3)]  // rounds to one decimal place, not truncated
    [InlineData(3, 2, 66.7)]
    [InlineData(1, 1, 100.0)]
    [InlineData(1, 0, 0.0)]
    public void Computes_the_rounded_percentage_of_scored_establishments_with_the_top_score(
        int totalScored, int topScoreCount, double expectedPercent)
    {
        var snapshot = new AreaScoreSnapshot(totalScored, topScoreCount);
        Assert.Equal(expectedPercent, snapshot.TopSharePercent);
    }
}
