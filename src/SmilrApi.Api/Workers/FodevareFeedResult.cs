namespace SmilrApi.Api.Workers;

/// <summary>
/// FodevareXmlParser.ParseAsync's full result: the successfully-parsed rows plus enough about
/// the download itself (response headers, total &lt;row&gt; elements seen before required-field
/// filtering) for XmlSyncWorker to build a SmilrApi.Core.Models.FeedHealthMetrics for
/// FeedHealthCheckService.
/// </summary>
public record FodevareFeedResult(
    IReadOnlyList<EstablishmentSyncRow> Rows,
    int TotalRowsSeen,
    string? ETag,
    DateTimeOffset? LastModified);
