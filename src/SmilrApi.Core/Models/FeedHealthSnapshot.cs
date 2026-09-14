using System.Text.Json;

namespace SmilrApi.Core.Models;

/// <summary>
/// Singleton row (one per deployment) recording the last successful XmlSyncWorker run's raw
/// feed metrics, so FeedHealthEvaluator can compute day-over-day deltas. See FeedHealthAnomaly
/// for the per-check alert-cadence state this snapshot is compared against.
/// </summary>
public class FeedHealthSnapshot
{
    public int Id { get; set; }

    public string? LastEtag { get; set; }
    public DateTimeOffset? LastModifiedHeader { get; set; }

    /// <summary>
    /// The last time LastEtag/LastModifiedHeader were actually observed to change — NOT the last
    /// time the worker ran. This is what "feed hasn't updated in Xh" is measured against; if it
    /// tracked run time instead, staleness could never be detected since the worker runs
    /// regardless of whether the upstream feed changed.
    /// </summary>
    public DateTime LastHeaderChangeDetectedAt { get; set; }

    public DateTime LastRunAt { get; set; }

    /// <summary>Total &lt;row&gt; elements encountered in the feed, including ones dropped for
    /// missing navnelbnr/navn1.</summary>
    public int LastTotalRowsSeen { get; set; }

    /// <summary>Rows that survived required-field parsing.</summary>
    public int LastParsedRowCount { get; set; }

    /// <summary>JSON-serialized tracked-field-name -&gt; null-count map, among LastParsedRowCount
    /// rows. A blob rather than a separate per-field table: the tracked field list is small,
    /// always read/written as a unit, and never queried by field name in SQL — adding or removing
    /// a tracked field is then a pure C# change with no migration.</summary>
    public string FieldNullCountsJson { get; set; } = "{}";

    public IReadOnlyDictionary<string, int> GetFieldNullCounts() =>
        JsonSerializer.Deserialize<Dictionary<string, int>>(FieldNullCountsJson) ?? new Dictionary<string, int>();

    public void SetFieldNullCounts(IReadOnlyDictionary<string, int> counts) =>
        FieldNullCountsJson = JsonSerializer.Serialize(counts);
}
