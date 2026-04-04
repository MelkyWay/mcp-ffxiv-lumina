namespace McpLumina.Models.Responses;

public sealed record TripleTriadCardsResponse : BaseResponse
{
    public string?                Query              { get; init; }
    public string[]               LanguagesRequested { get; init; } = [];
    public string[]               LanguagesReturned  { get; init; } = [];
    public bool                   FallbackUsed       { get; init; }
    public int                    TotalMatches       { get; init; }
    public int                    Offset             { get; init; }
    public int                    Limit              { get; init; }
    public TripleTriadCardEntry[] Cards              { get; init; } = [];
}

public sealed record TripleTriadCardEntry(
    uint                        RowId,
    Dictionary<string, string>  Name,
    Dictionary<string, string>? Description,
    bool                        StartsWithVowel,
    // Gameplay stats (from TripleTriadCardResident, language-neutral)
    byte                        Top,
    byte                        Bottom,
    byte                        Left,
    byte                        Right,
    int                         Stars,
    string                      Type,
    uint                        SaleValue,
    // Acquisition (resolved in primary language)
    string?                     AcquisitionSource,
    uint                        ObtainTypeIcon);
