namespace McpLumina.Models.Responses;

public sealed record AchievementsResponse : BaseResponse
{
    public string?             Query              { get; init; }
    public string[]            LanguagesRequested { get; init; } = [];
    public string[]            LanguagesReturned  { get; init; } = [];
    public bool                FallbackUsed       { get; init; }
    public int                 TotalMatches       { get; init; }
    public int                 Offset             { get; init; }
    public int                 Limit              { get; init; }
    public AchievementEntry[]  Achievements       { get; init; } = [];
}

public sealed record AchievementEntry(
    uint   RowId,
    /// <summary>Localized achievement name keyed by language code.</summary>
    Dictionary<string, string> Name,
    /// <summary>Localized achievement description keyed by language code.</summary>
    Dictionary<string, string> Description,
    uint   Icon,
    uint   Points,
    uint   AchievementCategoryId,
    string AchievementCategoryName
);
