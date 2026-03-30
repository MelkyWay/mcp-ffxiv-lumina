namespace McpLumina.Models;

/// <summary>
/// Structured error response returned as JSON text from every tool on failure.
/// </summary>
public sealed record McpError(
    string Code,
    string Message,
    string? Detail = null
)
{
    public static McpError From(ErrorCode code, string message, string? detail = null) =>
        new(code.ToString(), message, detail);
}

// ── Typed exceptions that tools catch and convert to McpError ──────────────

public sealed class ConfigException(string message) : Exception(message);

public sealed class SheetNotFoundException(string sheet)
    : Exception($"Sheet '{sheet}' was not found in the game data.");

public sealed class RowNotFoundException(string sheet, uint rowId)
    : Exception($"Row {rowId} was not found in sheet '{sheet}'.");

public sealed class LanguageUnavailableException(string language)
    : Exception($"Language '{language}' is not available in this game installation.");

public sealed class ValidationException(string message) : Exception(message);
