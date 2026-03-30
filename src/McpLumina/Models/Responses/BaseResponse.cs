namespace McpLumina.Models.Responses;

/// <summary>
/// Fields included on every response for stable frontend caching and traceability.
/// </summary>
public abstract record BaseResponse
{
    public string GameVersion { get; init; } = string.Empty;
    public string Timestamp   { get; init; } = DateTimeOffset.UtcNow.ToString("O");
}
