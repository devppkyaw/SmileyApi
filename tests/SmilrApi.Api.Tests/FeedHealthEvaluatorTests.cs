using SmilrApi.Core.Models;
using SmilrApi.Core.Utils;

namespace SmilrApi.Api.Tests;

// Reference semantics for the feed-health monitoring introduced after the 2026-09-08 incident
// (Fødevarestyrelsen's XML feed stopped being regenerated for 6 days with zero signal anywhere).
// Styled like ScoreChangeCalculatorTests: construct plain entities/DTOs directly, no mocking.
public class FeedHealthEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);
    private static readonly FeedHealthOptions DefaultOptions = new();

    private static FeedHealthMetrics Metrics(
        string? etag = "\"abc\"",
        DateTimeOffset? lastModified = null,
        int totalRowsSeen = 1000,
        int parsedRowCount = 1000,
        IReadOnlyDictionary<string, int>? nullCounts = null) =>
        new(etag, lastModified ?? new DateTimeOffset(2026, 9, 8, 6, 0, 0, TimeSpan.Zero),
            totalRowsSeen, parsedRowCount, nullCounts ?? new Dictionary<string, int> { ["Address"] = 5 });

    private static FeedHealthSnapshot Snapshot(
        string? etag = "\"abc\"",
        DateTimeOffset? lastModified = null,
        DateTime? headerChangeDetectedAt = null,
        int totalRowsSeen = 1000,
        int parsedRowCount = 1000,
        IReadOnlyDictionary<string, int>? nullCounts = null)
    {
        var snap = new FeedHealthSnapshot
        {
            Id = 1,
            LastEtag = etag,
            LastModifiedHeader = lastModified ?? new DateTimeOffset(2026, 9, 8, 6, 0, 0, TimeSpan.Zero),
            LastHeaderChangeDetectedAt = headerChangeDetectedAt ?? Now.AddHours(-1),
            LastRunAt = Now.AddDays(-1),
            LastTotalRowsSeen = totalRowsSeen,
            LastParsedRowCount = parsedRowCount,
        };
        snap.SetFieldNullCounts(nullCounts ?? new Dictionary<string, int> { ["Address"] = 5 });
        return snap;
    }

    [Fact]
    public void First_ever_run_produces_no_decisions()
    {
        var result = FeedHealthEvaluator.Evaluate(Metrics(), previous: null, priorAnomalies: [], DefaultOptions, Now);

        Assert.Empty(result.Decisions);
        Assert.Equal(Now, result.UpdatedSnapshot.LastHeaderChangeDetectedAt);
        Assert.Empty(result.UpdatedAnomalies); // no baseline to compare against yet, so none fabricated
    }

    // ── Feed staleness ──────────────────────────────────────────────────────────

    [Fact]
    public void Stale_feed_under_threshold_is_not_flagged()
    {
        var previous = Snapshot(headerChangeDetectedAt: Now.AddHours(-(DefaultOptions.StaleAfterHours - 1)));
        var current = Metrics(); // same etag/lastModified as previous -> header unchanged

        var result = FeedHealthEvaluator.Evaluate(current, previous, [], DefaultOptions, Now);

        Assert.DoesNotContain(result.Decisions, d => d.Kind == FeedHealthAlertKind.FeedStale);
    }

    [Fact]
    public void Stale_feed_first_crossing_threshold_is_Detected()
    {
        var previous = Snapshot(headerChangeDetectedAt: Now.AddHours(-DefaultOptions.StaleAfterHours));
        var current = Metrics(); // identical etag/lastModified -> header still unchanged

        var result = FeedHealthEvaluator.Evaluate(current, previous, [], DefaultOptions, Now);

        var decision = Assert.Single(result.Decisions);
        Assert.Equal(FeedHealthAlertKind.FeedStale, decision.Kind);
        Assert.Equal(FeedHealthAlertStatus.Detected, decision.Status);
        var anomaly = Assert.Single(result.UpdatedAnomalies, a => a.Kind == FeedHealthAlertKind.FeedStale);
        Assert.True(anomaly.IsActive);
        Assert.Equal(Now, anomaly.FirstDetectedAt);
    }

    [Fact]
    public void Stale_feed_still_unchanged_within_reminder_window_stays_silent()
    {
        var priorAnomaly = new FeedHealthAnomaly
        {
            Id = 10, Kind = FeedHealthAlertKind.FeedStale, IsActive = true,
            FirstDetectedAt = Now.AddDays(-1), LastAlertedAt = Now.AddDays(-1),
        };
        var previous = Snapshot(headerChangeDetectedAt: Now.AddHours(-100));
        var current = Metrics();

        var result = FeedHealthEvaluator.Evaluate(current, previous, [priorAnomaly], DefaultOptions, Now);

        Assert.Empty(result.Decisions);
        var anomaly = Assert.Single(result.UpdatedAnomalies, a => a.Kind == FeedHealthAlertKind.FeedStale);
        Assert.True(anomaly.IsActive);
        Assert.Equal(Now.AddDays(-1), anomaly.LastAlertedAt); // untouched
    }

    [Fact]
    public void Stale_feed_past_reminder_interval_sends_StillUnresolved_and_refreshes_LastAlertedAt()
    {
        var priorAnomaly = new FeedHealthAnomaly
        {
            Id = 10, Kind = FeedHealthAlertKind.FeedStale, IsActive = true,
            FirstDetectedAt = Now.AddDays(-10), LastAlertedAt = Now.AddDays(-DefaultOptions.ReminderIntervalDays),
        };
        var previous = Snapshot(headerChangeDetectedAt: Now.AddHours(-500));
        var current = Metrics();

        var result = FeedHealthEvaluator.Evaluate(current, previous, [priorAnomaly], DefaultOptions, Now);

        var decision = Assert.Single(result.Decisions);
        Assert.Equal(FeedHealthAlertStatus.StillUnresolved, decision.Status);
        Assert.Equal(Now.AddDays(-10), decision.FirstDetectedAt); // unchanged
        var anomaly = Assert.Single(result.UpdatedAnomalies, a => a.Kind == FeedHealthAlertKind.FeedStale);
        Assert.Equal(Now, anomaly.LastAlertedAt);
        Assert.Equal(Now.AddDays(-10), anomaly.FirstDetectedAt);
    }

    [Fact]
    public void Stale_feed_recovering_sends_Recovered_and_clears_FirstDetectedAt()
    {
        var priorAnomaly = new FeedHealthAnomaly
        {
            Id = 10, Kind = FeedHealthAlertKind.FeedStale, IsActive = true,
            FirstDetectedAt = Now.AddDays(-10), LastAlertedAt = Now.AddDays(-10),
        };
        var previous = Snapshot(etag: "\"old\"", headerChangeDetectedAt: Now.AddHours(-500));
        var current = Metrics(etag: "\"new\""); // header actually changed this run

        var result = FeedHealthEvaluator.Evaluate(current, previous, [priorAnomaly], DefaultOptions, Now);

        var decision = Assert.Single(result.Decisions);
        Assert.Equal(FeedHealthAlertStatus.Recovered, decision.Status);
        Assert.Equal(Now.AddDays(-10), decision.FirstDetectedAt); // reported for context, then cleared
        var anomaly = Assert.Single(result.UpdatedAnomalies, a => a.Kind == FeedHealthAlertKind.FeedStale);
        Assert.False(anomaly.IsActive);
        Assert.Null(anomaly.FirstDetectedAt);
        Assert.Null(anomaly.LastAlertedAt);
        Assert.Equal(Now, result.UpdatedSnapshot.LastHeaderChangeDetectedAt);
    }

    // ── Row-count collapse ──────────────────────────────────────────────────────

    [Fact]
    public void Row_count_drop_just_under_threshold_is_not_flagged()
    {
        var previous = Snapshot(parsedRowCount: 1000);
        var dropPct = DefaultOptions.RowCountDropPercentThreshold - 1;
        var current = Metrics(parsedRowCount: (int)(1000 * (1 - dropPct / 100)));

        var result = FeedHealthEvaluator.Evaluate(current, previous, [], DefaultOptions, Now);

        Assert.DoesNotContain(result.Decisions, d => d.Kind == FeedHealthAlertKind.RowCountCollapse);
    }

    [Fact]
    public void Row_count_drop_at_threshold_is_Detected()
    {
        var previous = Snapshot(parsedRowCount: 1000);
        var current = Metrics(parsedRowCount: 800); // exactly 20% drop

        var result = FeedHealthEvaluator.Evaluate(current, previous, [], DefaultOptions, Now);

        var decision = Assert.Single(result.Decisions, d => d.Kind == FeedHealthAlertKind.RowCountCollapse);
        Assert.Equal(FeedHealthAlertStatus.Detected, decision.Status);
    }

    [Fact]
    public void Row_count_baseline_of_zero_previous_rows_never_divides_by_zero()
    {
        var previous = Snapshot(parsedRowCount: 0, totalRowsSeen: 0);
        var current = Metrics(parsedRowCount: 500, totalRowsSeen: 500);

        var result = FeedHealthEvaluator.Evaluate(current, previous, [], DefaultOptions, Now);

        Assert.DoesNotContain(result.Decisions, d => d.Kind == FeedHealthAlertKind.RowCountCollapse);
    }

    [Fact]
    public void Row_count_growth_is_never_flagged()
    {
        var previous = Snapshot(parsedRowCount: 1000);
        var current = Metrics(parsedRowCount: 1200);

        var result = FeedHealthEvaluator.Evaluate(current, previous, [], DefaultOptions, Now);

        Assert.DoesNotContain(result.Decisions, d => d.Kind == FeedHealthAlertKind.RowCountCollapse);
    }

    // ── Required-field drop-rate spike ──────────────────────────────────────────

    [Fact]
    public void Required_field_drop_rate_unchanged_at_zero_is_not_flagged()
    {
        var previous = Snapshot(totalRowsSeen: 1000, parsedRowCount: 1000);
        var current = Metrics(totalRowsSeen: 1000, parsedRowCount: 1000);

        var result = FeedHealthEvaluator.Evaluate(current, previous, [], DefaultOptions, Now);

        Assert.DoesNotContain(result.Decisions, d => d.Kind == FeedHealthAlertKind.RequiredFieldDropSpike);
    }

    [Fact]
    public void Required_field_tiny_drop_rate_below_floor_is_suppressed_despite_large_multiplier()
    {
        // previous: 1/100000 dropped (0.001%), current: 5/100000 dropped (0.005%) -> 5x "spike"
        // but both are far under RequiredFieldDropRateMinimumPercent (0.5%), so must stay silent.
        var previous = Snapshot(totalRowsSeen: 100_000, parsedRowCount: 99_999);
        var current = Metrics(totalRowsSeen: 100_000, parsedRowCount: 99_995);

        var result = FeedHealthEvaluator.Evaluate(current, previous, [], DefaultOptions, Now);

        Assert.DoesNotContain(result.Decisions, d => d.Kind == FeedHealthAlertKind.RequiredFieldDropSpike);
    }

    [Fact]
    public void Required_field_genuine_spike_from_near_zero_is_Detected()
    {
        var previous = Snapshot(totalRowsSeen: 58_800, parsedRowCount: 58_776); // ~0.04% drop
        var current = Metrics(totalRowsSeen: 58_800, parsedRowCount: 50_000); // ~14.97% drop

        var result = FeedHealthEvaluator.Evaluate(current, previous, [], DefaultOptions, Now);

        var decision = Assert.Single(result.Decisions, d => d.Kind == FeedHealthAlertKind.RequiredFieldDropSpike);
        Assert.Equal(FeedHealthAlertStatus.Detected, decision.Status);
    }

    // ── Optional-field null-rate drift ──────────────────────────────────────────

    [Fact]
    public void Optional_field_small_null_rate_jump_under_threshold_is_not_flagged()
    {
        var previous = Snapshot(nullCounts: new Dictionary<string, int> { ["Address"] = 20 }, parsedRowCount: 1000);
        var current = Metrics(nullCounts: new Dictionary<string, int> { ["Address"] = 60 }, parsedRowCount: 1000); // 2% -> 6%, +4pp

        var result = FeedHealthEvaluator.Evaluate(current, previous, [], DefaultOptions, Now);

        Assert.DoesNotContain(result.Decisions, d => d.Kind == FeedHealthAlertKind.OptionalFieldNullDrift);
    }

    [Fact]
    public void Optional_field_renamed_element_causes_near_total_null_jump_and_is_Detected()
    {
        // Simulates Fødevarestyrelsen renaming <adresse1> -> every row's Address goes null.
        var previous = Snapshot(nullCounts: new Dictionary<string, int> { ["Address"] = 20 }, parsedRowCount: 1000);
        var current = Metrics(nullCounts: new Dictionary<string, int> { ["Address"] = 998 }, parsedRowCount: 1000);

        var result = FeedHealthEvaluator.Evaluate(current, previous, [], DefaultOptions, Now);

        var decision = Assert.Single(result.Decisions, d => d.Kind == FeedHealthAlertKind.OptionalFieldNullDrift);
        Assert.Equal("Address", decision.FieldName);
        Assert.Equal(FeedHealthAlertStatus.Detected, decision.Status);
    }

    [Fact]
    public void Optional_field_drift_is_evaluated_independently_per_field()
    {
        var previous = Snapshot(
            nullCounts: new Dictionary<string, int> { ["Address"] = 20, ["City"] = 20 }, parsedRowCount: 1000);
        var current = Metrics(
            nullCounts: new Dictionary<string, int> { ["Address"] = 950, ["City"] = 25 }, parsedRowCount: 1000);

        var result = FeedHealthEvaluator.Evaluate(current, previous, [], DefaultOptions, Now);

        var driftDecisions = result.Decisions.Where(d => d.Kind == FeedHealthAlertKind.OptionalFieldNullDrift).ToList();
        var addressDecision = Assert.Single(driftDecisions, d => d.FieldName == "Address");
        Assert.Equal(FeedHealthAlertStatus.Detected, addressDecision.Status);
        Assert.DoesNotContain(driftDecisions, d => d.FieldName == "City");
    }

    // ── Multiple simultaneous anomalies ──────────────────────────────────────────

    [Fact]
    public void Multiple_simultaneous_anomalies_each_produce_independent_decisions()
    {
        var previous = Snapshot(
            headerChangeDetectedAt: Now.AddHours(-DefaultOptions.StaleAfterHours),
            totalRowsSeen: 1000, parsedRowCount: 1000,
            nullCounts: new Dictionary<string, int> { ["Address"] = 10 });
        var current = Metrics(
            totalRowsSeen: 1000, parsedRowCount: 700, // both row-count collapse and required-field spike
            nullCounts: new Dictionary<string, int> { ["Address"] = 690 }); // and optional-field drift

        var result = FeedHealthEvaluator.Evaluate(current, previous, [], DefaultOptions, Now);

        var kinds = result.Decisions.Select(d => d.Kind).ToHashSet();
        Assert.Contains(FeedHealthAlertKind.FeedStale, kinds);
        Assert.Contains(FeedHealthAlertKind.RowCountCollapse, kinds);
        Assert.Contains(FeedHealthAlertKind.RequiredFieldDropSpike, kinds);
        Assert.Contains(FeedHealthAlertKind.OptionalFieldNullDrift, kinds);
        Assert.Equal(4, result.Decisions.Count);
    }
}
