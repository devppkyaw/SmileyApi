namespace SmilrApi.Core.Models;

/// <summary>The four things FeedHealthEvaluator watches for in each XmlSyncWorker run. See
/// FeedHealthAnomaly for how each kind's alert cadence is tracked.</summary>
public enum FeedHealthAlertKind
{
    /// <summary>The feed's Last-Modified/ETag hasn't changed in more than the configured
    /// threshold — Fødevarestyrelsen isn't regenerating it (the 2026-09-08 incident).</summary>
    FeedStale,

    /// <summary>Parsed row count dropped sharply vs. the last run.</summary>
    RowCountCollapse,

    /// <summary>The rate of rows silently dropped for missing navnelbnr/navn1 spiked vs. the
    /// last run — how a renamed required field would manifest.</summary>
    RequiredFieldDropSpike,

    /// <summary>A tracked optional field's null-rate jumped sharply vs. the last run — how a
    /// renamed optional field would manifest (currently invisible otherwise).</summary>
    OptionalFieldNullDrift,
}

/// <summary>
/// One row per anomaly kind (plus field name, for OptionalFieldNullDrift — one row per tracked
/// field). Rows are upserted lazily on first detection; there's no seed migration. Carries the
/// alert-once -&gt; weekly-reminder-while-unresolved -&gt; recovery-email cadence state for its
/// kind/field, applied by FeedHealthEvaluator.
/// </summary>
public class FeedHealthAnomaly
{
    public int Id { get; set; }
    public FeedHealthAlertKind Kind { get; set; }

    /// <summary>Null for every kind except OptionalFieldNullDrift.</summary>
    public string? FieldName { get; set; }

    public bool IsActive { get; set; }

    /// <summary>When this occurrence was first detected. Cleared to null on recovery, so a
    /// future recurrence of the same kind/field reads as a fresh detection, not a stale
    /// reminder clock.</summary>
    public DateTime? FirstDetectedAt { get; set; }

    /// <summary>When an email was last sent for this occurrence. Gates the weekly
    /// "still unresolved" reminder.</summary>
    public DateTime? LastAlertedAt { get; set; }

    /// <summary>Human-readable snapshot of the metric as of the most recent run, reused in
    /// reminder/recovery email bodies without recomputation.</summary>
    public string? LastObservedSummary { get; set; }
}
