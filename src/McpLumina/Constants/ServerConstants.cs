namespace McpLumina.Constants;

// ============================================================
// PATCH-SENSITIVE FILE
// Review and update after every major FFXIV patch.
// Last validated: Patch 7.2 / 2026-03-17 (game ver 2026.03.17.0000.0000)
//
// Steps after a patch:
//   1. Update KnownGoodGameVersion.Value
//   2. Verify ContentTypeIds still map to the expected content categories
//   3. Run integration tests: dotnet test --filter Category=Integration
// ============================================================

public static class KnownGoodGameVersion
{
    public const string Value = "2026.03.17.0000.0000";
}

/// <summary>
/// ContentType row IDs used to categorise duties in get_duties.
/// Verified against ContentType sheet via Lumina probe (patch 7.2).
/// NOTE: Unreal trials share ContentType=4 (Trial) and are identified by name suffix only.
/// </summary>
public static class ContentTypeIds
{
    public const int Dungeon   = 2;
    public const int Trial     = 4;   // Also covers Unreal duties (filtered by name suffix)
    public const int Raid      = 5;
    public const int Ultimate  = 28;
    public const int Criterion = 30;
}

public static class DutyCategories
{
    public const string Dungeon   = "dungeon";
    public const string Trial     = "trial";
    public const string Raid      = "raid";
    public const string Ultimate  = "ultimate";
    public const string Criterion = "criterion";
    public const string Unreal    = "unreal";

    /// <summary>
    /// Name substring used to identify Unreal trials within ContentType=4 (Trial) rows.
    /// Update if Square Enix changes the naming convention.
    /// </summary>
    public const string UnrealNameSuffix = "(Unreal)";

    public static readonly IReadOnlyList<string> All =
    [
        Dungeon, Trial, Raid, Ultimate, Criterion, Unreal
    ];

    public static int? ToContentTypeId(string? category) => category?.ToLowerInvariant() switch
    {
        Dungeon   => ContentTypeIds.Dungeon,
        Trial     => ContentTypeIds.Trial,
        Raid      => ContentTypeIds.Raid,
        Ultimate  => ContentTypeIds.Ultimate,
        Criterion => ContentTypeIds.Criterion,
        Unreal    => ContentTypeIds.Trial,  // Unreal shares ContentType=4 with Trial
        _         => null
    };
}

/// <summary>
/// Role groupings derived from ClassJob.Role values.
/// English-only labels — not sourced from game strings.
/// </summary>
public static class RoleLabels
{
    // ClassJob.Role byte → label. Role=3 covers all ranged; MagicalRangedJobIds splits it further.
    // Entry [5] is not a real game byte — used only to include Magical Ranged DPS in GetRoleLabels output.
    public static readonly IReadOnlyDictionary<byte, string> ByRoleId = new Dictionary<byte, string>
    {
        [1] = "Tank",
        [2] = "Melee DPS",
        [3] = "Physical Ranged DPS",
        [4] = "Healer",
        [5] = "Magical Ranged DPS",
    };

    // ClassJob row IDs whose Role byte = 3 but are magical ranged casters.
    // Physical ranged (ARC/BRD/MCH/DNC) also have Role=3 and use the ByRoleId default.
    private static readonly HashSet<uint> MagicalRangedJobIds = [7, 25, 26, 27, 35, 42];

    public const string MagicalRanged = "Magical Ranged DPS";
    public const string Limited = "Limited";
    public const string None    = "None";

    public static string Resolve(byte roleId, bool isLimited, uint rowId)
    {
        if (isLimited) return Limited;
        if (roleId == 3 && MagicalRangedJobIds.Contains(rowId)) return MagicalRanged;
        return ByRoleId.TryGetValue(roleId, out var label) ? label : None;
    }
}

/// <summary>
/// Known language codes supported by the server.
/// </summary>
public static class KnownLanguageCodes
{
    public const string En = "en";
    public const string Fr = "fr";
    public const string De = "de";
    public const string Ja = "ja";

    public static readonly IReadOnlyList<string> All = [En, Fr, De, Ja];

    public static bool Contains(string code) =>
        All.Contains(code, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Valid values for the <c>kind</c> parameter of get_localized_labels.
/// </summary>
public static class LabelKinds
{
    public const string Jobs       = "jobs";
    public const string Roles      = "roles";
    public const string Categories = "categories";
    public const string Ultimates  = "ultimates";
    public const string Criterion  = "criterion";
    public const string Unreal     = "unreal";

    public static readonly IReadOnlyList<string> All =
        [Jobs, Roles, Categories, Ultimates, Criterion, Unreal];
}

/// <summary>
/// StatusCategory byte values from the Status sheet.
/// 1 = Detrimental (debuff), 2 = Beneficial (buff), others treated as Other.
/// </summary>
public static class StatusCategories
{
    public const byte Beneficial  = 1;
    public const byte Detrimental = 2;

    public const string DetrimentalLabel = "detrimental";
    public const string BeneficialLabel  = "beneficial";

    public static readonly IReadOnlyList<string> All = [DetrimentalLabel, BeneficialLabel];

    public static string Resolve(byte category) => category switch
    {
        Detrimental => DetrimentalLabel,
        Beneficial  => BeneficialLabel,
        _ => "other",
    };
}

/// <summary>
/// Tomestone currency status labels derived from TomestonesItem.Column_2 (Category).
/// Category values are stable rotation slots — the underlying item names change each patch
/// but the category→status mapping remains the same.
/// </summary>
public static class TomestoneStatuses
{
    public const string Current  = "current";   // Category 3 — current limited (e.g. Mnemonics)
    public const string Previous = "previous";  // Category 2 — previous limited (e.g. Mathematics)
    public const string Older    = "older";     // Category 4 — older limited (e.g. Heliometry)
    public const string Poetics  = "poetics";   // Category 1 — permanent uncapped (Poetics)
    public const string Retired  = "retired";   // Category 0 — historical/demoted tomestones

    public static readonly IReadOnlyList<string> ValidValues =
        [Current, Previous, Older, Poetics, Retired];

    /// <summary>Sort priority: current first, retired last.</summary>
    private static readonly IReadOnlyDictionary<byte, int> SortOrder =
        new Dictionary<byte, int> { [3] = 0, [2] = 1, [4] = 2, [1] = 3, [0] = 4 };

    public static string FromCategory(byte category) => category switch
    {
        3 => Current,
        2 => Previous,
        4 => Older,
        1 => Poetics,
        _ => Retired,
    };

    public static int SortPriority(byte category) =>
        SortOrder.TryGetValue(category, out var p) ? p : 99;
}

/// <summary>
/// Hardcoded labels for TripleTriadCardObtain rows (RowId 1–14).
/// The Addon.Text chain for these entries is mostly empty or fragmentary, so labels
/// are derived from context. Those marked as inferred (*) may be imprecise.
/// </summary>
public static class TripleTriadObtainTypes
{
    // Index = TripleTriadCardObtain RowId. Index 0 is unused.
    public static readonly string[] Labels =
    [
        "",                        // 0  (unused)
        "Triple Triad tutorial",   // 1  confirmed
        "Card shop",               // 2  *
        "Card shop",               // 3  *
        "Vendor",                  // 4  *
        "FATE reward",             // 5  *
        "NPC challenge reward",    // 6  confirmed
        "Tournament reward",       // 7  confirmed ("Place in top rankings.")
        "Venture purchase",        // 8  confirmed ("Random result from purchase:")
        "Appraisal",               // 9  confirmed ("Random result from appraisal:")
        "Deep dungeon",            // 10 *
        "Unknown",                 // 11
        "Treasure hunt",           // 12 confirmed ("Happy bunny treasure hunt.")
        "Seasonal event",          // 13 confirmed ("Obtained from a seasonal event.")
        "Vendor",                  // 14 *
    ];

    public static string Get(uint rowId) =>
        rowId < (uint)Labels.Length ? Labels[rowId] : "";
}

public static class ServerInfo
{
    public const string Version = "1.0.0";
    public const string Name    = "mcp-lumina";
}

public static class Limits
{
    public const int MaxBatchRowIds  = 100;
    public const int MaxResultLimit  = 200;
    public const int MaxOffset       = 10_000;
    public const int DefaultLimit    = 50;
}
