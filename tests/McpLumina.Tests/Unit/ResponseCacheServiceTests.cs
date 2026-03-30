using McpLumina.Configuration;
using McpLumina.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace McpLumina.Tests.Unit;

public sealed class ResponseCacheServiceTests : IDisposable
{
    private readonly MemoryCache _memCache;

    public ResponseCacheServiceTests()
    {
        _memCache = new MemoryCache(new MemoryCacheOptions());
    }

    public void Dispose() => _memCache.Dispose();

    private ResponseCacheService MakeService(bool cacheEnabled, int ttlSeconds = 60)
    {
        var config  = new ServerConfig { CacheEnabled = cacheEnabled, CacheTTLSeconds = ttlSeconds };
        var options = Options.Create(config);
        return new ResponseCacheService(_memCache, options);
    }

    // ── GetOrCreate (sync) ─────────────────────────────────────────────────

    [Fact]
    public void GetOrCreate_CacheDisabled_CallsFactoryEveryTime()
    {
        var svc      = MakeService(cacheEnabled: false);
        int callCount = 0;

        svc.GetOrCreate("key", () => { callCount++; return "value"; });
        svc.GetOrCreate("key", () => { callCount++; return "value"; });

        Assert.Equal(2, callCount);
    }

    [Fact]
    public void GetOrCreate_CacheEnabled_CallsFactoryOnlyOnce()
    {
        var svc       = MakeService(cacheEnabled: true);
        int callCount = 0;

        var r1 = svc.GetOrCreate("key", () => { callCount++; return "value"; });
        var r2 = svc.GetOrCreate("key", () => { callCount++; return "value"; });

        Assert.Equal(1, callCount);
        Assert.Equal(r1, r2);
    }

    [Fact]
    public void GetOrCreate_CacheEnabled_ReturnsCachedInstance()
    {
        var svc     = MakeService(cacheEnabled: true);
        var obj     = new object();

        var r1 = svc.GetOrCreate("obj-key", () => obj);
        var r2 = svc.GetOrCreate("obj-key", () => new object()); // different factory

        Assert.Same(obj, r1);
        Assert.Same(r1, r2);
    }

    [Fact]
    public void GetOrCreate_CacheDisabled_ReturnsFactoryResult()
    {
        var svc    = MakeService(cacheEnabled: false);
        var result = svc.GetOrCreate("key", () => 42);

        Assert.Equal(42, result);
    }

    // ── GetOrCreateAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetOrCreateAsync_CacheDisabled_CallsFactoryEveryTime()
    {
        var svc       = MakeService(cacheEnabled: false);
        int callCount = 0;

        await svc.GetOrCreateAsync("async-key", () => { callCount++; return Task.FromResult("v"); });
        await svc.GetOrCreateAsync("async-key", () => { callCount++; return Task.FromResult("v"); });

        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task GetOrCreateAsync_CacheEnabled_CallsFactoryOnlyOnce()
    {
        var svc       = MakeService(cacheEnabled: true);
        int callCount = 0;

        var r1 = await svc.GetOrCreateAsync("async-key", () => { callCount++; return Task.FromResult("result"); });
        var r2 = await svc.GetOrCreateAsync("async-key", () => { callCount++; return Task.FromResult("result"); });

        Assert.Equal(1, callCount);
        Assert.Equal(r1, r2);
    }

    [Fact]
    public async Task GetOrCreateAsync_CacheDisabled_ReturnsFactoryResult()
    {
        var svc    = MakeService(cacheEnabled: false);
        var result = await svc.GetOrCreateAsync("k", () => Task.FromResult(99));

        Assert.Equal(99, result);
    }

    // ── Invalidate ────────────────────────────────────────────────────────

    [Fact]
    public void Invalidate_RemovesCachedEntry()
    {
        var svc       = MakeService(cacheEnabled: true);
        int callCount = 0;

        svc.GetOrCreate("inv-key", () => { callCount++; return "v"; });
        Assert.Equal(1, callCount);

        svc.Invalidate("inv-key");

        svc.GetOrCreate("inv-key", () => { callCount++; return "v"; });
        Assert.Equal(2, callCount);  // factory called again after invalidation
    }

    [Fact]
    public void Invalidate_NonExistentKey_DoesNotThrow()
    {
        var svc = MakeService(cacheEnabled: true);
        var ex  = Record.Exception(() => svc.Invalidate("no-such-key"));
        Assert.Null(ex);
    }

    [Fact]
    public void Invalidate_OnlyRemovesSpecifiedKey()
    {
        var svc        = MakeService(cacheEnabled: true);
        int callCountA = 0;
        int callCountB = 0;

        svc.GetOrCreate("a", () => { callCountA++; return "a"; });
        svc.GetOrCreate("b", () => { callCountB++; return "b"; });

        svc.Invalidate("a");

        svc.GetOrCreate("a", () => { callCountA++; return "a"; });
        svc.GetOrCreate("b", () => { callCountB++; return "b"; }); // should stay cached

        Assert.Equal(2, callCountA); // a: fetched twice (once invalidated)
        Assert.Equal(1, callCountB); // b: fetched once (still cached)
    }
}
