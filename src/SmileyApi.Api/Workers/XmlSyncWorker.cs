namespace SmileyApi.Api.Workers;

public class XmlSyncWorker(IServiceScopeFactory scopeFactory, ILogger<XmlSyncWorker> logger)
    : BackgroundService
{
    private static readonly SemaphoreSlim Lock = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Phase 1: download XML, parse, diff, upsert
        while (!stoppingToken.IsCancellationRequested)
        {
            if (await Lock.WaitAsync(0, stoppingToken))
            {
                try
                {
                    logger.LogInformation("XmlSyncWorker: sync not yet implemented");
                }
                finally
                {
                    Lock.Release();
                }
            }
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
