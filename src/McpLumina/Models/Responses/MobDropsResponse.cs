namespace McpLumina.Models.Responses;

public sealed record MobDropsResponse : BaseResponse
{
    public string? MonsterQuery        { get; init; }
    public string? ItemQuery           { get; init; }
    public string[] LanguagesRequested { get; init; } = [];
    public string[] LanguagesReturned  { get; init; } = [];
    public bool FallbackUsed           { get; init; }
    public int TotalMatches            { get; init; }
    public int Offset                  { get; init; }
    public int Limit                   { get; init; }
    public MobDropEntry[] Drops        { get; init; } = [];
}

public sealed record MobDropEntry(
    uint BNpcNameId,
    Dictionary<string, string> MobName,
    uint ItemId,
    Dictionary<string, string> ItemName);
