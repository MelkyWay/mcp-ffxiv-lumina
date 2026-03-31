namespace McpLumina.Models.Responses;

public sealed record TomestoneCurrenciesResponse : BaseResponse
{
    public string?                   StatusFilter       { get; init; }
    public string[]                  LanguagesRequested { get; init; } = [];
    public string[]                  LanguagesReturned  { get; init; } = [];
    public bool                      FallbackUsed       { get; init; }
    public int                       TotalMatches       { get; init; }
    public TomestoneCurrencyEntry[]  Currencies         { get; init; } = [];
}

public sealed record TomestoneCurrencyEntry(
    uint   RowId,
    Dictionary<string, string> Name,
    uint   Icon,
    /// <summary>"current" | "previous" | "older" | "poetics" | "retired"</summary>
    string Status,
    uint   StackSize
);
