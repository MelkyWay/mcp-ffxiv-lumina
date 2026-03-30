namespace McpLumina.Models.Responses;

public sealed record WeatherResponse : BaseResponse
{
    public string?        Query              { get; init; }
    public string[]       LanguagesRequested { get; init; } = [];
    public string[]       LanguagesReturned  { get; init; } = [];
    public bool           FallbackUsed       { get; init; }
    public int            TotalMatches       { get; init; }
    public int            Offset             { get; init; }
    public int            Limit              { get; init; }
    public WeatherEntry[] Weathers           { get; init; } = [];
}

public sealed record WeatherEntry(
    uint RowId,
    /// <summary>Localized weather name keyed by language code.</summary>
    Dictionary<string, string> Name,
    uint Icon
);
