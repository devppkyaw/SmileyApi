using SmilrApi.Api.Rendering;
using SmilrApi.Core.Interfaces;

namespace SmilrApi.Api.Endpoints;

/// <summary>
/// Holds the last successfully built sitemap URL list for /find/sitemap.xml and its chunk files.
///
/// Building the list takes three catalogue-wide queries (see FindEndpoints.BuildSitemapUrlsAsync), which
/// on the 5-DTU prod database can take 10-45s. It used to be built inside IMemoryCache.GetOrCreateAsync on
/// the request's CancellationToken: Googlebot hung up first (HTTP 499), the cancelled build cached
/// nothing, and the next fetch started over from zero. Many days most sitemap fetches failed that way, and
/// on 2026-10-24 Search Console dropped ~54k discovered-but-not-indexed URLs it had only learned about
/// from the sitemap.
///
/// So the build never runs on a request token: it runs on the app's own stopping token, one at a time,
/// and a request only waits for it (and may give up waiting) when there is no list yet at all. Once a list
/// exists it is always served immediately; a stale one triggers a background rebuild, and a failed
/// rebuild keeps serving the previous list.
/// </summary>
internal sealed class SitemapUrlCache
{
    private readonly Func<CancellationToken, Task<IReadOnlyList<FindPageRenderer.SitemapUrl>>> _build;
    private readonly CancellationToken _stopping;
    private readonly TimeSpan _ttl;
    private readonly ILogger _logger;

    private readonly object _gate = new();
    private IReadOnlyList<FindPageRenderer.SitemapUrl>? _urls;
    private DateTime _builtAt;
    private Task<IReadOnlyList<FindPageRenderer.SitemapUrl>?>? _building;

    internal SitemapUrlCache(
        Func<CancellationToken, Task<IReadOnlyList<FindPageRenderer.SitemapUrl>>> build,
        CancellationToken stopping, TimeSpan ttl, ILogger logger)
    {
        _build = build;
        _stopping = stopping;
        _ttl = ttl;
        _logger = logger;
    }

    /// <summary>DI factory: each build gets its own scope (the repository is scoped).</summary>
    public static SitemapUrlCache Create(IServiceProvider sp)
    {
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        return new SitemapUrlCache(
            async ct =>
            {
                using var scope = scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IEstablishmentRepository>();
                return await FindEndpoints.BuildSitemapUrlsAsync(repo, ct);
            },
            sp.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping,
            FindEndpoints.CacheTtl,
            sp.GetRequiredService<ILogger<SitemapUrlCache>>());
    }

    public async Task<IReadOnlyList<FindPageRenderer.SitemapUrl>> GetAsync(CancellationToken requestCt)
    {
        Task<IReadOnlyList<FindPageRenderer.SitemapUrl>?> building;
        lock (_gate)
        {
            if (_urls is not null)
            {
                if (DateTime.UtcNow - _builtAt > _ttl)
                    StartBuildLocked();
                return _urls;
            }
            building = StartBuildLocked();
        }

        // Cold start only (no list built yet). Giving up here abandons the wait, not the build.
        return await building.WaitAsync(requestCt)
               ?? throw new InvalidOperationException("Sitemap URL list could not be built.");
    }

    /// <summary>Starts a build if none is running — used to warm the cache at startup.</summary>
    public Task Refresh()
    {
        lock (_gate)
            return StartBuildLocked();
    }

    // Caller holds _gate. BuildAsync's own bookkeeping also takes _gate, so it can't finish (and clear
    // _building) before this assignment lands.
    private Task<IReadOnlyList<FindPageRenderer.SitemapUrl>?> StartBuildLocked() =>
        _building ??= Task.Run(BuildAsync);

    private async Task<IReadOnlyList<FindPageRenderer.SitemapUrl>?> BuildAsync()
    {
        try
        {
            var started = DateTime.UtcNow;
            var urls = await _build(_stopping);

            lock (_gate)
            {
                _urls = urls;
                _builtAt = DateTime.UtcNow;
            }
            _logger.LogInformation("SitemapUrlCache: built {Count} sitemap URLs in {Seconds:F1}s.",
                urls.Count, (DateTime.UtcNow - started).TotalSeconds);
            return urls;
        }
        catch (Exception ex)
        {
            if (!_stopping.IsCancellationRequested)
                _logger.LogError(ex, "SitemapUrlCache: build failed; keeping the previous sitemap list (if any).");
            lock (_gate)
                return _urls;
        }
        finally
        {
            lock (_gate)
                _building = null;
        }
    }
}

/// <summary>Builds the sitemap list once at startup so the first crawler fetch is served from memory.</summary>
internal sealed class SitemapWarmupService(SitemapUrlCache cache) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = cache.Refresh();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
