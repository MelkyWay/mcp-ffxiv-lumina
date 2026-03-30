namespace McpLumina.Models.Responses;

public sealed record TraitsResponse : BaseResponse
{
    public string?      Query              { get; init; }
    public string[]     LanguagesRequested { get; init; } = [];
    public string[]     LanguagesReturned  { get; init; } = [];
    public bool         FallbackUsed       { get; init; }
    public int          TotalMatches       { get; init; }
    public int          Offset             { get; init; }
    public int          Limit              { get; init; }
    public TraitEntry[] Traits             { get; init; } = [];
}

public sealed record TraitEntry(
    uint   RowId,
    /// <summary>Localized trait name keyed by language code.</summary>
    Dictionary<string, string> Name,
    uint   Icon,
    /// <summary>ClassJob row ID this trait belongs to. 0 = shared/role trait.</summary>
    uint   ClassJobId,
    /// <summary>Level at which this trait is learned.</summary>
    uint   Level
);
