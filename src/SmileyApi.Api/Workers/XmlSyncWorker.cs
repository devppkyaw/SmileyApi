using SmileyApi.Infrastructure.Services;

namespace SmileyApi.Api.Workers;

public class XmlSyncWorker(
    IServiceScopeFactory scopeFactory,
    FodevareXmlParser parser,
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
        logger.LogInformation("XmlSyncWorker: starting sync.");

        var rows = await parser.ParseAsync(ct);

        // Map parser DTOs to the service contract
        var syncRows = rows.Select(r => new SyncRow(
            r.Navnelbnr, r.CvrNumber, r.Name, r.Address, r.PostalCode,
            r.City, r.IndustryCode, r.IndustryName, r.GeoLat, r.GeoLng,
            r.ReportUrl, r.Inspections)).ToList();

        using var scope = scopeFactory.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<EstablishmentSyncService>();
        await syncService.SyncAsync(syncRows, ct);
    }
}
