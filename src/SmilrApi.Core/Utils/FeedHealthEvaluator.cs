using SmilrApi.Core.Models;

namespace SmilrApi.Core.Utils;

public enum FeedHealthAlertStatus { Detected, StillUnresolved, Recovered }

/// <summary>One anomaly kind/field that needs an email sent for it this run.</summary>
public record FeedHealthDecision(
    FeedHealthAlertKind Kind,
    string? FieldName,
    FeedHealthAlertStatus Status,
    string Summary,
    DateTime? FirstDetectedAt);

/// <summary>Everything FeedHealthCheckService needs to persist and send after one run.</summary>
public record FeedHealthEvaluation(
    FeedHealthSnapshot UpdatedSnapshot,
    IReadOnlyList<FeedHealthAnomaly> UpdatedAnomalies,
    IReadOnlyList<FeedHealthDecision> Decisions);

/// <summary>
/// Derives this run's feed-health anomalies purely from plain data — no EF/HTTP/email — mirroring
/// ScoreChangeCalculator's "pure function over plain entities" style so it's cleanly unit-testable.
/// Compares only the current run against the immediately previous one (a day-over-day delta); a
/// slow multi-day drift under any single day's threshold would not trip. See FeedHealthAnomaly's
/// doc comments for what each kind means and FeedHealthOptions for the thresholds.
/// </summary>
public static class FeedHealthEvaluator
{
    public static FeedHealthEvaluation Evaluate(
        FeedHealthMetrics current,
        FeedHealthSnapshot? previous,
        IReadOnlyList<FeedHealthAnomaly> priorAnomalies,
        FeedHealthOptions options,
        DateTime now)
    {
        var newSnapshot = new FeedHealthSnapshot
        {
            Id = previous?.Id ?? 0,
            LastEtag = current.ETag,
            LastModifiedHeader = current.LastModified,
            LastRunAt = now,
            LastTotalRowsSeen = current.TotalRowsSeen,
            LastParsedRowCount = current.ParsedRowCount,
        };
        newSnapshot.SetFieldNullCounts(current.OptionalFieldNullCounts);

        // First-ever run: nothing to compare against, so no anomalies are possible yet. Still
        // record this run's metrics as the baseline for next time.
        if (previous is null)
        {
            newSnapshot.LastHeaderChangeDetectedAt = now;
            return new FeedHealthEvaluation(newSnapshot, priorAnomalies, Array.Empty<FeedHealthDecision>());
        }

        var updatedAnomalies = new List<FeedHealthAnomaly>();
        var decisions = new List<FeedHealthDecision>();

        void Check(FeedHealthAlertKind kind, string? fieldName, bool conditionTrue, string summary)
        {
            var anomaly = CloneOrDefault(priorAnomalies, kind, fieldName);
            var decision = ApplyCadence(anomaly, conditionTrue, summary, options, now);
            updatedAnomalies.Add(anomaly);
            if (decision is not null)
                decisions.Add(decision);
        }

        // 1. Feed staleness — carry the "last genuinely changed" clock forward unless the header
        // actually differs from last run.
        var headerSame = current.ETag == previous.LastEtag && current.LastModified == previous.LastModifiedHeader;
        var headerChangeAt = headerSame ? previous.LastHeaderChangeDetectedAt : now;
        newSnapshot.LastHeaderChangeDetectedAt = headerChangeAt;
        var staleHours = (now - headerChangeAt).TotalHours;
        Check(FeedHealthAlertKind.FeedStale, null,
            staleHours >= options.StaleAfterHours,
            $"Feed's Last-Modified/ETag hasn't changed in {staleHours:F0}h (threshold {options.StaleAfterHours:F0}h). " +
            $"Last genuine update detected: {headerChangeAt:u}.");

        // 2. Row-count collapse.
        if (previous.LastParsedRowCount > 0)
        {
            var dropPct = (previous.LastParsedRowCount - current.ParsedRowCount) / (double)previous.LastParsedRowCount * 100;
            Check(FeedHealthAlertKind.RowCountCollapse, null,
                dropPct >= options.RowCountDropPercentThreshold,
                $"Parsed row count dropped from {previous.LastParsedRowCount} to {current.ParsedRowCount} " +
                $"({dropPct:F1}% drop, threshold {options.RowCountDropPercentThreshold:F0}%).");
        }

        // 3. Required-field (navnelbnr/navn1) drop-rate spike.
        {
            var currentDropRate = current.TotalRowsSeen > 0
                ? (current.TotalRowsSeen - current.ParsedRowCount) / (double)current.TotalRowsSeen * 100 : 0;
            var previousDropRate = previous.LastTotalRowsSeen > 0
                ? (previous.LastTotalRowsSeen - previous.LastParsedRowCount) / (double)previous.LastTotalRowsSeen * 100 : 0;
            var spiked = currentDropRate >= options.RequiredFieldDropRateMinimumPercent
                         && currentDropRate >= previousDropRate * options.RequiredFieldDropRateSpikeMultiplier;
            Check(FeedHealthAlertKind.RequiredFieldDropSpike, null, spiked,
                $"Required-field (navnelbnr/navn1) drop rate is {currentDropRate:F2}% " +
                $"({current.TotalRowsSeen - current.ParsedRowCount}/{current.TotalRowsSeen} rows), " +
                $"up from {previousDropRate:F2}% previously.");
        }

        // 4. Optional-field null-rate drift, evaluated independently per tracked field.
        var previousNullCounts = previous.GetFieldNullCounts();
        foreach (var (field, nullCount) in current.OptionalFieldNullCounts)
        {
            var currentNullPct = current.ParsedRowCount > 0 ? nullCount / (double)current.ParsedRowCount * 100 : 0;
            var previousNullPct = previous.LastParsedRowCount > 0 && previousNullCounts.TryGetValue(field, out var prevCount)
                ? prevCount / (double)previous.LastParsedRowCount * 100 : 0;
            var jump = currentNullPct - previousNullPct;
            Check(FeedHealthAlertKind.OptionalFieldNullDrift, field,
                jump >= options.OptionalFieldNullRateJumpPercentagePoints,
                $"Field '{field}' null-rate jumped from {previousNullPct:F1}% to {currentNullPct:F1}% " +
                $"(+{jump:F1}pp, threshold {options.OptionalFieldNullRateJumpPercentagePoints:F0}pp) — " +
                "possible upstream field rename.");
        }

        return new FeedHealthEvaluation(newSnapshot, updatedAnomalies, decisions);
    }

    private static FeedHealthAnomaly CloneOrDefault(
        IReadOnlyList<FeedHealthAnomaly> anomalies, FeedHealthAlertKind kind, string? fieldName)
    {
        var existing = anomalies.FirstOrDefault(a => a.Kind == kind && a.FieldName == fieldName);
        return new FeedHealthAnomaly
        {
            Id = existing?.Id ?? 0,
            Kind = kind,
            FieldName = fieldName,
            IsActive = existing?.IsActive ?? false,
            FirstDetectedAt = existing?.FirstDetectedAt,
            LastAlertedAt = existing?.LastAlertedAt,
            LastObservedSummary = existing?.LastObservedSummary,
        };
    }

    /// <summary>Applies the alert-once -&gt; weekly-reminder-while-unresolved -&gt; recovery
    /// cadence to a single anomaly row in place, returning the email decision (if any).</summary>
    private static FeedHealthDecision? ApplyCadence(
        FeedHealthAnomaly anomaly, bool conditionTrue, string summary, FeedHealthOptions options, DateTime now)
    {
        anomaly.LastObservedSummary = summary;

        if (!conditionTrue)
        {
            if (!anomaly.IsActive)
                return null;

            var firstDetectedAt = anomaly.FirstDetectedAt;
            anomaly.IsActive = false;
            anomaly.FirstDetectedAt = null;
            anomaly.LastAlertedAt = null;
            return new FeedHealthDecision(anomaly.Kind, anomaly.FieldName, FeedHealthAlertStatus.Recovered, summary, firstDetectedAt);
        }

        if (!anomaly.IsActive)
        {
            anomaly.IsActive = true;
            anomaly.FirstDetectedAt = now;
            anomaly.LastAlertedAt = now;
            return new FeedHealthDecision(anomaly.Kind, anomaly.FieldName, FeedHealthAlertStatus.Detected, summary, now);
        }

        var daysSinceAlert = anomaly.LastAlertedAt is { } lastAlerted ? (now - lastAlerted).TotalDays : double.MaxValue;
        if (daysSinceAlert >= options.ReminderIntervalDays)
        {
            anomaly.LastAlertedAt = now;
            return new FeedHealthDecision(anomaly.Kind, anomaly.FieldName, FeedHealthAlertStatus.StillUnresolved, summary, anomaly.FirstDetectedAt);
        }

        return null; // still active, but within the reminder window — stay silent
    }
}
