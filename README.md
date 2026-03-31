# mcp-ffxiv-lumina

An MCP (Model Context Protocol) server for Final Fantasy XIV game data, built on [Lumina](https://github.com/NotAdam/Lumina).

Provides localised, queryable access to FFXIV game data sheets via MCP stdio transport. Designed for use with Claude Desktop, AI assistants, and any MCP-compatible client.

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A local FFXIV installation (PC or Steam — the standard `SquareEnix/FINAL FANTASY XIV` directory)
- The game must have been launched at least once so EXD data files are present on disk

---

## Setup

```bash
# Clone and build
git clone https://github.com/MelkyWay/mcp-ffxiv-lumina
cd mcp-ffxiv-lumina
dotnet build -c Release

# Run directly, supplying gamePath on the command line
dotnet run --project src/McpLumina -- --McpLumina:GamePath "C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn"
```

---

## Configuration

Edit `src/McpLumina/appsettings.json`:

```json
{
  "McpLumina": {
    "gamePath": "C:\\Program Files (x86)\\SquareEnix\\FINAL FANTASY XIV - A Realm Reborn",
    "languageDefault": "en",
    "cacheEnabled": true,
    "cacheTTLSeconds": 300,
    "logLevel": "Information",
    "schemaPath": "C:\\path\\to\\EXDSchema"
  }
}
```

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `gamePath` | string | **yes** | — | Path to the FFXIV installation root |
| `languageDefault` | string | no | `"en"` | Default language when none is specified: `en`, `fr`, `de`, `ja` |
| `cacheEnabled` | bool | no | `true` | Enable in-memory response caching |
| `cacheTTLSeconds` | int | no | `300` | Cache TTL in seconds |
| `logLevel` | string | no | `"Information"` | Log verbosity (`Trace`, `Debug`, `Information`, `Warning`, `Error`) |
| `schemaPath` | string | no | — | Path to a local [EXDSchema](https://github.com/xivdev/EXDSchema) clone. When set, `describe_sheet` reports real field names instead of `Column_N` indices. |

All settings can also be set via environment variables using the `McpLumina__` prefix:

```bash
McpLumina__GamePath="C:\..." McpLumina__LanguageDefault=ja dotnet run --project src/McpLumina
```

> **Note:** All logging goes to stderr. stdout is reserved exclusively for the MCP stdio frame stream.

### EXDSchema (optional)

[EXDSchema](https://github.com/xivdev/EXDSchema) is a community-maintained repository of FFXIV sheet column definitions. When configured via `schemaPath`, `describe_sheet` returns real field names (e.g. `Name`, `ClassJob`) instead of positional indices (`Column_0`, `Column_10`), and `get_row` / `search_rows` accept those names in `return_fields` and `text_fields`.

```bash
git clone https://github.com/xivdev/EXDSchema C:\EXDSchema
cd C:\EXDSchema
git checkout ver/2026.03.17.0000.0000   # branch matching your game version
```

Use `refresh_schema` to fetch and switch to the latest branch without restarting the server.

---

## MCP Client Configuration

The server communicates over **stdio** using the MCP protocol. Any MCP-compatible client (Claude Desktop, Cursor, VS Code with MCP extensions, custom agents, etc.) can connect by launching the process and piping stdio.

### Published binary (recommended)

```bash
dotnet publish src/McpLumina -c Release -r win-x64 --self-contained -o publish/
```

Point your client at the binary:

```
command: C:\path\to\publish\mcp-lumina.exe
env:     McpLumina__GamePath = C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn
```

### Development (`dotnet run`)

```
command: dotnet
args:    run --project C:\path\to\mcp-ffxiv-lumina\src\McpLumina --no-launch-profile
env:     McpLumina__GamePath = C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn
```

### Example: Claude Desktop (`claude_desktop_config.json`)

```json
{
  "mcpServers": {
    "ffxiv": {
      "command": "C:\\path\\to\\publish\\mcp-lumina.exe",
      "env": {
        "McpLumina__GamePath": "C:\\Program Files (x86)\\SquareEnix\\FINAL FANTASY XIV - A Realm Reborn"
      }
    }
  }
}
```

---

## Tools Reference

The server exposes twenty-four tools across three categories:

- **Server tools** — health, schema management, language info
- **Generic sheet tools** — arbitrary access to any game data sheet
- **FFXIV convenience tools** — pre-shaped responses for common game entities

---

### `health()`

Returns server status, game version, cache settings, and uptime. Use this to confirm the server is running correctly and to check whether the game has been patched since the server was last validated.

```json
{
  "status": "ok",
  "serverVersion": "1.0.0",
  "gamePath": "C:\\...",
  "detectedVersion": "2026.03.17.0000.0000",
  "validatedVersion": "2026.03.17.0000.0000",
  "schemaAvailable": true,
  "schemaVersion": "ver/2026.03.17.0000.0000",
  "cacheEnabled": true,
  "cacheTTLSeconds": 300,
  "uptimeSeconds": 42,
  "warnings": []
}
```

If the game has been patched, `status` will be `"degraded"` with a `GameVersionMismatch` warning. Generic tools (`get_row`, `search_rows`, etc.) are unaffected; FFXIV convenience tools (`get_jobs`, `get_duties`) may return incorrect data until `ServerConstants.cs` is updated.

If a schema is configured but its branch version is older than the game version, a `SchemaOutdated` warning is emitted. Call `refresh_schema` to resolve it.

---

### `refresh_schema()`

Runs `git fetch` + `git checkout -B` in the configured `schemaPath` directory to pull the latest column definitions, then clears all cached describe and response data so subsequent calls use the updated schema. Returns success or failure with a message.

```json
{ "refreshed": true, "message": "Checked out ver/2026.03.17.0000.0000." }
```

If `schemaPath` is not configured, returns a `ConfigError`. If the git operation fails, returns an `InternalError` with the git output.

---

### `list_languages()`

Returns which of the four supported languages (`en`, `fr`, `de`, `ja`) are available in this installation.

```json
{
  "languages": [
    { "code": "en", "displayName": "English",  "available": true },
    { "code": "fr", "displayName": "French",   "available": true },
    { "code": "de", "displayName": "German",   "available": true },
    { "code": "ja", "displayName": "Japanese", "available": true }
  ]
}
```

---

### `list_sheets()`

Returns the names of all game data sheets available in this installation. Sheet names are parsed from `exd/root.exl` at startup and cached for the server lifetime. The result is sorted alphabetically.

```json
{
  "count": 756,
  "sheets": ["Achievement", "Action", "BeastTribe", "ClassJob", "ContentFinderCondition", "Item", "..."]
}
```

---

### `describe_sheet(sheet)`

Returns column metadata and approximate row count for the named sheet. Use this before `get_row` or `search_rows` to understand the available fields and their types.

```json
{
  "sheet": "ClassJob",
  "rowCountApprox": 42,
  "columns": [
    { "index": 0,  "name": "Column_0",  "type": "string" },
    { "index": 1,  "name": "Column_1",  "type": "string" },
    { "index": 7,  "name": "Column_7",  "type": "uint"   },
    { "index": 30, "name": "Column_30", "type": "string" }
  ],
  "languages": ["en", "fr", "de", "ja"]
}
```

When EXDSchema is configured, `name` values are real field names (e.g. `"Name"`, `"ClassJob"`) instead of `Column_N`. Column types are reported as: `string`, `bool`, `int`, `uint`, `float`.

---

### `get_row(sheet, row_id, languages?)`

Returns all column values for a single row. String columns are returned as a language-keyed dictionary when multiple languages are requested. Non-string columns return scalar values.

```json
// Request
{ "sheet": "Item", "row_id": 2, "languages": "en,ja" }

// Response
{
  "sheet": "Item",
  "rowId": 2,
  "languagesRequested": ["en", "ja"],
  "languagesReturned": ["en", "ja"],
  "fallbackUsed": false,
  "fields": {
    "Column_0": { "en": "Weathered Shortsword", "ja": "古びたショートソード" },
    "Column_1": 1,
    "Column_5": 0
  }
}
```

`fallbackUsed: true` means a requested language was unavailable and another was substituted.

---

### `get_rows(sheet, row_ids, languages?)`

Batched `get_row`. Accepts an array of row IDs. Maximum 100 IDs per call. Missing row IDs are reported in `missingRowIds`.

```json
// Request
{ "sheet": "Item", "row_ids": [1, 2, 3], "languages": "en" }

// Response
{
  "rows": [...],
  "missingRowIds": []
}
```

---

### `search_rows(sheet, query, text_fields?, languages?, limit?, offset?, column_filters?)`

Searches all string columns (or specific ones) for a case-insensitive substring match. Optionally pre-filters rows by exact integer column values before the text match.

```json
// Request — all WHM actions containing "Holy"
{
  "sheet": "Action",
  "query": "Holy",
  "text_fields": "Column_0",
  "languages": "en",
  "column_filters": "Column_10=24"
}

// Response
{
  "sheet": "Action",
  "query": "Holy",
  "columnFilters": { "Column_10": 24 },
  "totalMatches": 2,
  "offset": 0,
  "limit": 50,
  "rowsScanned": 49000,
  "results": [...]
}
```

**Performance:** This is a full O(n) scan of the entire sheet. Use `text_fields` to restrict which string columns are searched. Use `column_filters` to eliminate rows by integer column value before the text match — this is especially effective when filtering by a reference column such as ClassJob.

| Parameter | Default | Max |
|---|---|---|
| `limit` | 50 | 200 |
| `offset` | 0 | 10 000 |

---

### `get_jobs(languages?)`

Returns all ClassJob rows (base classes and jobs) enriched with derived role groupings.

```json
{
  "jobs": [
    {
      "rowId": 19,
      "name":         { "en": "paladin", "ja": "ナイト" },
      "abbreviation": { "en": "PLD",     "ja": "ナイト" },
      "role":      "Tank",
      "isJob":     true,
      "isLimited": false,
      "jobIndex":  1
    }
  ]
}
```

> **Note:** `name` values are the internal lowercase keys stored in the game data (e.g. `"paladin"`), not the display-capitalised form.

- `isJob: false` — base class (e.g. Marauder); `isJob: true` — job (e.g. Warrior)
- `isLimited: true` — Blue Mage
- `role` values: `Tank`, `Healer`, `Melee DPS`, `Physical Ranged DPS`, `Magical Ranged DPS`, `Limited`, `None`

> **Note:** `role` labels are English-only. They are derived values, not sourced from game strings.

---

### `get_duties(category?, languages?)`

Returns ContentFinderCondition rows, optionally filtered by category. Omit `category` to return all duties.

**Categories:** `dungeon` | `trial` | `raid` | `ultimate` | `criterion` | `unreal`

```json
{
  "category": "ultimate",
  "count": 6,
  "duties": [
    {
      "rowId": 1006,
      "name": { "en": "Futures Rewritten (Ultimate)" },
      "category": "ultimate",
      "isHighEndDuty": true,
      "levelRequired": 100,
      "itemLevelRequired": 735
    }
  ]
}
```

> **Patch sensitivity:** ContentType IDs are hardcoded constants validated against a specific game version (see `ServerConstants.cs`). A `GameVersionMismatch` warning from `health()` means these filters should be re-verified before relying on them.

---

### `get_items(query?, limit?, offset?, languages?)`

Searches the Item sheet by name. Returns item level, equip level, stack size, icon, rarity, NPC price, and HQ eligibility.

| Parameter | Description |
|---|---|
| `query` | Case-insensitive name substring filter |
| `limit` / `offset` | Pagination (max limit 200) |

```json
{
  "totalMatches": 1,
  "items": [
    {
      "rowId": 4179,
      "name":       { "en": "Hi-Potion" },
      "icon":       20,
      "itemLevel":  1,
      "equipLevel": 0,
      "stackSize":  999,
      "rarity":     1,
      "filterGroup": 8,
      "priceMid":   150,
      "canBeHq":    false
    }
  ]
}
```

**Rarity values:** `1` = common (white), `2` = uncommon (green), `3` = rare (blue), `4` = relic (purple).

> **Note:** Item names are stored as singular lowercase grammatical forms (e.g. `"potion"`, not `"Potion"`).

---

### `get_actions(query?, classJobId?, limit?, offset?, languages?)`

Searches player actions (abilities, spells, weaponskills) from the Action sheet. Only `IsPlayerAction = true` rows are returned, filtering out the ~47 k NPC/system entries.

| Parameter | Description |
|---|---|
| `query` | Case-insensitive name substring filter |
| `classJobId` | Filter by ClassJob row ID (e.g. `25` for Black Mage). `0` = role/cross-class actions |
| `limit` / `offset` | Pagination (max limit 200) |

```json
{
  "totalMatches": 31,
  "actions": [
    {
      "rowId": 162,
      "name": { "en": "Flare" },
      "icon": 2652,
      "classJobId": 25,
      "classJobLevel": 50,
      "actionCategoryId": 2,
      "actionCategoryName": "Spell",
      "isRoleAction": false,
      "isPvP": false,
      "castTimeMs": 2000,
      "recastTimeMs": 2500,
      "maxCharges": 0
    }
  ]
}
```

**ActionCategory IDs:** `2` = Spell, `3` = Weaponskill, `4` = Ability. Cast and recast times are in milliseconds. `maxCharges > 1` indicates a charges-based cooldown.

---

### `get_traits(query?, classJobId?, limit?, offset?, languages?)`

Returns passive job traits from the Trait sheet. Results are ordered by level.

| Parameter | Description |
|---|---|
| `query` | Case-insensitive name substring filter |
| `classJobId` | Filter by ClassJob row ID (e.g. `24` for White Mage) |
| `limit` / `offset` | Pagination (max limit 200) |

```json
{
  "totalMatches": 12,
  "traits": [
    {
      "rowId": 47,
      "name":        { "en": "Maim and Mend" },
      "description": { "en": "Increases the potency of physical attacks by 10% and all healing magic potency by 30%." },
      "classJobId":  22,
      "level":       20
    }
  ]
}
```

---

### `get_statuses(query?, category?, limit?, offset?, languages?)`

Searches status effects (buffs and debuffs) from the Status sheet.

| Parameter | Description |
|---|---|
| `query` | Case-insensitive name substring filter |
| `category` | `beneficial` or `detrimental`. Omit for all. |
| `limit` / `offset` | Pagination (max limit 200) |

```json
{
  "totalMatches": 1,
  "statuses": [
    {
      "rowId": 17,
      "name":               { "en": "Paralysis" },
      "description":        { "en": "Unable to execute actions." },
      "icon":               215003,
      "statusCategory":     2,
      "statusCategoryName": "detrimental",
      "canDispel":          true,
      "maxStacks":          0
    }
  ]
}
```

`maxStacks = 0` means the status is not stackable.

---

### `get_mounts(query?, limit?, offset?, languages?)`

Searches mounts from the Mount sheet.

```json
{
  "totalMatches": 3,
  "mounts": [
    {
      "rowId":      1,
      "name":       { "en": "Company Chocobo" },
      "icon":       4087,
      "isFlying":   true,
      "extraSeats": 0
    }
  ]
}
```

`extraSeats > 0` indicates a multi-seat mount.

---

### `get_minions(query?, limit?, offset?, languages?)`

Searches minions (companions) from the Companion sheet.

```json
{
  "totalMatches": 1,
  "minions": [
    {
      "rowId": 8,
      "name": { "en": "Bahamut" },
      "icon": 59114
    }
  ]
}
```

---

### `get_achievements(query?, limit?, offset?, languages?)`

Searches achievements from the Achievement sheet.

```json
{
  "totalMatches": 1,
  "achievements": [
    {
      "rowId":                   1,
      "name":                    { "en": "To Crush Your Enemies I" },
      "description":             { "en": "Defeat 100 enemies." },
      "points":                  5,
      "icon":                    112001,
      "achievementCategoryName": "Battle"
    }
  ]
}
```

---

### `get_races(languages?)`

Returns all eight playable races from the Race sheet. Each race has a masculine and feminine name (which may differ by language).

**Row IDs:** `1`=Hyur, `2`=Elezen, `3`=Lalafell, `4`=Miqo'te, `5`=Roegadyn, `6`=Au Ra, `7`=Hrothgar, `8`=Viera

```json
{
  "races": [
    {
      "rowId":     4,
      "masculine": { "en": "Miqo'te", "ja": "ミコッテ" },
      "feminine":  { "en": "Miqo'te", "ja": "ミコッテ" }
    }
  ]
}
```

---

### `get_worlds(query?)`

Returns public player-accessible worlds (servers) from the World sheet, with their data centre.

```json
{
  "totalMatches": 1,
  "worlds": [
    {
      "rowId":          73,
      "name":           "Cactuar",
      "internalName":   "Cactuar",
      "dataCenterId":   8,
      "dataCenterName": "Aether",
      "isPublic":       true
    }
  ]
}
```

> **Note:** World names are proper nouns and are the same across all languages.

---

### `get_weather(query?, limit?, offset?, languages?)`

Searches weather types from the Weather sheet.

```json
{
  "totalMatches": 2,
  "weathers": [
    {
      "rowId": 7,
      "name": { "en": "Rain", "ja": "雨" },
      "icon": 60913
    }
  ]
}
```

---

### `get_titles(query?, limit?, offset?, languages?)`

Searches player titles from the Title sheet. Each title has a masculine and feminine form, and a position flag.

```json
{
  "totalMatches": 1,
  "titles": [
    {
      "rowId":     1,
      "masculine": { "en": "the Insatiable" },
      "feminine":  { "en": "the Insatiable" },
      "isPrefix":  false
    }
  ]
}
```

`isPrefix: true` — title appears before the character name (e.g. `"Warrior of Light Firstname"`). `isPrefix: false` — appears after (e.g. `"Firstname the Insatiable"`).

---

### `get_currencies(languages?)`

Returns in-game currencies (Gil, tomestones, seals, MGP, etc.) from the Item sheet. Identified by `ItemUICategory = 63`, `FilterGroup = 16`, `StackSize > 1`.

```json
{
  "currencies": [
    {
      "rowId":     1,
      "name":      { "en": "Gil" },
      "icon":      65002,
      "stackSize": 999999999
    },
    {
      "rowId":     28,
      "name":      { "en": "Allagan Tomestone of Poetics" },
      "icon":      65023,
      "stackSize": 2000
    }
  ]
}
```

`stackSize` is the per-character cap.

---

### `get_materia(query?, stat?, limit?, offset?, languages?)`

Searches materia items from the Materia sheet. Returns the stat boosted, tier (I–XII), and bonus value per materia item. Results are sorted by stat name then tier.

- **`query`** — item name substring filter (e.g. `"Savage Aim"`)
- **`stat`** — English stat name substring filter (e.g. `"Critical Hit"`, `"Gathering"`)
- Stat names in the output are always English (proper nouns, identical across locales)
- `bonus` is `0` for pre-Heavensward primary-stat materia (Strength/Dexterity/Vitality/Intelligence/Mind tiers I–VI) where per-tier values are not stored in the sheet

```json
{
  "totalMatches": 12,
  "materia": [
    {
      "rowId":       5669,
      "name":        { "en": "Savage Aim Materia I" },
      "icon":        20221,
      "stat":        "Critical Hit",
      "baseParamId": 27,
      "tier":        1,
      "bonus":       1
    },
    {
      "rowId":       41772,
      "name":        { "en": "Savage Aim Materia XII" },
      "icon":        20298,
      "stat":        "Critical Hit",
      "baseParamId": 27,
      "tier":        12,
      "bonus":       54
    }
  ]
}
```

Bonus scales vary by stat group. Some examples:

| Stat group | I | II | III | IV | V | VI | VII | VIII | IX | X | XI | XII |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Combat substats (Crit, Det, etc.) | 1 | 2 | 3 | 4 | 6 | 16 | 8 | 24 | 12 | 36 | 18 | 54 |
| Gathering / Perception | 3 | 4 | 5 | 6 | 10 | 15 | 12 | 20 | 14 | 25 | 20 | 36 |
| Craftsmanship | 3 | 4 | 5 | 6 | 11 | 16 | 14 | 21 | 18 | 27 | 22 | 33 |
| CP / GP | 1 | 2 | 3 | 4 | 6 | 8 | 7 | 9 | 8 | 10 | 9 | 11 |

---

### `get_localized_labels(kind, languages?)`

Returns label sets for well-known FFXIV enumerations, suitable for populating UI dropdowns or building lookup tables.

**kind values:** `jobs` | `roles` | `categories` | `ultimates` | `criterion` | `unreal`

```json
{
  "kind": "jobs",
  "labels": [
    { "rowId": 19, "name": { "en": "Paladin", "ja": "ナイト" } }
  ]
}
```

---

## Error Responses

All tools return structured errors on failure:

```json
{
  "code": "SheetNotFound",
  "message": "Sheet 'Foobaz' was not found in the game data.",
  "detail": null
}
```

| Code | Meaning |
|---|---|
| `ConfigError` | Invalid or missing configuration (e.g. `schemaPath` not set when calling `refresh_schema`) |
| `SheetNotFound` | Named sheet doesn't exist in the game data |
| `RowNotFound` | Row ID is not present in the sheet |
| `LanguageUnavailable` | Requested language code is invalid or not installed |
| `ValidationError` | Bad input parameter value |
| `InternalError` | Unexpected server error |

---

## Testing

### All tests

```bash
FFXIV_GAME_PATH="C:/Program Files (x86)/SquareEnix/FINAL FANTASY XIV - A Realm Reborn" dotnet test
```

### Unit tests only (no FFXIV installation required)

```bash
dotnet test --filter "Category!=Integration"
```

### Integration tests only

```bash
FFXIV_GAME_PATH="C:/..." dotnet test --filter "Category=Integration"
```

If `FFXIV_GAME_PATH` is not set, integration tests are **skipped** rather than failed — CI passes without a game installation.

### Snapshot tests

Snapshot baselines for `get_jobs` and `get_duties` (ultimates) live in `tests/McpLumina.Tests/Integration/Snapshots/`. They verify the exact shape and content of tool responses against a known-good game version.

After a patch, if snapshots fail:

- New jobs or duties added → update the snapshot files (expected drift)
- Garbled data or wrong types → column indices in `ServerConstants.cs` need updating (see Maintenance below)

---

## Maintenance: Post-Patch Runbook

Convenience tools (`get_jobs`, `get_duties`, `get_actions`, `get_items`) use `Lumina.Excel` typed sheets with named properties, so column layout changes in the game data do **not** require manual index updates. The only patch-sensitive values are the ContentType IDs used for duty category filtering.

1. **Check for new Lumina releases.** Lumina typically publishes updated NuGet packages within days of a major FFXIV patch. Update the versions in `src/McpLumina/McpLumina.csproj` and rebuild:
   ```bash
   dotnet build
   ```

2. **Run the integration tests:**
   ```bash
   FFXIV_GAME_PATH="C:/..." dotnet test --filter "Category=Integration"
   ```

3. **If duty category filtering returns wrong results**, verify the ContentType IDs in `ServerConstants.cs` (`ContentTypeIds`) still map to the correct content categories by inspecting the ContentType sheet via `get_row`.

4. **Bump `KnownGoodGameVersion.Value`** in `ServerConstants.cs` to the new game version string (from `game/ffxivgame.ver`).

5. **Run the full test suite** — all 220 tests should run (147 unit pass without a game install; 73 integration require `FFXIV_GAME_PATH`).

6. **Refresh EXDSchema** if configured:
   ```
   refresh_schema()
   ```
   Or manually: `cd <schemaPath> && git fetch && git checkout ver/<new-version>`.

7. Release with a note of the validated FFXIV patch version.

---

## Architecture Notes

### Patch resilience

Generic tools (`get_row`, `search_rows`, etc.) use Lumina's untyped `RawExcelSheet` / `RawRow` API and are not sensitive to post-patch column layout changes. FFXIV-specific convenience tools (`get_jobs`, `get_duties`, `get_actions`, `get_items`) use `Lumina.Excel` source-generated typed sheets (`ClassJob`, `ContentFinderCondition`, `Action`, `Item`) with named properties, so they are also resilient to column layout changes — only ContentType ID constants in `ServerConstants.cs` require manual verification after a patch.

### Caching

Responses are cached in-memory with a configurable TTL (default 5 minutes). The sheet list is cached for the server lifetime. `describe_sheet` results are cached in a lifetime dictionary (they don't change while the server is running) and in `ResponseCacheService`. Calling `refresh_schema` clears both caches so updated column names are reflected immediately. The cache is not persisted across restarts.

---

## Known Limitations (V1)

- **Role labels are English-only.** `get_jobs` returns `"Tank"`, `"Healer"`, etc. as hardcoded English strings, not game-sourced localised labels.
- **`get_actions` returns internal action names.** The Action sheet stores internal lowercase names; display names (with capitalisation) are not available via a simple typed field.
- **No cross-sheet link resolution.** References to other sheets (e.g. `ClassJob → ClassJobCategory`) are not automatically followed in generic tools.
- **No query DSL.** There is no SQL-style WHERE clause. Use `search_rows` for text search and `get_row`/`get_rows` for known IDs.
- **`search_rows` is a full scan.** No index. Large sheets (Action: ~49 k rows, Item: ~44 k rows) scan every row on each call.
- **ContentType IDs are hardcoded.** Duty category filtering relies on known ContentType row IDs that are patch-version-dependent.
- **stdio transport only.** HTTP/SSE transport is not supported in V1.
- **In-memory cache only.** Cache is not persisted across server restarts.

---

## Tool Contract Versioning

Tool input/output schemas follow semver conventions:

- **Patch** (1.0.x): Bug fixes only; no schema changes
- **Minor** (1.x.0): Additive changes only — new optional response fields, new tools
- **Major** (x.0.0): Breaking schema changes; migration notes provided

Consumers should treat all response fields as potentially null unless documented as required, and should ignore unknown fields for forward compatibility.
