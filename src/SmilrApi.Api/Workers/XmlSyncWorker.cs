using Microsoft.EntityFrameworkCore;
using SmilrApi.Core.Models;
using SmilrApi.Infrastructure.Data;
using SmilrApi.Infrastructure.Services;

namespace SmilrApi.Api.Workers;

public class XmlSyncWorker(
    IServiceScopeFactory scopeFactory,
    FodevareXmlParser parser,
    IWebHostEnvironment env,
    ILogger<XmlSyncWorker> logger) : BackgroundService
{
    internal static readonly SemaphoreSlim Lock = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (await Lock.WaitAsync(0, stoppingToken))
            {
                try
                {
                    await RunSyncAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "XmlSyncWorker: sync failed.");
                }
                finally
                {
                    Lock.Release();
                }
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task RunSyncAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmilrDbContext>();

        // In non-dev, skip if data is fresh — avoids full XML download + MERGE on every container restart.
        if (!env.IsDevelopment() && await db.Establishments.AnyAsync(ct))
        {
            var lastSync = await db.Establishments.MaxAsync(e => e.UpdatedAt, ct);
            if (lastSync > DateTime.UtcNow.AddHours(-20))
            {
                logger.LogInformation("XmlSyncWorker: data is fresh (last synced {LastSync:u}), skipping.", lastSync);
                return;
            }
        }

        logger.LogInformation("XmlSyncWorker: starting sync.");

        var feedResult = await parser.ParseAsync(ct);

        // Wrapped separately so a bug in this new-ish feature can never abort the real sync below —
        // same defensive posture EstablishmentSyncService.SendScoreAlertsAsync already uses for its
        // own emails. Runs even when feedResult.Rows.Count == 0: a total collapse is the most
        // important case to catch.
        try
        {
            var healthSvc = scope.ServiceProvider.GetRequiredService<FeedHealthCheckService>();
            await healthSvc.CheckAndAlertAsync(BuildHealthMetrics(feedResult), ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "XmlSyncWorker: feed health check failed.");
        }

        var syncRows = feedResult.Rows.Select(r => new SyncRow(
            r.Navnelbnr, r.CvrNumber, r.Name, r.Address, r.PostalCode,
            r.City, r.IndustryCode, r.IndustryName, r.GeoLat, r.GeoLng,
            r.ReportUrl, r.VirksomhedsType, r.Pixibranche, r.LatestScoreDate, r.PNumber,
            r.Inspections)).ToList();

        var syncService = scope.ServiceProvider.GetRequiredService<EstablishmentSyncService>();
        var scoreChanges = await syncService.SyncAsync(syncRows, ct);

        if (scoreChanges.Count > 0)
        {
            var webhookSvc = scope.ServiceProvider.GetRequiredService<WebhookService>();
            var webhookChanges = scoreChanges
                .Select(c => new WebhookScoreChange(c.EstablishmentId, c.OldScore, c.NewScore))
                .ToList();
            await webhookSvc.EnqueueDeliveriesAsync(webhookChanges, ct);
        }
    }

    // Tracked fields are hardcoded (paired 1:1 with EstablishmentSyncRow properties) rather than
    // config-driven, and chosen because they're normally near-always populated in the real feed —
    // GeoLat/GeoLng are deliberately excluded here since they're legitimately sparse today and
    // would cause false positives. Internal (not private) so AdminEndpoints' manual /admin/sync
    // trigger — which duplicates this worker's sync logic for on-demand use — can reuse it too.
    internal static FeedHealthMetrics BuildHealthMetrics(FodevareFeedResult result)
    {
        var rows = result.Rows;
        var nullCounts = new Dictionary<string, int>
        {
            ["Address"] = rows.Count(r => r.Address is null),
            ["PostalCode"] = rows.Count(r => r.PostalCode is null),
            ["City"] = rows.Count(r => r.City is null),
            ["LatestScoreDate"] = rows.Count(r => r.LatestScoreDate is null),
        };

        return new FeedHealthMetrics(result.ETag, result.LastModified, result.TotalRowsSeen, rows.Count, nullCounts);
    }
}
