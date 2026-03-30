namespace McpLumina.Models.Responses;

public sealed record TitlesResponse : BaseResponse
{
    public string?       Query              { get; init; }
    public string[]      LanguagesRequested { get; init; } = [];
    public string[]      LanguagesReturned  { get; init; } = [];
    public bool          FallbackUsed       { get; init; }
    public int           TotalMatches       { get; init; }
    public int           Offset             { get; init; }
    public int           Limit              { get; init; }
    public TitleEntry[]  Titles             { get; init; } = [];
}

public sealed record TitleEntry(
    uint RowId,
    /// <summary>Masculine title text keyed by language code.</summary>
    Dictionary<string, string> Masculine,
    /// <summary>Feminine title text keyed by language code.</summary>
    Dictionary<string, string> Feminine,
    /// <summary>True if the title is a prefix (e.g. "The Insatiable the player"); false if suffix.</summary>
    bool IsPrefix
);
