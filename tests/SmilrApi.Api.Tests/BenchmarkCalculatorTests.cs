using SmilrApi.Core.Utils;

namespace SmilrApi.Api.Tests;

public class BenchmarkCalculatorTests
{
    // 90 x score 1, 8 x score 2, 2 x score 3 = 100 establishments, self included where noted.
    private static readonly ScoreDistribution Area = new(90, 8, 2, 0);

    [Fact]
    public void The_location_itself_is_removed_from_its_own_peer_group()
    {
        var b = BenchmarkCalculator.ForLocation(1, Area, null, selfInPeerSets: true)!;
        Assert.Equal(99, b.PeerCount);
        Assert.Equal(89, b.Peers.S1);
    }

    [Fact]
    public void A_location_not_counted_in_the_peer_query_is_not_subtracted()
    {
        var b = BenchmarkCalculator.ForLocation(1, Area, null, selfInPeerSets: false)!;
        Assert.Equal(100, b.PeerCount);
        Assert.Equal(90, b.Peers.S1);
    }

    [Fact]
    public void Same_better_and_worse_are_reported_from_the_locations_point_of_view()
    {
        // Score 2 among 100 peers (self excluded first): 90 score 1 (better), 7 same, 2 score 3 (worse).
        var b = BenchmarkCalculator.ForLocation(2, Area, null)!;
        Assert.Equal(99, b.PeerCount);
        Assert.Equal(7.1, b.SamePercent);
        Assert.Equal(90.9, b.PeersBetterPercent);
        Assert.Equal(2.0, b.PeersWorsePercent);
        Assert.InRange(b.SamePercent + b.PeersBetterPercent + b.PeersWorsePercent, 99.8, 100.2);
    }

    [Fact]
    public void Computes_peer_average_and_top_share()
    {
        var b = BenchmarkCalculator.ForLocation(1, new ScoreDistribution(10, 10, 0, 0), null, selfInPeerSets: false)!;
        Assert.Equal(1.5, b.PeerAverage);
        Assert.Equal(50.0, b.PeerTopSharePercent);
    }

    [Theory]
    [InlineData(10, BenchmarkScope.Area)]      // exactly MinPeers after removing self -> area group is used
    [InlineData(9, BenchmarkScope.National)]   // one short -> falls back to the nationwide category
    public void Falls_back_to_the_national_category_below_the_minimum_peer_count(int areaPeers, BenchmarkScope expected)
    {
        var area = new ScoreDistribution(areaPeers + 1, 0, 0, 0);          // +1 for the location itself
        var national = new ScoreDistribution(5000, 400, 50, 5);
        var b = BenchmarkCalculator.ForLocation(1, area, national)!;
        Assert.Equal(expected, b.Scope);
    }

    [Fact]
    public void Returns_null_when_neither_group_has_enough_peers()
    {
        Assert.Null(BenchmarkCalculator.ForLocation(1, new ScoreDistribution(3, 0, 0, 0), new ScoreDistribution(4, 0, 0, 0)));
        Assert.Null(BenchmarkCalculator.ForLocation(1, null, null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-1)]
    public void Scores_outside_1_to_4_are_not_benchmarked(int score) =>
        Assert.Null(BenchmarkCalculator.ForLocation(score, Area, Area));

    [Fact]
    public void Peer_keys_ignore_case_and_surrounding_whitespace()
    {
        Assert.Equal(
            BenchmarkCalculator.PeerKey("Aalborg", "Bagere og bagerafdelinger"),
            BenchmarkCalculator.PeerKey(" aalborg ", "BAGERE OG BAGERAFDELINGER"));
        Assert.NotEqual(BenchmarkCalculator.PeerKey("Aalborg", "x"), BenchmarkCalculator.PeerKey("Aarhus", "x"));
    }

    [Fact]
    public void Distribution_without_never_goes_negative()
    {
        Assert.Equal(new ScoreDistribution(0, 0, 0, 0), new ScoreDistribution(0, 0, 0, 0).Without(1));
        Assert.Equal(0, new ScoreDistribution(0, 0, 0, 0).Total);
        Assert.Null(new ScoreDistribution(0, 0, 0, 0).Average);
    }
}
