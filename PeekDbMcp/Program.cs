using Serilog;
using PeekDbMcp.Configuration;
using PeekDbMcp.Core;
using PeekDbMcp.Providers;
using PeekDbMcp.Tools;

try
{
    var settings = AppSettings.Load();

    // Log to file (AppContext.BaseDirectory = output folder like bin/Debug/net8.0)
    var logDir = Path.Combine(AppContext.BaseDirectory, "Logs");
    Directory.CreateDirectory(logDir);
    var logPath = Path.Combine(logDir, "mcp-.log");

    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .WriteTo.File(logPath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
        .CreateLogger();

    Log.Information("=== PeekDbMCP starting ===");

    // Console.Error.WriteLine goes to stderr - safe for startup messages before MCP takes over
    Console.Error.WriteLine("[PeekDbMCP] Server starting...");
    Console.Error.WriteLine($"[PeekDbMCP] Connection: {MaskConnectionString(settings.ConnectionString)}");

    // Determine provider from explicit setting or auto-detect
    var explicitProvider = Environment.GetEnvironmentVariable("PEEKDB_PROVIDER");
    var detectedProvider = explicitProvider ?? ProviderFactory.DetectProvider(settings.ConnectionString) ?? "Unknown";
    Console.Error.WriteLine($"[PeekDbMCP] Provider: {detectedProvider}");

    var db = ProviderFactory.Create(settings.ConnectionString, explicitProvider);
    var toolHandler = new ToolHandler(db);
    var dispatcher = new McpDispatcher(toolHandler);

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    Console.Error.WriteLine("[PeekDbMCP] Ready - listening for MCP requests...");
    await dispatcher.RunAsync(cts.Token);
}
catch (Exception ex)
{
    Log.Fatal(ex, "MCP server crashed");
    await Console.Error.WriteLineAsync($"[PeekDbMCP] FATAL: {ex.Message}");
    Environment.ExitCode = 1;
}
finally
{
    Log.Information("=== PeekDbMCP shutting down ===");
    await Log.CloseAndFlushAsync();
}

static string MaskConnectionString(string connectionString)
{
    if (string.IsNullOrEmpty(connectionString)) return "(empty)";
    var masked = System.Text.RegularExpressions.Regex.Replace(
        connectionString,
        @"(Password|Pwd)=[^;]*",
        "$1=*****",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    return masked;
}