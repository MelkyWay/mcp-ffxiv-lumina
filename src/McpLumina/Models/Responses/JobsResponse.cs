namespace McpLumina.Models.Responses;

public sealed record JobsResponse : BaseResponse
{
    public string[]     LanguagesRequested { get; init; } = [];
    public string[]     LanguagesReturned  { get; init; } = [];
    public bool         FallbackUsed       { get; init; }
    public JobEntry[]   Jobs               { get; init; } = [];
}

public sealed record JobEntry(
    uint   RowId,
    /// <summary>Localized job name keyed by language code.</summary>
    Dictionary<string, string> Name,
    /// <summary>Localized abbreviation keyed by language code.</summary>
    Dictionary<string, string> Abbreviation,
    string Role,        // English-only derived label (V1 limitation)
    bool   IsJob,       // false = base class (e.g. Marauder), true = job (e.g. Warrior)
    bool   IsLimited,   // true = limited job (e.g. Blue Mage or Beastmaster)
    byte   JobIndex     // display sort order
);
