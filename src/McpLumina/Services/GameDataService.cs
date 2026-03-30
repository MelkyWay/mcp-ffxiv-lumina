using System.Collections.Concurrent;
using Lumina;
using Lumina.Data;
using Lumina.Excel;
using McpLumina.Configuration;
using McpLumina.Constants;
using McpLumina.Models;
using McpLumina.Models.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace McpLumina.Services;

/// <summary>
/// Central service that owns the Lumina GameData instance and orchestrates all data access.
/// Initialised once at startup; the GameData instance is a long-lived singleton that caches
/// loaded sheets in memory (Lumina's own internal cache).
/// </summary>
public sealed class GameDataService : IDisposable
{
    private readonly GameData          _gameData;
    private readonly ServerConfig      _config;
    private readonly LanguageService   _languages;
    private readonly GenericSheetReader _genericReader;
    private readonly SheetListService  _sheetList;
    private readonly SchemaService     _schema;
    private readonly Microsoft.Extensions.Logging.ILogger<GameDataService> _logger;
    private readonly DateTime          _startTime = DateTime.UtcNow;
    private readonly string            _gameVersion;

    // Lifetime caches: these values are immutable after GameData initialisation.
    private readonly Lazy<LanguagesResponse>                          _languagesCache;
    private readonly ConcurrentDictionary<string, SheetDescribeResponse> _describeCache = new();

    public GameDataService(
        IOptions<ServerConfig> options,
        SchemaService schema,
        Microsoft.Extensions.Logging.ILogger<GameDataService> logger)
    {
        _config = options.Value;
        _schema = schema;
        _logger = logger;

        var luminaOptions = new LuminaOptions
        {
            PanicOnSheetChecksumMismatch = false,
            DefaultExcelLanguage         = Language.English,
        };

        _gameData      = new GameData(ResolveSqpackPath(_config.GamePath), luminaOptions);
        _gameVersion   = ReadGameVersion();
        _languages     = BuildLanguageService();
        _genericReader = new GenericSheetReader(_gameData);
        _sheetList     = new SheetListService(_gameData, logger.ToTyped<SheetListService>());

        _languagesCache = new Lazy<LanguagesResponse>(BuildLanguagesResponse);

        _logger.LogInformation(
            "GameData initialised. Version={Version}, Languages={Langs}",
            _gameVersion,
            string.Join(",", _languages.AvailableLanguages));
    }

    public GameData Raw          => _gameData;
    public LanguageService Languages => _languages;
    public GenericSheetReader GenericReader => _genericReader;
    public string GameVersion    => _gameVersion;
    public ServerConfig GetConfig() => _config;

    // ── health() ─────────────────────────────────────────────────────────

    public HealthResponse GetHealth()
    {
        var warnings = new List<HealthWarning>();

        if (_gameVersion != KnownGoodGameVersion.Value &&
            !string.IsNullOrEmpty(_gameVersion))
        {
            warnings.Add(new HealthWarning(
                Code:             "GameVersionMismatch",
                Message:          $"Detected game version {_gameVersion} differs from last validated version " +
                                  $"{KnownGoodGameVersion.Value}. FFXIV convenience tools (get_jobs, get_duties, " +
                                  $"get_localized_labels) may return incorrect data. Generic tools (get_row, " +
                                  $"search_rows, etc.) are unaffected.",
                DetectedVersion:  _gameVersion,
                ValidatedVersion: KnownGoodGameVersion.Value));
        }

        var schemaVersion = _schema.Version;
        if (schemaVersion is not null && !string.IsNullOrEmpty(_gameVersion))
        {
            // Branch names are "ver/YYYY.MM.DD.0000.0000"; strip the prefix for comparison.
            var schemaDate = schemaVersion.StartsWith("ver/", StringComparison.OrdinalIgnoreCase)
                ? schemaVersion[4..] : schemaVersion;

            if (string.Compare(schemaDate, _gameVersion, StringComparison.OrdinalIgnoreCase) < 0)
            {
                warnings.Add(new HealthWarning(
                    Code:    "SchemaOutdated",
                    Message: $"Schema version ({schemaDate}) is behind the detected game version " +
                             $"({_gameVersion}). Column names may be incorrect for sheets changed " +
                             $"in recent patches. Call refresh_schema to update."));
            }
        }

        return new HealthResponse
        {
            Status           = warnings.Count == 0 ? "ok" : "degraded",
            ServerVersion    = ServerInfo.Version,
            GamePath         = _config.GamePath,
            DetectedVersion  = _gameVersion,
            ValidatedVersion = KnownGoodGameVersion.Value,
            SchemaAvailable  = _schema.IsAvailable,
            SchemaVersion    = _schema.Version,
            CacheEnabled     = _config.CacheEnabled,
            CacheTTLSeconds  = _config.CacheTTLSeconds,
            UptimeSeconds    = (long)(DateTime.UtcNow - _startTime).TotalSeconds,
            Warnings         = [.. warnings],
            GameVersion      = _gameVersion,
            Timestamp        = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    public (bool Success, string Message) RefreshSchema() => _schema.Refresh(_gameVersion);

    // ── list_languages() ─────────────────────────────────────────────────

    public LanguagesResponse GetLanguages() => _languagesCache.Value;

    private LanguagesResponse BuildLanguagesResponse()
    {
        var entries = KnownLanguageCodes.All.Select(code => new LanguageEntry(
            Code:        code,
            DisplayName: _languages.GetDisplayName(code),
            Available:   _languages.AvailableLanguages.Contains(code)
        )).ToArray();

        return new LanguagesResponse
        {
            Languages   = entries,
            GameVersion = _gameVersion,
            Timestamp   = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    // ── list_sheets() ────────────────────────────────────────────────────

    public SheetListResponse GetSheetList()
    {
        var names = _sheetList.GetSheetNames();
        return new SheetListResponse
        {
            Count       = names.Length,
            Sheets      = names,
            GameVersion = _gameVersion,
            Timestamp   = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    // ── describe_sheet() ─────────────────────────────────────────────────

    public SheetDescribeResponse DescribeSheet(string sheetName) =>
        _describeCache.GetOrAdd(sheetName, BuildSheetDescribeResponse);

    private SheetDescribeResponse BuildSheetDescribeResponse(string sheetName)
    {
        var sheet       = _genericReader.LoadSheet(sheetName);  // throws SheetNotFoundException
        var schemaNames = _schema.GetColumnNames(sheetName, sheet.Columns.Count);
        var columns     = _genericReader.GetColumns(sheet, schemaNames);

        // Probe which language variants are available for this sheet.
        var probe = new[] {
            ("en", Language.English), ("fr", Language.French),
            ("de", Language.German),  ("ja", Language.Japanese)
        };
        var sheetLangs = probe
            .Where(p => _languages.AvailableLanguages.Contains(p.Item1))
            .Select(p =>
            {
                try { _genericReader.LoadSheet(sheetName, p.Item2); return p.Item1; }
                catch { return null; }
            })
            .Where(l => l is not null)
            .Cast<string>()
            .ToArray();

        var schemaInfo = _schema.IsAvailable
            ? new SchemaInfo(
                Available: true,
                Version:   _schema.Version,
                Note: "Column names are derived from EXDSchema and are best-effort. " +
                      "For sheets with arrays or mixed column types (e.g. Item), the names may not " +
                      "match binary column order. Use Column_N syntax for reliable access on unknown sheets.")
            : new SchemaInfo(Available: false, Version: null, Note: null);

        return new SheetDescribeResponse
        {
            Sheet          = sheetName,
            RowCountApprox = sheet.Count,
            Columns        = columns,
            Languages      = sheetLangs.Length > 0 ? sheetLangs : ["(language-neutral)"],
            Schema         = schemaInfo,
            GameVersion    = _gameVersion,
            Timestamp      = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    // ── get_row() ────────────────────────────────────────────────────────

    public RowResponse GetRow(string sheetName, uint rowId, string[] languages, string[]? returnFields = null)
    {
        var (returned, fallback) = _languages.ApplyFallback(languages);
        var primaryCode  = returned.FirstOrDefault() ?? _config.LanguageDefault;
        var primaryLang  = _languages.ToLuminaLanguage(primaryCode);

        var sheet       = _genericReader.LoadSheet(sheetName, primaryLang);
        var schemaNames = _schema.GetColumnNames(sheetName, sheet.Columns.Count);
        var parser      = _genericReader.ReadRow(sheet, rowId)
                          ?? throw new RowNotFoundException(sheetName, rowId);

        var returnIndices = ResolveReturnFields(sheetName, returnFields, sheet.Columns.Count, schemaNames);

        var fields = _genericReader.RowToFields(
            sheet, parser, returned,
            lang => TryLoadSheet(sheetName, _languages.ToLuminaLanguage(lang)),
            schemaNames, returnIndices);

        return new RowResponse
        {
            Sheet              = sheetName,
            RowId              = rowId,
            LanguagesRequested = languages,
            LanguagesReturned  = returned,
            FallbackUsed       = fallback,
            Fields             = fields,
            GameVersion        = _gameVersion,
            Timestamp          = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    // ── get_rows() ───────────────────────────────────────────────────────

    public RowsResponse GetRows(string sheetName, uint[] rowIds, string[] languages, string[]? returnFields = null)
    {
        var (returned, fallback) = _languages.ApplyFallback(languages);
        var primaryCode = returned.FirstOrDefault() ?? _config.LanguageDefault;
        var primaryLang = _languages.ToLuminaLanguage(primaryCode);

        var sheet       = _genericReader.LoadSheet(sheetName, primaryLang);
        var schemaNames = _schema.GetColumnNames(sheetName, sheet.Columns.Count);
        var wanted      = new HashSet<uint>(rowIds);

        var returnIndices = ResolveReturnFields(sheetName, returnFields, sheet.Columns.Count, schemaNames);

        // Single pass through the sheet to collect all requested rows.
        var found = _genericReader.ReadAllRows(sheet)
            .Where(row => wanted.Contains(row.RowId))
            .ToDictionary(row => row.RowId, row => row);

        var rows       = new List<RowResponse>();
        var missingIds = new List<uint>();

        foreach (var rowId in rowIds)
        {
            if (found.TryGetValue(rowId, out var row))
            {
                var fields = _genericReader.RowToFields(
                    sheet, row, returned,
                    lang => TryLoadSheet(sheetName, _languages.ToLuminaLanguage(lang)),
                    schemaNames, returnIndices);

                rows.Add(new RowResponse
                {
                    Sheet = sheetName,
                    RowId = rowId,
                    LanguagesRequested = languages,
                    LanguagesReturned  = returned,
                    FallbackUsed       = fallback,
                    Fields             = fields,
                    GameVersion        = _gameVersion,
                    Timestamp          = DateTimeOffset.UtcNow.ToString("O"),
                });
            }
            else
            {
                missingIds.Add(rowId);
            }
        }

        return new RowsResponse
        {
            Sheet              = sheetName,
            RowIds             = rowIds,
            LanguagesRequested = languages,
            LanguagesReturned  = returned,
            FallbackUsed       = fallback,
            Rows               = [.. rows],
            MissingRowIds      = [.. missingIds],
            GameVersion        = _gameVersion,
            Timestamp          = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    // ── search_rows() ────────────────────────────────────────────────────

    public SearchResponse SearchRows(
        string                  sheetName,
        string                  query,
        string[]                textFields,
        Dictionary<string, long> rawFilters,
        string[]                languages,
        int                     limit,
        int                     offset,
        string[]?               returnFields = null)
    {
        var (returned, fallback) = _languages.ApplyFallback(languages);
        var primaryCode = returned.FirstOrDefault() ?? _config.LanguageDefault;
        var primaryLang = _languages.ToLuminaLanguage(primaryCode);

        var sheet       = _genericReader.LoadSheet(sheetName, primaryLang);
        var columns     = sheet.Columns;
        var schemaNames = _schema.GetColumnNames(sheetName, columns.Count);

        // Resolve named filters and return fields to column indices.
        var columnFilters = ResolveColumnFilters(sheetName, rawFilters, columns.Count, schemaNames);
        var returnIndices = ResolveReturnFields(sheetName, returnFields, columns.Count, schemaNames);

        // Resolve textFields to a set of column indices (supports schema names and Column_N).
        HashSet<int>? textFieldFilter = null;
        if (textFields.Length > 0)
        {
            textFieldFilter = [];
            foreach (var f in textFields)
            {
                var idx = ResolveFieldName(f, columns.Count, schemaNames);
                if (idx >= 0) textFieldFilter.Add(idx);
                // Unknown text field names are silently ignored (field may not be a string column).
            }
        }

        var textColIndices = columns
            .Select((col, i) => (col, i))
            .Where(x => x.col.Type == Lumina.Data.Structs.Excel.ExcelColumnDataType.String)
            .Where(x => textFieldFilter is null || textFieldFilter.Contains(x.i))
            .Select(x => x.i)
            .ToHashSet();

        var lowerQuery = query.ToLowerInvariant();
        var matches    = new List<RowResponse>();
        int scanned    = 0;

        foreach (var row in _genericReader.ReadAllRows(sheet))
        {
            scanned++;

            // Apply column filters first — cheap integer comparisons cut rows before text matching.
            if (columnFilters.Count > 0)
            {
                bool passesFilter = true;
                foreach (var (colIdx, expected) in columnFilters)
                {
                    if (colIdx >= columns.Count) { passesFilter = false; break; }
                    var val = _genericReader.ReadColumnValue(row, columns[colIdx], colIdx);
                    try   { if (val is null || Convert.ToInt64(val) != expected) { passesFilter = false; break; } }
                    catch (Exception ex) when (ex is InvalidCastException or OverflowException or FormatException)
                          { passesFilter = false; break; }
                }
                if (!passesFilter) continue;
            }

            bool isMatch = false;
            foreach (var colIdx in textColIndices)
            {
                var value = _genericReader.ReadColumnValue(row, columns[colIdx], colIdx) as string;
                if (value is not null && value.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase))
                {
                    isMatch = true;
                    break;
                }
            }

            if (!isMatch) continue;

            var fields = _genericReader.RowToFields(
                sheet, row, returned,
                lang => TryLoadSheet(sheetName, _languages.ToLuminaLanguage(lang)),
                schemaNames, returnIndices);

            matches.Add(new RowResponse
            {
                Sheet              = sheetName,
                RowId              = row.RowId,
                LanguagesRequested = languages,
                LanguagesReturned  = returned,
                FallbackUsed       = fallback,
                Fields             = fields,
                GameVersion        = _gameVersion,
                Timestamp          = DateTimeOffset.UtcNow.ToString("O"),
            });
        }

        var total   = matches.Count;
        var results = matches.Skip(offset).Take(limit).ToArray();

        return new SearchResponse
        {
            Sheet              = sheetName,
            Query              = query,
            ColumnFilters      = rawFilters.Count > 0 ? rawFilters : null,
            LanguagesRequested = languages,
            LanguagesReturned  = returned,
            FallbackUsed       = fallback,
            TotalMatches       = total,
            Offset             = offset,
            Limit              = limit,
            RowsScanned        = scanned,
            Results            = results,
            GameVersion        = _gameVersion,
            Timestamp          = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    // ── Field name resolution helpers ────────────────────────────────────

    /// <summary>
    /// Resolves a field name to its column index.
    /// Accepts "Column_N" (positional) or a schema name (when schema is loaded).
    /// Returns -1 if the name cannot be resolved.
    /// </summary>
    private static int ResolveFieldName(string field, int columnCount, string[]? schemaNames)
    {
        // Column_N format always works regardless of schema.
        if (field.StartsWith("Column_", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(field["Column_".Length..], out var idx) &&
            idx >= 0 && idx < columnCount)
            return idx;

        // Schema name lookup (case-insensitive).
        if (schemaNames is not null)
        {
            for (int i = 0; i < schemaNames.Length && i < columnCount; i++)
            {
                if (string.Equals(schemaNames[i], field, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
        }

        return -1;
    }

    /// <summary>Resolves return_fields strings to a set of column indices, or null (= all columns).</summary>
    private HashSet<int>? ResolveReturnFields(
        string sheetName, string[]? returnFields, int columnCount, string[]? schemaNames)
    {
        if (returnFields is null || returnFields.Length == 0) return null;

        var indices = new HashSet<int>();
        foreach (var field in returnFields)
        {
            var idx = ResolveFieldName(field, columnCount, schemaNames);
            if (idx < 0)
                throw new ValidationException(
                    $"Field '{field}' not found in sheet '{sheetName}'. " +
                    "Use describe_sheet to see available field names.");
            indices.Add(idx);
        }
        return indices;
    }

    /// <summary>Resolves raw column filter keys (field names) to column indices.</summary>
    private Dictionary<int, long> ResolveColumnFilters(
        string sheetName, Dictionary<string, long> rawFilters, int columnCount, string[]? schemaNames)
    {
        if (rawFilters.Count == 0) return [];

        var resolved = new Dictionary<int, long>(rawFilters.Count);
        foreach (var (key, value) in rawFilters)
        {
            var idx = ResolveFieldName(key, columnCount, schemaNames);
            if (idx < 0)
                throw new ValidationException(
                    $"Column filter field '{key}' not found in sheet '{sheetName}'. " +
                    "Use describe_sheet to see available field names.");
            resolved[idx] = value;
        }
        return resolved;
    }

    // ── Sheet load helpers ────────────────────────────────────────────────

    private RawExcelSheet? TryLoadSheet(string name, Language lang)
    {
        try { return _genericReader.LoadSheet(name, lang); }
        catch { return null; }
    }

    private static string ResolveSqpackPath(string gamePath)
    {
        // Lumina requires the sqpack directory itself, not the game root.
        // Support both layouts:
        //   <root>/game/sqpack  (standard retail install)
        //   <root>/sqpack       (already pointing at game/)
        //   <root>              (already the sqpack directory)
        var candidates = new[]
        {
            Path.Combine(gamePath, "game", "sqpack"),
            Path.Combine(gamePath, "sqpack"),
            gamePath,
        };
        return candidates.FirstOrDefault(Directory.Exists) ?? gamePath;
    }

    private string ReadGameVersion()
    {
        var candidates = new[]
        {
            Path.Combine(_config.GamePath, "game", "ffxivgame.ver"),
            Path.Combine(_config.GamePath, "ffxivgame.ver"),
        };

        var path = candidates.FirstOrDefault(File.Exists);
        if (path is not null)
        {
            var version = File.ReadAllText(path).Trim();
            _logger.LogInformation("Detected game version: {Version}", version);
            return version;
        }

        _logger.LogWarning("Could not detect game version (ffxivgame.ver not found).");
        return "unknown";
    }

    private LanguageService BuildLanguageService()
    {
        var available = new HashSet<string>();
        var probe     = new[] { ("en", Language.English), ("fr", Language.French),
                                ("de", Language.German),  ("ja", Language.Japanese) };

        foreach (var (code, lang) in probe)
        {
            try
            {
                var sheet = _gameData.Excel.GetRawSheet("Action", lang);
                if (sheet is not null) available.Add(code);
            }
            catch { /* language not available */ }
        }

        if (available.Count == 0)
        {
            _logger.LogWarning("Language detection returned no results; defaulting to English.");
            available.Add("en");
        }

        return new LanguageService(available, _config.LanguageDefault.ToLowerInvariant());
    }

    public void Dispose() => _gameData?.Dispose();
}

internal static class LoggerExtensions
{
    /// <summary>Wraps a logger as a typed logger for a different category (avoids creating a new factory).</summary>
    public static Microsoft.Extensions.Logging.ILogger<T> ToTyped<T>(
        this Microsoft.Extensions.Logging.ILogger logger) =>
        new TypedLoggerWrapper<T>(logger);

    private sealed class TypedLoggerWrapper<T>(Microsoft.Extensions.Logging.ILogger inner)
        : Microsoft.Extensions.Logging.ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            inner.Log(logLevel, eventId, state, exception, formatter);
    }
}
