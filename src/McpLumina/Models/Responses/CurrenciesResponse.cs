namespace McpLumina.Models.Responses;

public sealed record CurrenciesResponse : BaseResponse
{
    public string[]         LanguagesRequested { get; init; } = [];
    public string[]         LanguagesReturned  { get; init; } = [];
    public bool             FallbackUsed       { get; init; }
    public CurrencyEntry[]  Currencies         { get; init; } = [];
}

public sealed record CurrencyEntry(
    uint RowId,
    /// <summary>Localized currency name keyed by language code.</summary>
    Dictionary<string, string> Name,
    uint Icon,
    /// <summary>Maximum stack size (e.g. 999999999 for Gil, 2000 for tomestones).</summary>
    uint StackSize
);
