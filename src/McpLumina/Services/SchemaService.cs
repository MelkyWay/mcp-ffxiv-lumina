using System.Collections.Concurrent;
using McpLumina.Configuration;
using McpLumina.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace McpLumina.Services;

/// <summary>
/// Loads EXDSchema YAML definitions from a local clone of https://github.com/xivdev/EXDSchema
/// and maps each sheet's column indices to their real field names.
///
/// Schema files live at {SchemaPath}/{SheetName}.yml on version-specific branches
/// (e.g. ver/2026.03.17.0000.0000). When SchemaPath is not configured or a sheet has
/// no schema file, all methods return null and callers fall back to "Column_N" naming.
/// </summary>
public sealed class SchemaService
{
    private readonly string?  _schemaRoot;
    private readonly ILogger<SchemaService> _logger;

    // Null entry = schema file absent or parse failed for this sheet.
    private readonly ConcurrentDictionary<string, string[]?> _cache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public bool    IsAvailable => _schemaRoot is not null;
    public string? Version     => _schemaRoot is null ? null : _version;

    private string? _version;

    public SchemaService(IOptions<ServerConfig> options, ILogger<SchemaService> logger)
    {
        _logger = logger;

        var path = options.Value.SchemaPath;
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (Directory.Exists(path))
        {
            _schemaRoot = path;
            _version    = ReadGitBranch(_schemaRoot);
            _logger.LogInformation("Schema service initialised from {Path}", path);
        }
        else
        {
            _logger.LogWarning("SchemaPath '{Path}' does not exist; column naming disabled.", path);
        }
    }

    // ── Git helpers ───────────────────────────────────────────────────────

    /// <summary>Runs a git command in the schema root. Returns (exitCode==0, stdout-or-stderr).</summary>
    private (bool Success, string Output) RunGit(params string[] args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git")
            {
                RedirectStandardInput  = true,  // isolate from parent stdin (MCP transport)
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
            };
            psi.ArgumentList.Add("-C");
            psi.ArgumentList.Add(_schemaRoot!);
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var proc = System.Diagnostics.Process.Start(psi)!;
            var stdout = proc.StandardOutput.ReadToEnd().Trim();
            var stderr = proc.StandardError.ReadToEnd().Trim();
            proc.WaitForExit();
            return (proc.ExitCode == 0, stdout.Length > 0 ? stdout : stderr);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private string? ReadGitBranch(string? root)
    {
        if (root is null) return null;
        try
        {
            var (ok, output) = RunGit("rev-parse", "--abbrev-ref", "HEAD");
            return ok && output.Length > 0 ? output : null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read schema git branch.");
            return null;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Fetches remote branches, checks out the branch matching <paramref name="gameVersion"/>
    /// (e.g. <c>ver/2026.03.17.0000.0000</c>), falling back to <c>origin/latest</c> if that
    /// branch does not exist yet. Clears the column-name cache afterwards.
    /// Returns (success, message).
    /// </summary>
    public (bool Success, string Message, ErrorCode ErrorCode) Refresh(string? gameVersion = null)
    {
        if (_schemaRoot is null)
            return (false, "Schema path is not configured. Set SchemaPath in appsettings.json.", ErrorCode.ConfigError);

        // Fetch so local tracking refs are up to date.
        var fetch = RunGit("fetch", "origin");
        if (!fetch.Success)
            return (false, $"git fetch failed: {fetch.Output}", ErrorCode.InternalError);

        // Determine which branch to check out.
        string localBranch, remoteBranch, note;

        var versionBranch = gameVersion is not null ? $"ver/{gameVersion}" : null;
        var versionExists = versionBranch is not null
            && RunGit("rev-parse", "--verify", $"origin/{versionBranch}").Success;

        if (versionExists)
        {
            localBranch  = versionBranch!;
            remoteBranch = $"origin/{versionBranch}";
            note         = $"Checked out {versionBranch}.";
        }
        else
        {
            localBranch  = "latest";
            remoteBranch = "origin/latest";
            note         = versionBranch is not null
                ? $"ver/{gameVersion} not yet available; using origin/latest."
                : "Using origin/latest.";
        }

        // Create or reset the local branch to match the remote.
        var checkout = RunGit("checkout", "-B", localBranch, remoteBranch);
        if (!checkout.Success)
            return (false, $"git checkout failed: {checkout.Output}", ErrorCode.InternalError);

        _cache.Clear();
        _version = ReadGitBranch(_schemaRoot);
        _logger.LogInformation("Schema refreshed: {Note}", note);
        return (true, note, ErrorCode.InternalError); // ErrorCode unused on success
    }

    /// <summary>
    /// Returns a string[] of length <paramref name="columnCount"/> mapping each column index
    /// to its schema name, or null if no schema is available for this sheet.
    /// Entries for columns beyond the schema definition fall back to "Column_N".
    /// </summary>
    public string[]? GetColumnNames(string sheetName, int columnCount)
    {
        if (_schemaRoot is null) return null;
        // Use GetOrAdd; factory may be called more than once under contention but will
        // always produce the same result since column count is fixed per sheet.
        return _cache.GetOrAdd(sheetName, s => LoadSchema(s, columnCount));
    }

    // ── Private: loading + flattening ─────────────────────────────────────

    private string[]? LoadSchema(string sheetName, int columnCount)
    {
        var path = Path.Combine(_schemaRoot!, $"{sheetName}.yml");
        if (!File.Exists(path)) return null;

        try
        {
            var yaml   = File.ReadAllText(path);
            var schema = _deserializer.Deserialize<SchemaDocument>(yaml);

            var names = FlattenFields(schema.Fields ?? [], columnCount);
            _logger.LogDebug("Schema loaded for sheet '{Sheet}': {Count} named columns.", sheetName, columnCount);
            return names;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to parse schema for sheet '{Sheet}'; falling back to Column_N names.", sheetName);
            return null;
        }
    }

    private static string[] FlattenFields(List<SchemaField> fields, int columnCount)
    {
        // Pre-fill with Column_N fallbacks; schema names overwrite as we walk.
        var result = new string[columnCount];
        for (int i = 0; i < columnCount; i++)
            result[i] = $"Column_{i}";

        int col = 0;
        FlattenInto(fields, prefix: "", result, ref col, columnCount);
        return result;
    }

    private static void FlattenInto(
        List<SchemaField> fields, string prefix,
        string[] result, ref int col, int columnCount)
    {
        foreach (var field in fields)
        {
            if (col >= columnCount) return;

            if (!string.Equals(field.Type, "array", StringComparison.OrdinalIgnoreCase))
            {
                // Scalar / link / icon / modelId / color — one column.
                var currentCol = col++;
                result[currentCol] = string.IsNullOrEmpty(field.Name)
                    ? $"Column_{currentCol}"
                    : prefix + field.Name;
            }
            else
            {
                // Array: expand count times.
                var count     = field.Count > 0 ? field.Count : 1;
                var subFields = field.Fields ?? [];

                // Simple flat array (no named sub-fields): Name[0], Name[1], …
                var isSimple = subFields.Count == 0
                    || (subFields.Count == 1 && string.IsNullOrEmpty(subFields[0].Name));

                for (int i = 0; i < count && col < columnCount; i++)
                {
                    if (isSimple)
                    {
                        result[col++] = prefix + field.Name + $"[{i}]";
                    }
                    else
                    {
                        // Struct array: recurse with "Name[i]" prefix.
                        FlattenInto(subFields, prefix + field.Name + $"[{i}]", result, ref col, columnCount);
                    }
                }
            }
        }
    }

    // ── POCO models for YAML deserialization ──────────────────────────────

    private sealed class SchemaDocument
    {
        public List<SchemaField>? Fields { get; set; }
    }

    private sealed class SchemaField
    {
        public string? Name  { get; set; }
        public string  Type  { get; set; } = "scalar";
        public int     Count { get; set; } = 1;
        public List<SchemaField>? Fields { get; set; }
    }
}
