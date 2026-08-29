using SmilrApi.Core.Models;
using SmilrApi.Core.Utils;

namespace SmilrApi.Api.Tests;

// These cases are the reference semantics the SQL rewrite in EstablishmentRepository
// (GetRecentChangesByCitiesAsync/GetChangesSummaryAsync/GetChangeCountsByCityAsync) must match —
// see the worked-example proof in the "optimize-score-change-queries" plan.
public class ScoreChangeCalculatorTests
{
    private static Inspection Inspection(int score, DateOnly inspectedOn) =>
        new() { SmileyScore = score, InspectedOn = inspectedOn };

    [Fact]
    public void LatestChange_returns_null_for_empty_history()
    {
        Assert.Null(ScoreChangeCalculator.LatestChange([]));
    }

    [Fact]
    public void LatestChange_returns_null_for_a_single_inspection()
    {
        var history = new[] { Inspection(1, new DateOnly(2026, 1, 1)) };

        Assert.Null(ScoreChangeCalculator.LatestChange(history));
    }

    [Fact]
    public void LatestChange_returns_null_when_the_score_never_changed()
    {
        // Newest-first, as the method requires.
        var history = new[]
        {
            Inspection(1, new DateOnly(2026, 8, 20)),
            Inspection(1, new DateOnly(2026, 8, 10)),
            Inspection(1, new DateOnly(2026, 7, 1)),
        };

        Assert.Null(ScoreChangeCalculator.LatestChange(history));
    }

    [Fact]
    public void LatestChange_reports_a_change_at_the_newest_inspection()
    {
        var history = new[]
        {
            Inspection(1, new DateOnly(2026, 3, 1)), // newest
            Inspection(2, new DateOnly(2026, 2, 1)),
            Inspection(2, new DateOnly(2026, 1, 1)),
        };

        var change = ScoreChangeCalculator.LatestChange(history);

        Assert.NotNull(change);
        Assert.Equal(2, change!.Value.PreviousScore);
        Assert.Equal(1, change.Value.NewScore);
        Assert.Equal(new DateOnly(2026, 3, 1), change.Value.ChangeDate);
    }

    [Fact]
    public void LatestChange_is_not_erased_by_a_same_score_reinspection_after_a_real_change()
    {
        // Documented edge case from ScoreChangeCalculator's own doc comment: newest-first
        // [Aug20:1, Aug10:1, Jul1:2] must still report Jul1->Aug10 (2->1), not "no change",
        // because Aug20 re-confirmed Aug10's score rather than changing anything.
        var history = new[]
        {
            Inspection(1, new DateOnly(2026, 8, 20)), // newest — same as Aug10, doesn't erase the change
            Inspection(1, new DateOnly(2026, 8, 10)),
            Inspection(2, new DateOnly(2026, 7, 1)),
        };

        var change = ScoreChangeCalculator.LatestChange(history);

        Assert.NotNull(change);
        Assert.Equal(2, change!.Value.PreviousScore);
        Assert.Equal(1, change.Value.NewScore);
        Assert.Equal(new DateOnly(2026, 8, 10), change.Value.ChangeDate);
    }
}
