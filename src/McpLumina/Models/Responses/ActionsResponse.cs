namespace McpLumina.Models.Responses;

public sealed record ActionsResponse : BaseResponse
{
    public string?        Query              { get; init; }
    public string[]       LanguagesRequested { get; init; } = [];
    public string[]       LanguagesReturned  { get; init; } = [];
    public bool           FallbackUsed       { get; init; }
    public int            TotalMatches       { get; init; }
    public int            Offset             { get; init; }
    public int            Limit              { get; init; }
    public ActionEntry[]  Actions            { get; init; } = [];
}

public sealed record ActionEntry(
    uint   RowId,
    /// <summary>Localized action name keyed by language code.</summary>
    Dictionary<string, string> Name,
    uint   Icon,
    /// <summary>ClassJob row ID this action belongs to. 0 = role/cross-class.</summary>
    uint   ClassJobId,
    byte   ClassJobLevel,
    /// <summary>ActionCategory row ID (1=Auto-attack, 2=Spell, 3=Weaponskill, 4=Ability, etc.).</summary>
    uint   ActionCategoryId,
    string ActionCategoryName,
    bool   IsRoleAction,
    bool   IsPvP,
    /// <summary>Cast time in milliseconds.</summary>
    uint   CastTimeMs,
    /// <summary>Recast/cooldown in milliseconds.</summary>
    uint   RecastTimeMs,
    byte   MaxCharges
);
