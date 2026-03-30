namespace McpLumina.Models.Responses;

public sealed record MinionsResponse : BaseResponse
{
    public string?        Query              { get; init; }
    public string[]       LanguagesRequested { get; init; } = [];
    public string[]       LanguagesReturned  { get; init; } = [];
    public bool           FallbackUsed       { get; init; }
    public int            TotalMatches       { get; init; }
    public int            Offset             { get; init; }
    public int            Limit              { get; init; }
    public MinionEntry[]  Minions            { get; init; } = [];
}

public sealed record MinionEntry(
    uint   RowId,
    /// <summary>Localized singular minion name keyed by language code.</summary>
    Dictionary<string, string> Name,
    uint   Icon
);
