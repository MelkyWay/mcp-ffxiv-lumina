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

    // ── ResolveEffectiveDefault ───────────────────────────────────────────

    [Fact]
    public void ResolveEffectiveDefault_ConfiguredIsAvailable_ReturnsConfigured()
    {
        var result = LanguageService.ResolveEffectiveDefault("ko", new HashSet<string> { "ko", "en" });
        Assert.Equal("ko", result);
    }

    [Fact]
    public void ResolveEffectiveDefault_ConfiguredUnavailable_FallsBackToEnglish()
    {
        var result = LanguageService.ResolveEffectiveDefault("ko", new HashSet<string> { "en", "fr" });
        Assert.Equal("en", result);
    }

    [Fact]
    public void ResolveEffectiveDefault_ConfiguredUnavailableAndNoEnglish_FallsBackToFirst()
    {
        var result = LanguageService.ResolveEffectiveDefault("ko", new HashSet<string> { "chs" });
        Assert.Equal("chs", result);
    }

    [Fact]
    public void ResolveEffectiveDefault_NormalizesCase()
    {
        var result = LanguageService.ResolveEffectiveDefault("EN", new HashSet<string> { "en" });
        Assert.Equal("en", result);
    }

    // ── CodeToLumina ──────────────────────────────────────────────────────

    [Fact]
    public void CodeToLumina_DoesNotContainEmptyKey()
    {
        Assert.DoesNotContain(string.Empty, LanguageService.CodeToLumina.Keys);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("fr")]
    [InlineData("de")]
    [InlineData("ja")]
    [InlineData("ko")]
    [InlineData("chs")]
    public void CodeToLumina_ContainsExpectedLanguageCodes(string code)
    {
        Assert.Contains(code, LanguageService.CodeToLumina.Keys);
    }
}
