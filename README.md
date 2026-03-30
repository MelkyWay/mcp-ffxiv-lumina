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
    "logLevel": "Information"
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

All settings can also be set via environment variables using the `McpLumina__` prefix:

```bash
McpLumina__GamePath="C:\..." McpLumina__LanguageDefault=ja dotnet run --project src/McpLumina
```

> **Note:** All logging goes to stderr. stdout is reserved exclusively for the MCP stdio frame stream.

---

## MCP Client Configuration

### Claude Desktop (`claude_desktop_config.json`)

**Using `dotnet run`** (development):

```json
{
  "mcpServers": {
    "ffxiv": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\path\\to\\mcp-ffxiv-lumina\\src\\McpLumina",
        "--no-launch-profile"
      ],
      "env": {
        "McpLumina__GamePath": "C:\\Program Files (x86)\\SquareEnix\\FINAL FANTASY XIV - A Realm Reborn"
      }
    }
  }
}
```

**Using a published binary** (recommended for production):

```bash
dotnet publish src/McpLumina -c Release -r win-x64 --self-contained -o publish/
```

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

The server exposes thirteen tools across two categories: **generic sheet tools** for arbitrary data access, and **FFXIV-specific convenience tools** for pre-shaped game entities.

### `health()`

Returns server status, game version, cache settings, and uptime. Use this to confirm the server is running correctly and to check whether the game has been patched since the server was last validated.

```json
{
  "status": "ok",
  "serverVersion": "1.0.0",
  "gamePath": "C:\\...",
  "detectedVersion": "2026.03.17.0000.0000",
  "validatedVersion": "2026.03.17.0000.0000",
  "cacheEnabled": true,
  "cacheTTLSeconds": 300,
  "uptimeSeconds": 42,
  "warnings": []
}
```

If the game has been patched since the server was validated, `status` will be `"degraded"` with a `GameVersionMismatch` warning. Generic tools (`get_row`, `search_rows`, etc.) are unaffected; FFXIV convenience tools (`get_jobs`, `get_duties`) may return incorrect data until `ServerConstants.cs` is updated.

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

Column types are reported as: `string`, `bool`, `int`, `uint`, `float`.

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

> **Note:** `role` labels are English-only in V1. They are derived values, not sourced from game strings.

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
| `ConfigError` | Invalid or missing configuration |
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

5. **Run the full test suite** — all 151 tests should pass.

6. Release with a note of the validated FFXIV patch version.

---

## Architecture Notes

### Patch resilience

Generic tools (`get_row`, `search_rows`, etc.) use Lumina's untyped `RawExcelSheet` / `RawRow` API and are not sensitive to post-patch column layout changes. FFXIV-specific convenience tools (`get_jobs`, `get_duties`, `get_actions`, `get_items`) use `Lumina.Excel` source-generated typed sheets (`ClassJob`, `ContentFinderCondition`, `Action`, `Item`) with named properties, so they are also resilient to column layout changes — only ContentType ID constants in `ServerConstants.cs` require manual verification after a patch.

### Caching

Responses are cached in-memory with a configurable TTL (default 5 minutes). The sheet list and sheet descriptions are cached for the server lifetime (they don't change while the server is running). The cache is not persisted across restarts.

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
