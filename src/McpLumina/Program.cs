using McpLumina.Configuration;
using McpLumina.Services;
using McpLumina.Tools;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using ModelContextProtocol.Server;

// ── Build host ────────────────────────────────────────────────────────────────

var builder = Host.CreateApplicationBuilder(args);

// Configuration sources: appsettings.json → environment variables → command-line
// Use AppContext.BaseDirectory (the output directory) so the file is found regardless
// of what working directory the host process (e.g. Claude Desktop) uses.
builder.Configuration
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

builder.Services.Configure<ServerConfig>(
    builder.Configuration.GetSection(ServerConfig.SectionName));

// Logging: write to stderr so stdout stays clean for MCP stdio transport
var logLevelStr = builder.Configuration.GetSection(ServerConfig.SectionName)["LogLevel"] ?? "Information";
var configuredLogLevel = Enum.TryParse<LogLevel>(logLevelStr, ignoreCase: true, out var lvl)
    ? lvl : LogLevel.Information;

builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(configuredLogLevel);
builder.Logging.AddSimpleConsole(opts =>
{
    opts.UseUtcTimestamp   = true;
    opts.TimestampFormat   = "HH:mm:ss ";
    opts.SingleLine        = true;
});
builder.Logging.AddFilter("Microsoft.Hosting", LogLevel.Warning);
builder.Logging.AddFilter("System.Net.Http", LogLevel.Warning);

// Redirect all logging to stderr (MCP stdio transport owns stdout)
builder.Services.Configure<ConsoleLoggerOptions>(opts =>
    opts.LogToStandardErrorThreshold = LogLevel.Trace);

// ── Services ──────────────────────────────────────────────────────────────────

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ResponseCacheService>();
builder.Services.AddSingleton<ConfigValidator>();
builder.Services.AddSingleton<SchemaService>();
builder.Services.AddSingleton<GameDataService>();
builder.Services.AddSingleton<SupplementalDataService>();

// ── MCP server ────────────────────────────────────────────────────────────────

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

// ── Build and validate ────────────────────────────────────────────────────────

var host = builder.Build();

// Run config validation before starting the server — fail fast with actionable error
var validator = host.Services.GetRequiredService<ConfigValidator>();
var config    = host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ServerConfig>>().Value;

try
{
    validator.ValidateOrThrow(config);
}
catch (McpLumina.Models.ConfigException ex)
{
    await Console.Error.WriteLineAsync(ex.Message);
    return 1;
}

// Eagerly initialise GameDataService so startup errors surface before first tool call
try
{
    _ = host.Services.GetRequiredService<GameDataService>();
}
catch (Exception ex)
{
    await Console.Error.WriteLineAsync($"Failed to initialise game data: {ex.Message}");
    return 1;
}

// Eagerly initialise SupplementalDataService (loads CSV assets + builds name indices)
try
{
    _ = host.Services.GetRequiredService<SupplementalDataService>();
}
catch (Exception ex)
{
    await Console.Error.WriteLineAsync($"Failed to initialise supplemental data: {ex.Message}");
    return 1;
}

await host.RunAsync();
return 0;
