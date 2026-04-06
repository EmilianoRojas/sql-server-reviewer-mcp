using System.Text.Json;

namespace SqlServerMcp.Core;

public static class ToolRegistry
{
    private static readonly List<McpToolDefinition> _tools = new();

    public static IReadOnlyList<McpToolDefinition> Tools => _tools.AsReadOnly();

    static ToolRegistry()
    {
        Register("list_tables",
            "Lista todas las tablas de usuario en la base de datos con su esquema, cantidad de filas aproximada y espacio utilizado.",
            new
            {
                type = "object",
                properties = new { },
                required = Array.Empty<string>()
            });

        Register("analyze_table_schema",
            "Devuelve el esquema completo de una tabla: columnas, tipos de dato, nullability, PKs, FKs e índices.",
            new
            {
                type = "object",
                properties = new
                {
                    table_name = new
                    {
                        type = "string",
                        description = "Nombre de la tabla (opcionalmente con esquema, ej: 'dbo.Users')"
                    }
                },
                required = new[] { "table_name" }
            });

        Register("get_sp_definition",
            "Obtiene el código fuente (definición T-SQL) de un procedimiento almacenado.",
            new
            {
                type = "object",
                properties = new
                {
                    sp_name = new
                    {
                        type = "string",
                        description = "Nombre del procedimiento almacenado (opcionalmente con esquema)"
                    }
                },
                required = new[] { "sp_name" }
            });

        Register("list_stored_procedures",
            "Lista todos los procedimientos almacenados de la base de datos con su esquema y fecha de modificación.",
            new
            {
                type = "object",
                properties = new { },
                required = Array.Empty<string>()
            });

        Register("get_function_definition",
            "Obtiene el código fuente de una función (escalar, tabla o inline).",
            new
            {
                type = "object",
                properties = new
                {
                    function_name = new
                    {
                        type = "string",
                        description = "Nombre de la función (opcionalmente con esquema)"
                    }
                },
                required = new[] { "function_name" }
            });

        Register("find_object_dependencies",
            "Busca las dependencias de un objeto: qué objetos referencia y qué objetos lo referencian a él.",
            new
            {
                type = "object",
                properties = new
                {
                    object_name = new
                    {
                        type = "string",
                        description = "Nombre del objeto (tabla, SP, función, vista)"
                    }
                },
                required = new[] { "object_name" }
            });

        Register("search_in_code",
            "Busca un texto dentro del código de todos los objetos programáticos (SPs, funciones, triggers, vistas).",
            new
            {
                type = "object",
                properties = new
                {
                    keyword = new
                    {
                        type = "string",
                        description = "Texto a buscar en el código fuente de los objetos"
                    }
                },
                required = new[] { "keyword" }
            });

        Register("get_missing_indexes",
            "Consulta las DMVs de SQL Server para obtener los índices faltantes sugeridos por el motor, ordenados por impacto.",
            new
            {
                type = "object",
                properties = new { },
                required = Array.Empty<string>()
            });

        Register("get_table_relationships",
            "Obtiene todas las relaciones de foreign keys de una tabla o de toda la base de datos.",
            new
            {
                type = "object",
                properties = new
                {
                    table_name = new
                    {
                        type = "string",
                        description = "Nombre de la tabla (opcional; si se omite, devuelve todas las FKs de la BD)"
                    }
                },
                required = Array.Empty<string>()
            });

        Register("get_index_usage_stats",
            "Muestra estadísticas de uso real de los índices existentes: seeks, scans, lookups y updates. Identifica índices sin uso o de bajo valor.",
            new
            {
                type = "object",
                properties = new { },
                required = Array.Empty<string>()
            });

        Register("get_trigger_definition",
            "Obtiene el código fuente (definición T-SQL) de un trigger.",
            new
            {
                type = "object",
                properties = new
                {
                    trigger_name = new
                    {
                        type = "string",
                        description = "Nombre del trigger (opcionalmente con esquema)"
                    }
                },
                required = new[] { "trigger_name" }
            });

        Register("list_views",
            "Lista todas las vistas de usuario con su esquema, fecha de creación y tamaño de definición.",
            new
            {
                type = "object",
                properties = new { },
                required = Array.Empty<string>()
            });

        Register("get_table_row_counts",
            "Devuelve el conteo aproximado de filas, cantidad de columnas e índices de cada tabla, ordenado por tamaño.",
            new
            {
                type = "object",
                properties = new { },
                required = Array.Empty<string>()
            });

        Register("analyze_query_plan",
            "Recibe una consulta SELECT y devuelve el plan de ejecución estimado (XML). Útil para analizar performance sin ejecutar la query.",
            new
            {
                type = "object",
                properties = new
                {
                    query = new
                    {
                        type = "string",
                        description = "Consulta SQL SELECT para obtener su plan de ejecución estimado"
                    }
                },
                required = new[] { "query" }
            });
    }

    private static void Register(string name, string description, object inputSchema)
    {
        var schemaJson = JsonSerializer.Serialize(inputSchema);
        _tools.Add(new McpToolDefinition
        {
            Name = name,
            Description = description,
            InputSchema = JsonDocument.Parse(schemaJson).RootElement.Clone()
        });
    }
}
