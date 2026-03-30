namespace McpLumina.Models.Responses;

public sealed record HealthResponse : BaseResponse
{
    public string   Status          { get; init; } = "ok";   // "ok" | "degraded"
    public string   ServerVersion   { get; init; } = string.Empty;
    public string   GamePath        { get; init; } = string.Empty;
    public string   DetectedVersion { get; init; } = string.Empty;
    public string   ValidatedVersion { get; init; } = string.Empty;
    public bool     SchemaAvailable  { get; init; }
    public string?  SchemaVersion    { get; init; }
    public bool     CacheEnabled    { get; init; }
    public int      CacheTTLSeconds { get; init; }
    public long     UptimeSeconds   { get; init; }
    public HealthWarning[] Warnings { get; init; } = [];
}

public sealed record HealthWarning(
    string Code,
    string Message,
    string? DetectedVersion  = null,
    string? ValidatedVersion = null
);
