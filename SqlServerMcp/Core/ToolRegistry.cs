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
