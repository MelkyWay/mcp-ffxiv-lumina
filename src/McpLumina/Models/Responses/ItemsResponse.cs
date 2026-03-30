namespace McpLumina.Models.Responses;

public sealed record ItemsResponse : BaseResponse
{
    public string      Query              { get; init; } = string.Empty;
    public string[]    LanguagesRequested { get; init; } = [];
    public string[]    LanguagesReturned  { get; init; } = [];
    public bool        FallbackUsed       { get; init; }
    public int         TotalMatches       { get; init; }
    public int         Offset             { get; init; }
    public int         Limit              { get; init; }
    public ItemEntry[] Items              { get; init; } = [];
}

/// <param name="Name">Localized singular lowercase grammatical form (e.g. "potion").</param>
/// <param name="DisplayName">Localized capitalized display name (e.g. "Potion").</param>
/// <param name="Description">Localized item description tooltip text.</param>
/// <param name="ItemLevel">Item level (ilvl); reliable for equippable items, may be 0 or inaccurate for consumables/tokens.</param>
/// <param name="EquipLevel">Required character level to equip (0 = not equippable/consumable).</param>
/// <param name="StackSize">Maximum stack size.</param>
/// <param name="Icon">Icon ID (use with /i/XXXYYY/XXXYYY.tex path pattern).</param>
/// <param name="Rarity">Item rarity: 1=white (common), 2=green, 3=blue (rare), 4=purple (relic/artifact).</param>
/// <param name="FilterGroup">Item category ID (1=physical weapon, 8=HP potion, 16=misc, 31=soul crystal, etc.).</param>
/// <param name="PriceMid">NPC vendor price in gil (0 = not sold by NPCs).</param>
/// <param name="CanBeHq">Whether the item can be obtained as high quality.</param>
public sealed record ItemEntry(
    uint   RowId,
    Dictionary<string, string> Name,
    Dictionary<string, string> DisplayName,
    Dictionary<string, string> Description,
    ushort ItemLevel,
    byte   EquipLevel,
    uint   StackSize,
    uint   Icon,
    byte   Rarity,
    byte   FilterGroup,
    uint   PriceMid,
    bool   CanBeHq
);
