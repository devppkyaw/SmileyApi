namespace SmilrApi.Core.Models;

/// <summary>Bound from the "FeedHealth" config section. Thresholds for FeedHealthEvaluator.</summary>
public class FeedHealthOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Alert when the feed's Last-Modified/ETag hasn't changed in this many hours.</summary>
    public double StaleAfterHours { get; set; } = 48;

    /// <summary>Alert when parsed row count drops by at least this many percent vs. the last run.</summary>
    public double RowCountDropPercentThreshold { get; set; } = 20;

    /// <summary>Floor below which a required-field drop-rate spike is ignored as noise, even if
    /// it technically clears the multiplier below (e.g. 0.01% -&gt; 0.05% is a "5x spike" but
    /// meaningless in absolute terms).</summary>
    public double RequiredFieldDropRateMinimumPercent { get; set; } = 0.5;

    /// <summary>Alert when the current required-field drop rate is at least this many times the
    /// previous run's rate (and clears the minimum-percent floor above).</summary>
    public double RequiredFieldDropRateSpikeMultiplier { get; set; } = 3.0;

    /// <summary>Alert when a tracked optional field's null-rate jumps by at least this many
    /// percentage points vs. the last run. A renamed field jumps ~0%-&gt;100% instantly, so this
    /// is set high enough to ignore ordinary day-to-day data noise.</summary>
    public double OptionalFieldNullRateJumpPercentagePoints { get; set; } = 10;

    /// <summary>While an anomaly stays active, re-alert at most this often.</summary>
    public int ReminderIntervalDays { get; set; } = 7;
}
