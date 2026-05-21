using Microsoft.EntityFrameworkCore;
using SmilrApi.Infrastructure.Data;
using SmilrApi.Infrastructure.Services;

namespace SmilrApi.Api.Workers;

public class XmlSyncWorker(
    IServiceScopeFactory scopeFactory,
    FodevareXmlParser parser,
    IWebHostEnvironment env,
    ILogger<XmlSyncWorker> logger) : BackgroundService
{
    private static readonly SemaphoreSlim Lock = new(1, 1);

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
        var db = scope.ServiceProvider.GetRequiredService<SmileyDbContext>();

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

        var rows = await parser.ParseAsync(ct);

        var syncRows = rows.Select(r => new SyncRow(
            r.Navnelbnr, r.CvrNumber, r.Name, r.Address, r.PostalCode,
            r.City, r.IndustryCode, r.IndustryName, r.GeoLat, r.GeoLng,
            r.ReportUrl, r.Inspections)).ToList();

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
}
