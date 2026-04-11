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

/// <summary>Resolved ClassJob reference with id and localized name.</summary>
public sealed record ClassJobRef(uint Id, Dictionary<string, string> Name);

public sealed record ActionEntry(
    uint   RowId,
    /// <summary>Localized action name keyed by language code.</summary>
    Dictionary<string, string> Name,
    uint   Icon,
    /// <summary>ClassJob this action belongs to. Null = role/cross-class action.</summary>
    ClassJobRef? ClassJob,
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
