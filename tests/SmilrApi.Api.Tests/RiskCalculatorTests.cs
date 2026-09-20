using SmilrApi.Core.Utils;

namespace SmilrApi.Api.Tests;

public class RiskCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 9, 20);

    private static ScoreTransition Down(int daysAgo, int from = 1, int to = 2) => new(Today.AddDays(-daysAgo), from, to);
    private static ScoreTransition Up(int daysAgo, int from = 2, int to = 1) => new(Today.AddDays(-daysAgo), from, to);

    private static LocationRisk Assess(int? score, params ScoreTransition[] transitions) =>
        RiskCalculator.Assess(score, transitions, Today);

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void A_current_score_of_3_or_4_is_at_risk(int score)
    {
        var r = Assess(score);
        Assert.Equal(RiskLevel.AtRisk, r.Level);
        Assert.Equal([RiskReason.HighScore], r.Reasons);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void A_good_score_with_no_recent_change_is_ok_and_needs_no_attention(int score)
    {
        var r = Assess(score);
        Assert.Equal(RiskLevel.Ok, r.Level);
        Assert.False(r.NeedsAttention);
        Assert.Equal(long.MaxValue, r.Rank);
    }

    [Fact]
    public void Two_consecutive_downgrades_within_90_days_are_at_risk_even_at_a_low_score()
    {
        var r = Assess(2, Down(10, 1, 2), Down(60, 1, 2));
        Assert.Equal(RiskLevel.AtRisk, r.Level);
        Assert.Contains(RiskReason.ConsecutiveDeclines, r.Reasons);
        Assert.DoesNotContain(RiskReason.RecentDecline, r.Reasons);
    }

    [Fact]
    public void Two_downgrades_with_the_older_one_outside_the_window_is_only_watch()
    {
        var r = Assess(2, Down(10), Down(120));
        Assert.Equal(RiskLevel.Watch, r.Level);
        Assert.Equal([RiskReason.RecentDecline], r.Reasons);
    }

    [Fact]
    public void One_downgrade_in_the_window_is_watch_and_carries_its_date()
    {
        var r = Assess(2, Down(2));
        Assert.Equal(RiskLevel.Watch, r.Level);
        Assert.Equal(Today.AddDays(-2), r.DeclineDate);
        Assert.True(r.NeedsAttention);
    }

    [Fact]
    public void A_decline_the_location_has_since_recovered_from_is_not_flagged()
    {
        // Navnelbnr 1356778's shape: 1 -> 2 on 29/06, then 2 -> 1 on 27/08. Its latest score is a 1.
        var r = Assess(1, Up(24, 2, 1), Down(83, 1, 2));
        Assert.Equal(RiskLevel.Ok, r.Level);
        Assert.False(r.NeedsAttention);
        Assert.Null(r.DeclineDate);
    }

    [Fact]
    public void Two_declines_followed_by_a_recovery_are_not_flagged()
    {
        var r = Assess(1, Up(5, 3, 1), Down(20, 2, 3), Down(40, 1, 2));
        Assert.Equal(RiskLevel.Ok, r.Level);
        Assert.False(r.NeedsAttention);
    }

    [Fact]
    public void A_partial_recovery_that_is_itself_the_latest_change_is_not_a_decline()
    {
        // 1 -> 3 (decline), then 3 -> 2 (improvement): the latest change is an improvement.
        var r = Assess(2, Up(5, 3, 2), Down(30, 1, 3));
        Assert.False(r.NeedsAttention);
    }

    [Fact]
    public void An_improvement_only_is_ok()
    {
        Assert.Equal(RiskLevel.Ok, Assess(1, Up(5)).Level);
        Assert.False(Assess(1, Up(5)).NeedsAttention);
    }

    [Fact]
    public void A_downgrade_older_than_the_window_is_ok()
    {
        var r = Assess(2, Down(91));
        Assert.Equal(RiskLevel.Ok, r.Level);
        Assert.False(r.NeedsAttention);
    }

    [Theory]
    [InlineData(90, true)]   // window start is inclusive
    [InlineData(91, false)]
    public void The_90_day_window_boundary_is_inclusive(int daysAgo, bool expectedInWindow) =>
        Assert.Equal(expectedInWindow, Assess(2, Down(daysAgo)).NeedsAttention);

    [Fact]
    public void A_location_with_no_score_is_listed_but_is_not_a_risk_level()
    {
        var r = Assess(null);
        Assert.Equal(RiskLevel.Ok, r.Level);
        Assert.Equal([RiskReason.NoScoreYet], r.Reasons);
        Assert.True(r.NeedsAttention);
    }

    [Fact]
    public void A_high_score_that_also_declined_reports_both_reasons_most_severe_first()
    {
        var r = Assess(4, Down(3, 3, 4));
        Assert.Equal(RiskLevel.AtRisk, r.Level);
        Assert.Equal([RiskReason.HighScore, RiskReason.RecentDecline], r.Reasons);
    }

    [Fact]
    public void Delisted_locations_are_never_flagged()
    {
        var r = RiskCalculator.Assess(4, [Down(3)], Today, isDelisted: true);
        Assert.Equal(RiskLevel.Ok, r.Level);
        Assert.False(r.NeedsAttention);
        Assert.False(RiskCalculator.Assess(null, [], Today, isDelisted: true).NeedsAttention);
    }

    [Fact]
    public void Input_order_of_transitions_does_not_matter()
    {
        var a = RiskCalculator.Assess(1, [Down(10), Down(30)], Today);
        var b = RiskCalculator.Assess(1, [Down(30), Down(10)], Today);
        Assert.Equal(RiskLevel.AtRisk, a.Level);
        Assert.Equal(a.Level, b.Level);
        Assert.Equal(a.DeclineDate, b.DeclineDate);
    }

    [Fact]
    public void Rank_orders_high_score_then_recent_decline_then_no_score_then_the_rest()
    {
        var score4 = Assess(4);
        var score3 = Assess(3);
        var declinedNewest = Assess(2, Down(2));
        var declinedOlder = Assess(2, Down(40));
        var noScore = Assess(null);
        var fine = Assess(1);

        var ordered = new[] { fine, noScore, declinedOlder, score3, declinedNewest, score4 }
            .OrderBy(r => r.Rank).ToList();

        Assert.Equal([score4, score3, declinedNewest, declinedOlder, noScore, fine], ordered);
    }
}
