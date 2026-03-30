namespace McpLumina.Configuration;

public sealed class ServerConfig
{
    public const string SectionName = "McpLumina";

    /// <summary>Path to the root FFXIV game installation directory (required).</summary>
    public string GamePath { get; set; } = string.Empty;

    /// <summary>Default language when none is specified by the caller. One of: en, fr, de, ja.</summary>
    public string LanguageDefault { get; set; } = "en";

    /// <summary>Enable in-memory response caching.</summary>
    public bool CacheEnabled { get; set; } = true;

    /// <summary>Cache entry TTL in seconds.</summary>
    public int CacheTTLSeconds { get; set; } = 300;

    /// <summary>Serilog/Microsoft.Extensions.Logging log level.</summary>
    public string LogLevel { get; set; } = "Information";

    /// <summary>
    /// Optional path to a local clone of the EXDSchema repository (https://github.com/xivdev/EXDSchema).
    /// When set, column names in all responses use real field names (e.g. "Name", "ClassJob") instead
    /// of positional indices (e.g. "Column_0", "Column_10"). Check out the version branch matching
    /// your game version, e.g. ver/2026.03.17.0000.0000. Leave empty to disable (Column_N fallbacks used).
    /// </summary>
    public string? SchemaPath { get; set; }

}
