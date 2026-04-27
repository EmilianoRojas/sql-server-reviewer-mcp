using System.Text.Json;

namespace SqlServerMcp.Configuration;

public class AppSettings
{
    public string ConnectionString { get; set; } = string.Empty;

    public static AppSettings Load()
    {
        // 1. Environment variable takes priority (for container/cloud deployments)
        var envConnectionString = Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(envConnectionString))
        {
            return new AppSettings { ConnectionString = envConnectionString };
        }

        // 2. Load base appsettings.json
        var baseConfig = LoadFromFile("appsettings.json");
        if (baseConfig != null)
        {
            // 3. In Development, override with appsettings.Development.json
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
            if (env.Equals("Development", StringComparison.OrdinalIgnoreCase))
            {
                var devConfig = LoadFromFile("appsettings.Development.json");
                if (devConfig != null && !string.IsNullOrWhiteSpace(devConfig.ConnectionString))
                {
                    return devConfig;
                }
            }

            if (!string.IsNullOrWhiteSpace(baseConfig.ConnectionString))
            {
                return baseConfig;
            }
        }

        throw new InvalidOperationException(
            "Missing connection string. Set 'SQLSERVER_CONNECTION_STRING' environment variable, " +
            "or create 'appsettings.json' / 'appsettings.Development.json' with your connection details.");
    }

    private static AppSettings? LoadFromFile(string fileName)
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, fileName);
        if (File.Exists(configPath))
        {
            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<AppSettings>(json);
        }
        return null;
    }
}
