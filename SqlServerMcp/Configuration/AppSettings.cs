namespace SqlServerMcp.Configuration;

public class AppSettings
{
    public string ConnectionString { get; set; } = string.Empty;

    public static AppSettings Load()
    {
        var connectionString = Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "Missing required environment variable: SQLSERVER_CONNECTION_STRING. " +
                "Set it in your MCP client config (e.g. claude_desktop_config.json).");

        return new AppSettings { ConnectionString = connectionString };
    }
}
