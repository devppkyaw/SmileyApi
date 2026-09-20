namespace SmilrApi.Core.Utils;

/// <summary>How many establishments hold each Smiley score (1 = best, 4 = worst).</summary>
public readonly record struct ScoreDistribution(int S1, int S2, int S3, int S4)
{
    public int Total => S1 + S2 + S3 + S4;

    public int CountOf(int score) => score switch { 1 => S1, 2 => S2, 3 => S3, 4 => S4, _ => 0 };

    /// <summary>This distribution minus one establishment holding <paramref name="score"/> — used to take
    /// the benchmarked location itself out of its own peer group.</summary>
    public ScoreDistribution Without(int score) => score switch
    {
        1 => this with { S1 = Math.Max(0, S1 - 1) },
        2 => this with { S2 = Math.Max(0, S2 - 1) },
        3 => this with { S3 = Math.Max(0, S3 - 1) },
        4 => this with { S4 = Math.Max(0, S4 - 1) },
        _ => this,
    };

    public double? Average => Total == 0 ? null : (S1 + 2.0 * S2 + 3.0 * S3 + 4.0 * S4) / Total;

    public double? TopSharePercent => Total == 0 ? null : Math.Round(100.0 * S1 / Total, 1);
}

public enum BenchmarkScope { Area, National }

/// <summary>One location compared with its peers. The three percentages are shares of the peer group,
/// from this location's point of view: peers with the same score, peers doing better (a lower score
/// number) and peers doing worse. They can differ from 100 by rounding.</summary>
public record LocationBenchmark(
    int Score,
    BenchmarkScope Scope,
    ScoreDistribution Peers,
    double PeerAverage,
    double PeerTopSharePercent,
    double SamePercent,
    double PeersBetterPercent,
    double PeersWorsePercent)
{
    public int PeerCount => Peers.Total;
}

public record PortfolioBenchmark(
    int Benchmarked,
    int Total,
    double YourAverage,
    double PeerAverage,
    double YourTopSharePercent,
    double PeerTopSharePercent,
    int BetterThanPeers,
    int InLineWithPeers,
    int WorseThanPeers);

/// <summary>
/// Benchmarks a business's locations against peers — same Pixibranche category in the same City,
/// falling back to the same category nationwide when the local group is too small to mean anything.
///
/// Scores are discrete (1-4) and heavily skewed to 1, so a single "percentile" is misleading (having a 1
/// is "tied with 90% of peers"). Instead the comparison reports the share of peers with the same, a
/// better and a worse score, plus the peer average and the peers' share with the top score.
/// </summary>
public static class BenchmarkCalculator
{
    /// <summary>Fewer peers than this and the area group is replaced by the nationwide category.</summary>
    public const int MinPeers = 10;

    /// <summary>A location within this many points of its peer average counts as "in line".</summary>
    public const double InLineTolerance = 0.05;

    /// <summary>Case-insensitive key for a (City, Pixibranche) peer group. SQL Server compares these
    /// case-insensitively, so lookups built from establishment rows must not be case-sensitive.</summary>
    public static string PeerKey(string city, string category) =>
        city.Trim().ToLowerInvariant() + "||" + category.Trim().ToLowerInvariant();

    /// <param name="score">The location's own current score (1-4).</param>
    /// <param name="area">Distribution of scored establishments in the same City + category, or null.</param>
    /// <param name="national">Distribution of scored establishments in the same category nationwide, or null.</param>
    /// <param name="selfInPeerSets">True when the location itself is counted in the distributions passed in
    /// (it passes the same filters as the peer queries), so it has to be subtracted from them.</param>
    /// <returns>Null when neither group has at least <see cref="MinPeers"/> peers.</returns>
    public static LocationBenchmark? ForLocation(
        int score, ScoreDistribution? area, ScoreDistribution? national, bool selfInPeerSets = true)
    {
        if (score is < 1 or > 4) return null;

        var scope = BenchmarkScope.Area;
        var peers = Peers(area, score, selfInPeerSets);
        if (peers.Total < MinPeers)
        {
            scope = BenchmarkScope.National;
            peers = Peers(national, score, selfInPeerSets);
            if (peers.Total < MinPeers) return null;
        }

        var total = (double)peers.Total;
        var peersBetter = 0;
        var peersWorse = 0;
        for (var s = 1; s <= 4; s++)
        {
            if (s < score) peersBetter += peers.CountOf(s);
            else if (s > score) peersWorse += peers.CountOf(s);
        }
        var same = peers.CountOf(score);

        return new LocationBenchmark(
            score, scope, peers,
            PeerAverage: peers.Average!.Value,
            PeerTopSharePercent: peers.TopSharePercent!.Value,
            SamePercent: Pct(same, total),
            PeersBetterPercent: Pct(peersBetter, total),
            PeersWorsePercent: Pct(peersWorse, total));
    }

    /// <summary>Rolls per-location benchmarks up into one portfolio summary.</summary>
    /// <param name="items">Only the locations that could be benchmarked.</param>
    /// <param name="totalLocations">All the business's locations, benchmarked or not.</param>
    public static PortfolioBenchmark? Rollup(IReadOnlyList<LocationBenchmark> items, int totalLocations)
    {
        if (items.Count == 0) return null;

        var better = 0;
        var inLine = 0;
        var worse = 0;
        foreach (var i in items)
        {
            var gap = i.Score - i.PeerAverage; // positive = worse than peers (higher score is worse)
            if (gap > InLineTolerance) worse++;
            else if (gap < -InLineTolerance) better++;
            else inLine++;
        }

        return new PortfolioBenchmark(
            Benchmarked: items.Count,
            Total: totalLocations,
            YourAverage: Math.Round(items.Average(i => (double)i.Score), 2),
            PeerAverage: Math.Round(items.Average(i => i.PeerAverage), 2),
            YourTopSharePercent: Math.Round(100.0 * items.Count(i => i.Score == 1) / items.Count, 1),
            PeerTopSharePercent: Math.Round(items.Average(i => i.PeerTopSharePercent), 1),
            BetterThanPeers: better,
            InLineWithPeers: inLine,
            WorseThanPeers: worse);
    }

    private static ScoreDistribution Peers(ScoreDistribution? group, int score, bool selfIncluded) =>
        group is null ? default : (selfIncluded ? group.Value.Without(score) : group.Value);

    private static double Pct(int count, double total) => Math.Round(100.0 * count / total, 1);
}
