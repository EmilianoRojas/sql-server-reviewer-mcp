using Serilog;
using SqlServerMcp.Configuration;
using SqlServerMcp.Core;
using SqlServerMcp.Services;
using SqlServerMcp.Tools;

try
{
    var settings = AppSettings.Load();

    // Log to file (AppContext.BaseDirectory = output folder like bin/Debug/net8.0)
    var logDir = Path.Combine(AppContext.BaseDirectory, "Logs");
    Directory.CreateDirectory(logDir);  // Ensure Logs folder exists
    var logPath = Path.Combine(logDir, "mcp-.log");

    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .WriteTo.File(logPath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
        .CreateLogger();

    Log.Information("=== SQL Server Reviewer MCP starting ===");

    // Console.Error.WriteLine goes to stderr - safe for startup messages before MCP takes over
    Console.Error.WriteLine("[SQL Server MCP] Server starting...");
    Console.Error.WriteLine($"[SQL Server MCP] Connection: {MaskConnectionString(settings.ConnectionString)}");

    var db = new DatabaseAnalyzer(settings.ConnectionString);
    var toolHandler = new ToolHandler(db);
    var dispatcher = new McpDispatcher(toolHandler);

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    Console.Error.WriteLine("[SQL Server MCP] Ready - listening for MCP requests...");
    await dispatcher.RunAsync(cts.Token);
}
catch (Exception ex)
{
    Log.Fatal(ex, "MCP server crashed");
    await Console.Error.WriteLineAsync($"[SQL Server MCP] FATAL: {ex.Message}");
    Environment.ExitCode = 1;
}
finally
{
    Log.Information("=== SQL Server Reviewer MCP shutting down ===");
    await Log.CloseAndFlushAsync();
}

static string MaskConnectionString(string connectionString)
{
    // Mask password in connection string for logging
    if (string.IsNullOrEmpty(connectionString)) return "(empty)";
    var masked = System.Text.RegularExpressions.Regex.Replace(
        connectionString,
        @"(Password|Pwd)=[^;]*",
        "$1=*****",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    return masked;
}
