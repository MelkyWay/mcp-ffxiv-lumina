namespace McpLumina.Models.Responses;

public sealed record RacesResponse : BaseResponse
{
    public string[]    LanguagesRequested { get; init; } = [];
    public string[]    LanguagesReturned  { get; init; } = [];
    public bool        FallbackUsed       { get; init; }
    public RaceEntry[] Races              { get; init; } = [];
}

public sealed record RaceEntry(
    uint RowId,
    /// <summary>Localized masculine race name keyed by language code.</summary>
    Dictionary<string, string> Masculine,
    /// <summary>Localized feminine race name keyed by language code.</summary>
    Dictionary<string, string> Feminine
);
