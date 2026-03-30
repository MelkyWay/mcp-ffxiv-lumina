namespace McpLumina.Models.Responses;

public sealed record StatusesResponse : BaseResponse
{
    public string?        Query              { get; init; }
    public string?        Category           { get; init; }
    public string[]       LanguagesRequested { get; init; } = [];
    public string[]       LanguagesReturned  { get; init; } = [];
    public bool           FallbackUsed       { get; init; }
    public int            TotalMatches       { get; init; }
    public int            Offset             { get; init; }
    public int            Limit              { get; init; }
    public StatusEntry[]  Statuses           { get; init; } = [];
}

public sealed record StatusEntry(
    uint   RowId,
    /// <summary>Localized status name keyed by language code.</summary>
    Dictionary<string, string> Name,
    /// <summary>Localized status description keyed by language code.</summary>
    Dictionary<string, string> Description,
    uint   Icon,
    /// <summary>Raw StatusCategory byte (1=Beneficial, 2=Detrimental).</summary>
    byte   StatusCategory,
    /// <summary>Human-readable category label: "detrimental", "beneficial", or "other".</summary>
    string StatusCategoryName,
    bool   CanDispel,
    bool   IsFcBuff,
    bool   IsGaze,
    bool   IsPermanent
);
