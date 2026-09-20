namespace SmilrApi.Core.Utils;

public enum RiskLevel { Ok = 0, Watch = 1, AtRisk = 2 }

/// <summary>Why a location needs attention. A location can have several.</summary>
public enum RiskReason
{
    /// <summary>Current score is 3 or 4.</summary>
    HighScore,
    /// <summary>The two most recent score changes were both downgrades, both within the recent window.</summary>
    ConsecutiveDeclines,
    /// <summary>The most recent score change is a downgrade within the recent window.</summary>
    RecentDecline,
    /// <summary>No score yet — awaiting first inspection.</summary>
    NoScoreYet,
}

/// <summary>A score change. A downgrade moves to a higher score number (1 = best, 4 = worst).</summary>
public readonly record struct ScoreTransition(DateOnly Date, int Previous, int New)
{
    public bool IsDowngrade => New > Previous;
}

/// <param name="Level">Ok / Watch / AtRisk — what the Locations badge and risk sort use.</param>
/// <param name="Reasons">Every reason the location qualifies for the Overview's "Needs attention" panel,
/// most severe first. Empty when it doesn't qualify.</param>
/// <param name="DeclineDate">Date of the latest score change, when that change is an in-window downgrade.</param>
/// <param name="Rank">Sort key, lowest = most concerning; <see cref="long.MaxValue"/> when the location
/// doesn't need attention.</param>
public record LocationRisk(RiskLevel Level, IReadOnlyList<RiskReason> Reasons, DateOnly? DeclineDate, long Rank)
{
    public bool NeedsAttention => Reasons.Count > 0;

    public static readonly LocationRisk None = new(RiskLevel.Ok, [], null, long.MaxValue);
}

/// <summary>
/// Single definition of "how concerning is this location", shared by the Overview's "Needs attention"
/// panel and the Locations tab (badges, "Risk: most concerning first" sort, needs-attention filter).
///
///   AtRisk = current score 3 or 4, OR the two most recent score changes are both downgrades and both
///            within <see cref="RecentWindowDays"/> days
///   Watch  = the most recent score change is a downgrade within <see cref="RecentWindowDays"/> days
///            (and not AtRisk); a decline the location has since recovered from does not count
///   Ok     = otherwise
///
/// "Needs attention" additionally lists locations with no score yet, which is not a risk level (there's
/// nothing to judge), so it is a reason but leaves the level at Ok. "Overdue for reinspection" is
/// deliberately not a trigger: there is no per-category inspection cadence in the data, so it would be a
/// guess rather than a fact.
/// </summary>
public static class RiskCalculator
{
    public const int RecentWindowDays = 90;

    // Rank = tier * TierSpan + tie-break, lower = more concerning. Tiers: 0 current 3/4, 1 recent decline,
    // 2 no score yet. Tie-break stays well inside TierSpan.
    private const long TierSpan = 100_000_000;
    private const int DayNumberCeiling = 4_000_000; // above the DayNumber of any date this app will see

    /// <param name="latestScore">Current score, or null when there isn't one yet.</param>
    /// <param name="transitions">The location's score changes, any order; only those inside the window
    /// matter, and only the two newest are used for the "consecutive" test.</param>
    /// <param name="today">Reference date (UTC); the window is the last 90 days, start inclusive.</param>
    /// <param name="isDelisted">Closed / no longer in the source feed — never flagged.</param>
    public static LocationRisk Assess(
        int? latestScore, IReadOnlyCollection<ScoreTransition> transitions, DateOnly today, bool isDelisted = false)
    {
        if (isDelisted) return LocationRisk.None;

        var windowStart = today.AddDays(-RecentWindowDays);
        var newestFirst = transitions.OrderByDescending(t => t.Date).ToList();

        // Only the location's *latest* score change counts: a location that dipped and has since recovered
        // (e.g. 1 -> 2 in June, 2 -> 1 in August) is back where it was and isn't flagged. The repository
        // only supplies in-window changes, so when any are supplied the newest one is the real latest.
        var latestDeclined = newestFirst.Count > 0 && newestFirst[0].IsDowngrade && newestFirst[0].Date >= windowStart;
        var consecutive = newestFirst.Count >= 2
            && newestFirst[0].IsDowngrade && newestFirst[0].Date >= windowStart
            && newestFirst[1].IsDowngrade && newestFirst[1].Date >= windowStart;
        var highScore = latestScore is >= 3;
        var noScore = latestScore is null;

        var reasons = new List<RiskReason>();
        if (highScore) reasons.Add(RiskReason.HighScore);
        if (consecutive) reasons.Add(RiskReason.ConsecutiveDeclines);
        else if (latestDeclined) reasons.Add(RiskReason.RecentDecline);
        if (noScore) reasons.Add(RiskReason.NoScoreYet);

        var level = highScore || consecutive ? RiskLevel.AtRisk
                  : latestDeclined ? RiskLevel.Watch
                  : RiskLevel.Ok;

        DateOnly? declineDate = latestDeclined ? newestFirst[0].Date : null;

        long rank;
        if (highScore) rank = 0 * TierSpan + (4 - latestScore!.Value);                       // 4 before 3
        else if (latestDeclined) rank = 1 * TierSpan + (DayNumberCeiling - declineDate!.Value.DayNumber); // newest first
        else if (noScore) rank = 2 * TierSpan;
        else rank = long.MaxValue;

        return new LocationRisk(level, reasons, declineDate, rank);
    }
}
