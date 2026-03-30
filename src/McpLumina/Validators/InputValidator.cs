using System.Text.RegularExpressions;
using McpLumina.Constants;
using McpLumina.Models;

namespace McpLumina.Validators;

/// <summary>
/// Centralised input validation for all tool parameters.
/// All methods throw <see cref="ValidationException"/> on bad input.
/// </summary>
public static partial class InputValidator
{
    // Sheet and column names: alphanumeric, underscores, and forward slashes (for sub-sheets)
    [GeneratedRegex(@"^[A-Za-z0-9_/]+$")]
    private static partial Regex SheetNamePattern();

    public static void ValidateSheetName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Sheet name must not be empty.");

        if (name.Length > 128)
            throw new ValidationException($"Sheet name '{name}' exceeds maximum length of 128 characters.");

        if (!SheetNamePattern().IsMatch(name))
            throw new ValidationException(
                $"Sheet name '{name}' contains invalid characters. " +
                "Only alphanumeric characters, underscores, and forward slashes are allowed.");
    }

    public static int ValidateLimit(int? limit)
    {
        if (limit is null) return Limits.DefaultLimit;

        if (limit < 1)
            throw new ValidationException("limit must be >= 1.");

        if (limit > Limits.MaxResultLimit)
            throw new ValidationException($"limit must be <= {Limits.MaxResultLimit}.");

        return limit.Value;
    }

    public static int ValidateOffset(int? offset)
    {
        if (offset is null) return 0;

        if (offset < 0)
            throw new ValidationException("offset must be >= 0.");

        if (offset > Limits.MaxOffset)
            throw new ValidationException($"offset must be <= {Limits.MaxOffset}.");

        return offset.Value;
    }

    public static void ValidateBatchSize(uint[] rowIds)
    {
        if (rowIds.Length == 0)
            throw new ValidationException("row_ids must contain at least one ID.");

        if (rowIds.Length > Limits.MaxBatchRowIds)
            throw new ValidationException(
                $"row_ids contains {rowIds.Length} IDs; maximum batch size is {Limits.MaxBatchRowIds}.");
    }

    public static void ValidateSearchQuery(string? query)
    {
        if (string.IsNullOrEmpty(query))
            throw new ValidationException("query must not be empty.");

        if (query.Length > 256)
            throw new ValidationException("query must be <= 256 characters.");
    }

    public static void ValidateDutyCategory(string? category)
    {
        if (category is null) return;  // null = all categories

        if (!DutyCategories.All.Contains(category.ToLowerInvariant()))
            throw new ValidationException(
                $"category '{category}' is not valid. " +
                $"Allowed values: {string.Join(", ", DutyCategories.All)}.");
    }

    public static int ValidateClassJobId(int? classJobId)
    {
        if (classJobId is null) return -1;

        if (classJobId < 0)
            throw new ValidationException("classJobId must be >= 0.");

        return classJobId.Value;
    }

    public static void ValidateLabelKind(string kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
            throw new ValidationException("kind must not be empty.");

        if (!LabelKinds.All.Contains(kind.ToLowerInvariant()))
            throw new ValidationException(
                $"kind '{kind}' is not valid. " +
                $"Allowed values: {string.Join(", ", LabelKinds.All)}.");
    }

    /// <summary>
    /// Parses a comma-separated language string like "en,fr" into an array.
    /// Returns empty array if null or empty (callers treat empty as "use default").
    /// </summary>
    public static string[] ParseLanguages(string? languagesParam)
    {
        if (string.IsNullOrWhiteSpace(languagesParam))
            return [];

        return languagesParam
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(l => l.ToLowerInvariant())
            .Distinct()
            .ToArray();
    }

    /// <summary>
    /// Parses a column filter string like "ClassJob=24,Column_12=2" into a raw dictionary
    /// mapping field name (schema name or "Column_N") to the expected integer value.
    /// Resolution of field names to column indices happens in GameDataService once the
    /// sheet's schema is known.
    /// </summary>
    public static Dictionary<string, long> ParseColumnFilters(string? filtersParam)
    {
        if (string.IsNullOrWhiteSpace(filtersParam))
            return [];

        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in filtersParam.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=');
            if (eq < 0)
                throw new ValidationException(
                    $"Column filter '{part}' is not valid. Expected format: FieldName=value.");

            var fieldName = part[..eq].Trim();
            var valStr    = part[(eq + 1)..].Trim();

            if (string.IsNullOrEmpty(fieldName))
                throw new ValidationException("Column filter field name must not be empty.");

            if (!long.TryParse(valStr, out var colVal))
                throw new ValidationException(
                    $"Column filter value '{valStr}' for '{fieldName}' is not a valid integer.");

            if (result.ContainsKey(fieldName))
                throw new ValidationException(
                    $"Column filter '{fieldName}' appears more than once.");

            result[fieldName] = colVal;
        }
        return result;
    }

    /// <summary>
    /// Parses a comma-separated return_fields string like "Name,ClassJob,Column_3".
    /// Returns empty array if null (means: return all fields).
    /// Field name resolution against the sheet schema happens in GameDataService.
    /// </summary>
    public static string[] ParseReturnFields(string? param)
    {
        if (string.IsNullOrWhiteSpace(param))
            return [];

        return param
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }

    /// <summary>
    /// Parses a comma-separated text fields string like "Column_0,Column_3".
    /// Returns empty array if null (means: search all string columns).
    /// </summary>
    public static string[] ParseTextFields(string? textFieldsParam)
    {
        if (string.IsNullOrWhiteSpace(textFieldsParam))
            return [];

        return textFieldsParam
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }
}
