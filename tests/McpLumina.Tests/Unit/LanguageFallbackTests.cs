using McpLumina.Models;
using McpLumina.Services;
using Xunit;

namespace McpLumina.Tests.Unit;

public sealed class LanguageFallbackTests
{
    private static LanguageService MakeService(params string[] available) =>
        new(new HashSet<string>(available), "en");

    // ── Resolve ───────────────────────────────────────────────────────────

    [Fact]
    public void Resolve_Null_ReturnsDefault()
    {
        var svc    = MakeService("en");
        var result = svc.Resolve(null);
        Assert.Equal(["en"], result);
    }

    [Fact]
    public void Resolve_Empty_ReturnsDefault()
    {
        var svc    = MakeService("en");
        var result = svc.Resolve([]);
        Assert.Equal(["en"], result);
    }

    [Fact]
    public void Resolve_KnownCodes_ReturnsThem()
    {
        var svc    = MakeService("en", "fr");
        var result = svc.Resolve(["en", "fr"]);
        Assert.Equal(["en", "fr"], result);
    }

    [Fact]
    public void Resolve_UnknownCode_Throws()
    {
        var svc = MakeService("en");
        Assert.Throws<LanguageUnavailableException>(() => svc.Resolve(["zz"]));
    }

    // ── ApplyFallback ─────────────────────────────────────────────────────

    [Fact]
    public void ApplyFallback_AvailableLanguage_NoFallback()
    {
        var svc             = MakeService("en", "fr");
        var (returned, fb)  = svc.ApplyFallback(["en"]);
        Assert.Equal(["en"], returned);
        Assert.False(fb);
    }

    [Fact]
    public void ApplyFallback_UnavailableFrench_FallsBackToEnglish()
    {
        // fr not in available set; should fall back to en
        var svc            = MakeService("en");
        var (returned, fb) = svc.ApplyFallback(["fr"]);
        Assert.Contains("en", returned);
        Assert.True(fb);
    }

    [Fact]
    public void ApplyFallback_AllAvailable_NoFallback()
    {
        var svc            = MakeService("en", "fr", "de", "ja");
        var (returned, fb) = svc.ApplyFallback(["en", "fr", "de", "ja"]);
        Assert.Equal(4, returned.Length);
        Assert.False(fb);
    }

    [Fact]
    public void ApplyFallback_DeduplicatesFallbacks()
    {
        // Both "fr" and "de" unavailable; both fall back to "en" — result should have "en" once
        var svc            = MakeService("en");
        var (returned, fb) = svc.ApplyFallback(["fr", "de"]);
        Assert.Single(returned);
        Assert.Equal("en", returned[0]);
        Assert.True(fb);
    }
}
