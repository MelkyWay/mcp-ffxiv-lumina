namespace McpLumina.Models.Responses;

public sealed record MountsResponse : BaseResponse
{
    public string?       Query              { get; init; }
    public string[]      LanguagesRequested { get; init; } = [];
    public string[]      LanguagesReturned  { get; init; } = [];
    public bool          FallbackUsed       { get; init; }
    public int           TotalMatches       { get; init; }
    public int           Offset             { get; init; }
    public int           Limit              { get; init; }
    public MountEntry[]  Mounts             { get; init; } = [];
}

public sealed record MountEntry(
    uint   RowId,
    /// <summary>Localized singular mount name keyed by language code.</summary>
    Dictionary<string, string> Name,
    uint   Icon,
    bool   IsFlying,
    /// <summary>Number of additional passenger seats (0 = solo mount).</summary>
    byte   ExtraSeats
);
