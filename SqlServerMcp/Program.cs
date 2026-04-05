using Serilog;
using SqlServerMcp.Configuration;
using SqlServerMcp.Core;
using SqlServerMcp.Services;
using SqlServerMcp.Tools;

// Configure Serilog to file only — NEVER to stdout (would corrupt JSON-RPC)
var logPath = Path.Combine(AppContext.BaseDirectory, "Logs", "mcp-.log");
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(logPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("=== SQL Server Reviewer MCP starting ===");

    var settings = AppSettings.Load();
    Log.Information("Connection string loaded successfully.");

    var db = new DatabaseAnalyzer(settings.ConnectionString);
    var toolHandler = new ToolHandler(db);
    var dispatcher = new McpDispatcher(toolHandler);

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    await dispatcher.RunAsync(cts.Token);
}
catch (Exception ex)
{
    Log.Fatal(ex, "MCP server crashed");

    // Write error to stderr (not stdout) so the client sees it
    await Console.Error.WriteLineAsync($"FATAL: {ex.Message}");
    Environment.ExitCode = 1;
}
finally
{
    Log.Information("=== SQL Server Reviewer MCP shutting down ===");
    await Log.CloseAndFlushAsync();
}
