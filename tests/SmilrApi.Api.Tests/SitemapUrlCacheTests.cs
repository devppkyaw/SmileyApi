using Microsoft.Extensions.Logging.Abstractions;
using SmilrApi.Api.Endpoints;
using SmilrApi.Api.Rendering;

namespace SmilrApi.Api.Tests;

/// <summary>
/// The sitemap list is slow to build on prod, and crawlers hang up mid-build. These pin down that a
/// hang-up never throws the build away, that a built list is always served without waiting, and that a
/// failed rebuild keeps the previous list.
/// </summary>
public class SitemapUrlCacheTests
{
    private static readonly IReadOnlyList<FindPageRenderer.SitemapUrl> ListA = [new("/find/a-1", null)];
    private static readonly IReadOnlyList<FindPageRenderer.SitemapUrl> ListB = [new("/find/b-2", null)];

    private static SitemapUrlCache Cache(
        Func<CancellationToken, Task<IReadOnlyList<FindPageRenderer.SitemapUrl>>> build, TimeSpan? ttl = null) =>
        new(build, CancellationToken.None, ttl ?? TimeSpan.FromHours(12), NullLogger.Instance);

    [Fact]
    public async Task A_request_that_gives_up_does_not_cancel_the_build()
    {
        var release = new TaskCompletionSource();
        var builds = 0;
        var cache = Cache(async _ =>
        {
            Interlocked.Increment(ref builds);
            await release.Task;
            return ListA;
        });

        using var hangUp = new CancellationTokenSource();
        var first = cache.GetAsync(hangUp.Token);
        hangUp.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);

        release.SetResult();
        Assert.Same(ListA, await cache.GetAsync(CancellationToken.None));
        Assert.Equal(1, builds);
    }

    [Fact]
    public async Task Concurrent_cold_requests_share_one_build()
    {
        var release = new TaskCompletionSource();
        var builds = 0;
        var cache = Cache(async _ =>
        {
            Interlocked.Increment(ref builds);
            await release.Task;
            return ListA;
        });

        var requests = Enumerable.Range(0, 5).Select(_ => cache.GetAsync(CancellationToken.None)).ToList();
        release.SetResult();
        await Task.WhenAll(requests);

        Assert.Equal(1, builds);
    }

    [Fact]
    public async Task A_stale_list_is_served_immediately_while_it_rebuilds()
    {
        var second = new TaskCompletionSource<IReadOnlyList<FindPageRenderer.SitemapUrl>>();
        var calls = 0;
        var cache = Cache(_ => Interlocked.Increment(ref calls) == 1 ? Task.FromResult(ListA) : second.Task,
            ttl: TimeSpan.Zero);

        Assert.Same(ListA, await cache.GetAsync(CancellationToken.None));
        Assert.Same(ListA, await cache.GetAsync(CancellationToken.None)); // stale: rebuild starts, not awaited

        second.SetResult(ListB);
        await cache.Refresh(); // joins the in-flight rebuild
        Assert.Same(ListB, await cache.GetAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_failed_rebuild_keeps_serving_the_previous_list()
    {
        var calls = 0;
        var cache = Cache(_ => Interlocked.Increment(ref calls) == 1
                ? Task.FromResult(ListA)
                : Task.FromException<IReadOnlyList<FindPageRenderer.SitemapUrl>>(new TimeoutException()),
            ttl: TimeSpan.Zero);

        await cache.GetAsync(CancellationToken.None);
        await cache.Refresh();

        Assert.Same(ListA, await cache.GetAsync(CancellationToken.None));
    }
}
