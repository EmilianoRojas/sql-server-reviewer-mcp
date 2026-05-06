using System.Text.RegularExpressions;
using PeekDbMcp.Abstractions;

namespace PeekDbMcp.Providers;

public static class ProviderFactory
{
    public static IDatabaseAnalyzer Create(string connectionString, string? explicitProvider = null)
    {
        // Explicit provider takes precedence
        if (!string.IsNullOrEmpty(explicitProvider))
        {
            return CreateExplicit(explicitProvider, connectionString);
        }

        // Auto-detect from connection string
        return connectionString.ToUpperInvariant() switch
        {
            var s when s.Contains("HOST=") || s.Contains("PORT=5432") => new PostgreSql.PostgresAnalyzer(connectionString),
            var s when s.Contains("DATA SOURCE=") && s.Contains(".DB") => new Sqlite.SqliteAnalyzer(connectionString),
            var s when s.Contains("SERVER=") || s.Contains("DATA SOURCE=") => new SqlServer.SqlServerAnalyzer(connectionString),
            _ => throw new ArgumentException($"Could not detect database provider from connection string. Please specify a provider explicitly.")
        };
    }

    private static IDatabaseAnalyzer CreateExplicit(string provider, string connectionString)
    {
        return provider.ToUpperInvariant() switch
        {
            "SQLSERVER" or "MSSQL" or "SQL SERVER" => new SqlServer.SqlServerAnalyzer(connectionString),
            "POSTGRESQL" or "POSTGRES" or "NPGSQL" => new PostgreSql.PostgresAnalyzer(connectionString),
            "SQLITE" or "SQLITE3" => new Sqlite.SqliteAnalyzer(connectionString),
            _ => throw new ArgumentException($"Unknown provider: '{provider}'. Valid options: SqlServer, PostgreSql, Sqlite.")
        };
    }

    public static string? DetectProvider(string connectionString)
    {
        var upper = connectionString.ToUpperInvariant();
        if (upper.Contains("HOST=") || upper.Contains("PORT=5432")) return "PostgreSql";
        if (upper.Contains("DATA SOURCE=") && upper.Contains(".DB")) return "Sqlite";
        if (upper.Contains("SERVER=") || upper.Contains("DATA SOURCE=")) return "SqlServer";
        return null;
    }
}