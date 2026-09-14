namespace SmilrApi.Core.Models;

/// <summary>
/// Plain, portable summary of one XmlSyncWorker run's feed download, built by XmlSyncWorker
/// (which can see both SmilrApi.Api.Workers.FodevareFeedResult and this Core type) and passed to
/// FeedHealthCheckService in SmilrApi.Infrastructure — which cannot reference SmilrApi.Api types
/// directly, since the dependency direction only flows Api -&gt; Infrastructure -&gt; Core.
/// </summary>
/// <param name="OptionalFieldNullCounts">Tracked field display name -&gt; null count, among
/// ParsedRowCount rows.</param>
public record FeedHealthMetrics(
    string? ETag,
    DateTimeOffset? LastModified,
    int TotalRowsSeen,
    int ParsedRowCount,
    IReadOnlyDictionary<string, int> OptionalFieldNullCounts);
