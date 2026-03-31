namespace McpLumina.Models.Responses;

public sealed record MateriaResponse : BaseResponse
{
    public string?        Query              { get; init; }
    public string?        Stat               { get; init; }
    public string[]       LanguagesRequested { get; init; } = [];
    public string[]       LanguagesReturned  { get; init; } = [];
    public bool           FallbackUsed       { get; init; }
    public int            TotalMatches       { get; init; }
    public int            Offset             { get; init; }
    public int            Limit              { get; init; }
    public MateriaEntry[] Materia            { get; init; } = [];
}

/// <param name="Tier">1-indexed tier number (1=I, 2=II, …, 12=XII).</param>
/// <param name="Bonus">
/// Stat bonus granted by this materia item. 0 for pre-Heavensward primary-stat
/// materia (Strength/Dexterity/Vitality/Intelligence/Mind tiers I–VI) where the
/// Materia sheet does not store per-tier values.
/// </param>
public sealed record MateriaEntry(
    uint   RowId,
    Dictionary<string, string> Name,
    uint   Icon,
    string Stat,
    uint   BaseParamId,
    int    Tier,
    int    Bonus
);
