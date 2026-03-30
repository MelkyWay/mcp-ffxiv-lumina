using System.Text.Json;
using System.Text.Json.Serialization;
using McpLumina.Models;

namespace McpLumina.Tools;

/// <summary>
/// Shared JSON serialisation options and error-wrapping helpers for tool methods.
/// </summary>
internal static class ToolHelper
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented          = false,
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters             = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>
    /// Serialises a result object to a JSON string.
    /// </summary>
    internal static string Ok<T>(T result) => JsonSerializer.Serialize(result, JsonOptions);

    /// <summary>
    /// Serialises an error using the standard McpError contract.
    /// All tool methods call this for any known exception type.
    /// </summary>
    internal static string Err(ErrorCode code, string message, string? detail = null) =>
        JsonSerializer.Serialize(McpError.From(code, message, detail), JsonOptions);

    /// <summary>
    /// Wraps a tool body in a try/catch that maps known exception types to structured errors.
    /// Unknown exceptions are mapped to InternalError.
    /// </summary>
    internal static string Execute(Func<string> body)
    {
        try
        {
            return body();
        }
        catch (ConfigException ex)
        {
            return Err(ErrorCode.ConfigError, ex.Message);
        }
        catch (SheetNotFoundException ex)
        {
            return Err(ErrorCode.SheetNotFound, ex.Message);
        }
        catch (RowNotFoundException ex)
        {
            return Err(ErrorCode.RowNotFound, ex.Message);
        }
        catch (LanguageUnavailableException ex)
        {
            return Err(ErrorCode.LanguageUnavailable, ex.Message);
        }
        catch (ValidationException ex)
        {
            return Err(ErrorCode.ValidationError, ex.Message);
        }
        catch (Exception ex)
        {
            return Err(ErrorCode.InternalError, "An unexpected error occurred.", ex.Message);
        }
    }

}
