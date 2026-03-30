using McpLumina.Configuration;
using McpLumina.Services;
using McpLumina.Tools;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace McpLumina.Tests.Integration;

/// <summary>
/// Base class for integration tests that require a real FFXIV installation.
///
/// Gate: integration tests only run when the FFXIV_GAME_PATH environment variable is set.
/// If absent, all tests in derived classes are skipped — they do not fail CI.
///
/// Usage:
///   export FFXIV_GAME_PATH="C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn"
///   dotnet test --filter Category=Integration
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    protected static readonly string? GamePath =
        Environment.GetEnvironmentVariable("FFXIV_GAME_PATH");

    protected static bool ShouldSkip => string.IsNullOrWhiteSpace(GamePath);

    protected GameDataService GameData { get; }
    protected FfxivTools      Tools    { get; }

    protected IntegrationTestBase()
    {
        if (ShouldSkip)
        {
            GameData = null!;
            Tools    = null!;
            return;
        }

        var config = new ServerConfig
        {
            GamePath        = GamePath!,
            LanguageDefault = "en",
            CacheEnabled    = false,  // No cache in tests for isolation
            CacheTTLSeconds = 0,
        };

        var options = Options.Create(config);
        var schema  = new SchemaService(options, NullLogger<SchemaService>.Instance);
        var logger  = NullLogger<GameDataService>.Instance;

        GameData = new GameDataService(options, schema, logger);

        var memCache = new MemoryCache(new MemoryCacheOptions());
        var cache    = new ResponseCacheService(memCache, options);
        Tools = new FfxivTools(GameData, cache);
    }

    protected static void SkipIfNoGamePath()
    {
        // xunit.skippablefact: throwing Skip.FromException skips the test cleanly.
        // All integration test methods must be decorated with [SkippableFact] or [SkippableTheory].
        Skip.If(ShouldSkip,
            "Integration test skipped: FFXIV_GAME_PATH environment variable is not set. " +
            "Set it to your FFXIV installation path to run integration tests.");
    }

    public void Dispose() => GameData?.Dispose();
}
