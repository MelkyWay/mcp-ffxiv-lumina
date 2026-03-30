using McpLumina.Configuration;
using McpLumina.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace McpLumina.Tests.Unit;

public sealed class ConfigValidatorTests
{
    private static ConfigValidator MakeValidator() =>
        new(NullLogger<ConfigValidator>.Instance);

    /// <summary>Creates a temp directory that looks like an FFXIV root (has game/ffxivgame.ver).</summary>
    private static string CreateFakeFfxivRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(root, "game"));
        File.WriteAllText(Path.Combine(root, "game", "ffxivgame.ver"), "2024.01.01.0000.0000");
        return root;
    }

    private static ServerConfig ValidConfig(string gamePath) => new()
    {
        GamePath        = gamePath,
        LanguageDefault = "en",
        CacheEnabled    = false,
        CacheTTLSeconds = 60,
    };

    // ── gamePath validations ──────────────────────────────────────────────

    [Fact]
    public void ValidateOrThrow_EmptyGamePath_Throws()
    {
        var config    = new ServerConfig { GamePath = "", LanguageDefault = "en", CacheTTLSeconds = 60 };
        var validator = MakeValidator();

        var ex = Assert.Throws<ConfigException>(() => validator.ValidateOrThrow(config));
        Assert.Contains("gamePath is required", ex.Message);
    }

    [Fact]
    public void ValidateOrThrow_WhitespaceGamePath_Throws()
    {
        var config    = new ServerConfig { GamePath = "   ", LanguageDefault = "en", CacheTTLSeconds = 60 };
        var validator = MakeValidator();

        var ex = Assert.Throws<ConfigException>(() => validator.ValidateOrThrow(config));
        Assert.Contains("gamePath is required", ex.Message);
    }

    [Fact]
    public void ValidateOrThrow_NonExistentDirectory_Throws()
    {
        var config    = new ServerConfig { GamePath = @"C:\DoesNotExist\FakeFFXIV", LanguageDefault = "en", CacheTTLSeconds = 60 };
        var validator = MakeValidator();

        var ex = Assert.Throws<ConfigException>(() => validator.ValidateOrThrow(config));
        Assert.Contains("does not exist", ex.Message);
    }

    [Fact]
    public void ValidateOrThrow_ExistingDirNotFfxivRoot_Throws()
    {
        // A plain temp directory — exists but has no FFXIV markers.
        var emptyDir  = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(emptyDir);
        try
        {
            var config    = new ServerConfig { GamePath = emptyDir, LanguageDefault = "en", CacheTTLSeconds = 60 };
            var validator = MakeValidator();

            var ex = Assert.Throws<ConfigException>(() => validator.ValidateOrThrow(config));
            Assert.Contains("does not look like an FFXIV installation root", ex.Message);
        }
        finally
        {
            Directory.Delete(emptyDir, recursive: true);
        }
    }

    [Fact]
    public void ValidateOrThrow_FfxivRootViaSqpackSubdir_Passes()
    {
        // Presence of "sqpack" directory (no game/ffxivgame.ver) is also accepted.
        var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(root, "sqpack"));
        try
        {
            var config    = ValidConfig(root);
            var validator = MakeValidator();

            var ex = Record.Exception(() => validator.ValidateOrThrow(config));
            Assert.Null(ex);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // ── languageDefault validations ────────────────────────────────────────

    [Fact]
    public void ValidateOrThrow_UnknownLanguage_Throws()
    {
        var root   = CreateFakeFfxivRoot();
        try
        {
            var config    = new ServerConfig { GamePath = root, LanguageDefault = "zz", CacheTTLSeconds = 60 };
            var validator = MakeValidator();

            var ex = Assert.Throws<ConfigException>(() => validator.ValidateOrThrow(config));
            Assert.Contains("languageDefault", ex.Message);
            Assert.Contains("zz", ex.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("en")]
    [InlineData("fr")]
    [InlineData("de")]
    [InlineData("ja")]
    public void ValidateOrThrow_KnownLanguage_Passes(string lang)
    {
        var root      = CreateFakeFfxivRoot();
        try
        {
            var config    = new ServerConfig { GamePath = root, LanguageDefault = lang, CacheTTLSeconds = 60 };
            var validator = MakeValidator();

            var ex = Record.Exception(() => validator.ValidateOrThrow(config));
            Assert.Null(ex);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // ── cacheTTLSeconds validations ────────────────────────────────────────

    [Fact]
    public void ValidateOrThrow_NegativeTtl_Throws()
    {
        var root   = CreateFakeFfxivRoot();
        try
        {
            var config    = new ServerConfig { GamePath = root, LanguageDefault = "en", CacheTTLSeconds = -1 };
            var validator = MakeValidator();

            var ex = Assert.Throws<ConfigException>(() => validator.ValidateOrThrow(config));
            Assert.Contains("cacheTTLSeconds", ex.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ValidateOrThrow_ZeroTtl_Passes()
    {
        var root      = CreateFakeFfxivRoot();
        try
        {
            var config    = new ServerConfig { GamePath = root, LanguageDefault = "en", CacheTTLSeconds = 0 };
            var validator = MakeValidator();

            var ex = Record.Exception(() => validator.ValidateOrThrow(config));
            Assert.Null(ex);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // ── full valid config ──────────────────────────────────────────────────

    [Fact]
    public void ValidateOrThrow_ValidConfig_DoesNotThrow()
    {
        var root      = CreateFakeFfxivRoot();
        try
        {
            var config    = ValidConfig(root);
            var validator = MakeValidator();

            var ex = Record.Exception(() => validator.ValidateOrThrow(config));
            Assert.Null(ex);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // ── multiple errors collected ──────────────────────────────────────────

    [Fact]
    public void ValidateOrThrow_MultipleErrors_AllReportedInSingleException()
    {
        var config    = new ServerConfig
        {
            GamePath        = "",
            LanguageDefault = "zz",
            CacheTTLSeconds = -5,
        };
        var validator = MakeValidator();

        var ex = Assert.Throws<ConfigException>(() => validator.ValidateOrThrow(config));
        // All three issues should appear in one message.
        Assert.Contains("gamePath is required", ex.Message);
        Assert.Contains("languageDefault", ex.Message);
        Assert.Contains("cacheTTLSeconds", ex.Message);
    }
}
