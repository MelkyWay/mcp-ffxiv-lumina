using McpLumina.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace McpLumina.Services;

/// <summary>
/// Thin wrapper over IMemoryCache providing keyed response caching with configurable TTL.
/// Disabled cleanly when CacheEnabled = false.
/// </summary>
public sealed class ResponseCacheService(IMemoryCache cache, IOptions<ServerConfig> options)
{
    private readonly ServerConfig _config = options.Value;

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory)
    {
        if (!_config.CacheEnabled)
            return await factory();

        return (await cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_config.CacheTTLSeconds);
            return await factory();
        }))!;
    }

    public T GetOrCreate<T>(string key, Func<T> factory)
    {
        if (!_config.CacheEnabled)
            return factory();

        return cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_config.CacheTTLSeconds);
            return factory();
        })!;
    }

    public void Invalidate(string key) => cache.Remove(key);

    public void InvalidateAll()
    {
        if (cache is MemoryCache mc)
            mc.Clear();
    }
}
