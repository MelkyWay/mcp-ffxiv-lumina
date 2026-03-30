using Lumina;
using Lumina.Data;
using Lumina.Data.Structs.Excel;
using Lumina.Excel;
using McpLumina.Models;
using McpLumina.Models.Responses;

namespace McpLumina.Services;

/// <summary>
/// Provides untyped row access for any FFXIV game data sheet via Lumina's RawExcelSheet API.
///
/// Uses ExcelModule.GetRawSheet() which returns a RawExcelSheet — the non-generic base
/// that works for any sheet without requiring generated C# type definitions. This makes
/// the generic tools (describe_sheet, get_row, search_rows) resilient to sheet type
/// regeneration between FFXIV patches.
///
/// Row access and iteration are done via ExcelSheet&lt;RawRow&gt;. Column reads use
/// RawRow.ReadColumn(int) and the typed ReadXxxColumn(int) helpers.
/// </summary>
public sealed class GenericSheetReader(GameData gameData)
{
    // ── Column type → contract string mapping ─────────────────────────────

    internal static string ColumnTypeToString(ExcelColumnDataType type) => type switch
    {
        ExcelColumnDataType.String  => "string",
        ExcelColumnDataType.Bool    => "bool",
        ExcelColumnDataType.Int8    => "int",
        ExcelColumnDataType.Int16   => "int",
        ExcelColumnDataType.Int32   => "int",
        ExcelColumnDataType.Int64   => "int",
        ExcelColumnDataType.UInt8   => "uint",
        ExcelColumnDataType.UInt16  => "uint",
        ExcelColumnDataType.UInt32  => "uint",
        ExcelColumnDataType.UInt64  => "uint",
        ExcelColumnDataType.Float32 => "float",
        >= ExcelColumnDataType.PackedBool0 and
           <= ExcelColumnDataType.PackedBool7 => "bool",
        _ => "unknown",
    };

    // ── Sheet access ──────────────────────────────────────────────────────

    /// <summary>Returns a RawExcelSheet for the named sheet, or throws SheetNotFoundException.</summary>
    public RawExcelSheet LoadSheet(string sheetName, Language language = Language.None)
    {
        try
        {
            var sheet = language == Language.None
                ? gameData.Excel.GetRawSheet(sheetName)
                : gameData.Excel.GetRawSheet(sheetName, language);

            if (sheet is null)
                throw new SheetNotFoundException(sheetName);

            return sheet;
        }
        catch (Lumina.Excel.Exceptions.SheetNotFoundException)
        {
            // Lumina 7 throws its own SheetNotFoundException for unknown sheets;
            // wrap it in ours so callers see a consistent type.
            throw new SheetNotFoundException(sheetName);
        }
    }

    public ColumnInfo[] GetColumns(RawExcelSheet sheet, string[]? schemaNames = null)
    {
        return sheet.Columns
            .Select((col, i) => new ColumnInfo(i, schemaNames?[i] ?? $"Column_{i}", ColumnTypeToString(col.Type)))
            .ToArray();
    }

    // ── Row iteration ─────────────────────────────────────────────────────

    /// <summary>Iterates all RawRow instances for the sheet. Each row has RowId set.</summary>
    public IEnumerable<RawRow> ReadAllRows(RawExcelSheet sheet)
    {
        foreach (var row in new ExcelSheet<RawRow>(sheet))
            yield return row;
    }

    /// <summary>Returns a RawRow for a single row, or null if the row doesn't exist.</summary>
    public RawRow? ReadRow(RawExcelSheet sheet, uint rowId) =>
        new ExcelSheet<RawRow>(sheet).GetRowOrDefault(rowId);

    // ── Column value reading ──────────────────────────────────────────────

    /// <summary>
    /// Reads a column value from a row as a plain object.
    /// String columns return the raw string (ReadOnlySeString.ToString()), others return
    /// their natural CLR type.
    /// </summary>
    public object? ReadColumnValue(RawRow row, ExcelColumnDefinition col, int index)
    {
        try
        {
            var raw = row.ReadColumn(index);
            if (raw is Lumina.Text.ReadOnly.ReadOnlySeString rss) return rss.ToString();
            return raw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Builds the fields dictionary for a row response.
    /// String columns are returned as a per-language map when multiple languages are requested.
    /// </summary>
    /// <param name="columnNames">Optional schema names; when provided, keys use real field names instead of Column_N.</param>
    /// <param name="returnFieldIndices">When non-null, only the listed column indices are included in the output.</param>
    public Dictionary<string, object?> RowToFields(
        RawExcelSheet primarySheet,
        RawRow primaryRow,
        string[] languages,
        Func<string, RawExcelSheet?> getSheetForLanguage,
        string[]? columnNames = null,
        HashSet<int>? returnFieldIndices = null)
    {
        var columns  = primarySheet.Columns;
        var capacity = returnFieldIndices?.Count ?? columns.Count;
        var fields   = new Dictionary<string, object?>(capacity);

        for (int i = 0; i < columns.Count; i++)
        {
            if (returnFieldIndices is not null && !returnFieldIndices.Contains(i))
                continue;

            var col    = columns[i];
            var name   = columnNames?[i] ?? $"Column_{i}";
            var isText = col.Type == ExcelColumnDataType.String;

            if (isText && languages.Length > 1)
            {
                var langMap = new Dictionary<string, string?>();
                foreach (var lang in languages)
                {
                    var langSheet = getSheetForLanguage(lang);
                    if (langSheet is null) { langMap[lang] = null; continue; }
                    var langRow = ReadRow(langSheet, primaryRow.RowId);
                    langMap[lang] = langRow is null
                        ? null
                        : ReadColumnValue(langRow.Value, col, i) as string;
                }
                fields[name] = langMap;
            }
            else
            {
                fields[name] = ReadColumnValue(primaryRow, col, i);
            }
        }

        return fields;
    }
}
