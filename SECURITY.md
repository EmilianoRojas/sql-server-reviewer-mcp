# Seguridad — SQL Server Reviewer MCP

## Modelo de Amenazas

Este MCP server recibe input de un LLM (Large Language Model). El LLM actúa como intermediario entre el usuario humano y la base de datos. Esto introduce un vector de ataque único: **prompt injection** — donde un input malicioso al LLM podría intentar usar las herramientas del MCP para ejecutar acciones no deseadas.

## Capas de Defensa

### 1. Permisos de Base de Datos (Primera Línea)

El server está diseñado para usar un usuario con **permisos mínimos**:

```sql
-- Solo lectura + ver definiciones + planes de ejecución
ALTER ROLE db_datareader ADD MEMBER McpReader;
GRANT VIEW DEFINITION TO McpReader;
GRANT SHOWPLAN TO McpReader;
```

**¿Por qué esto importa?** Incluso si todas las demás capas fallan, un usuario `db_datareader` **no puede** ejecutar `INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, `CREATE`, ni ninguna operación destructiva. SQL Server lo bloqueará a nivel de motor.

### 2. Queries Parametrizadas (Segunda Línea)

Todas las herramientas que aceptan input del usuario utilizan **parámetros SQL** de ADO.NET a través de Dapper:

```csharp
// ✅ Seguro — el input se envía como parámetro, no concatenado
await conn.QueryAsync(sql, new { Schema = schema, Name = name });
```

Esto previene inyección SQL clásica. El input del LLM **nunca** se concatena en strings T-SQL.

### 3. Validación de Input (Tercera Línea)

La herramienta `analyze_query_plan` es un caso especial porque recibe SQL libre. Para esto implementamos un validador que:

- Solo permite queries que empiecen con `SELECT` o `WITH`
- Bloquea keywords peligrosos: `INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, `CREATE`, `EXEC`, `XP_`, etc.
- Bloquea separadores de sentencias (`;`) que permiten multi-statement injection
- Bloquea comentarios SQL (`--`, `/*`) que pueden ocultar código malicioso
- Bloquea funciones de acceso externo: `OPENROWSET`, `OPENDATASOURCE`, `OPENQUERY`

### 4. Sin Secrets en stdout (Cuarta Línea)

Los logs (Serilog) escriben exclusivamente a archivos. El stdout está reservado para JSON-RPC. Esto previene:

- Leak de connection strings en respuestas al LLM
- Exposición de errores internos con información sensible

## Herramientas y Nivel de Riesgo

| Herramienta | Riesgo | Justificación |
|---|---|---|
| `list_tables` | 🟢 Bajo | Sin parámetros de usuario |
| `analyze_table_schema` | 🟢 Bajo | Parametrizado por nombre |
| `get_sp_definition` | 🟢 Bajo | Parametrizado por nombre |
| `list_stored_procedures` | 🟢 Bajo | Sin parámetros de usuario |
| `get_function_definition` | 🟢 Bajo | Parametrizado por nombre |
| `find_object_dependencies` | 🟢 Bajo | Parametrizado por nombre |
| `search_in_code` | 🟡 Medio | LIKE con input (parametrizado + límite 200 chars) |
| `get_missing_indexes` | 🟢 Bajo | Sin parámetros de usuario |
| `get_table_relationships` | 🟢 Bajo | Parametrizado por nombre |
| `get_index_usage_stats` | 🟢 Bajo | Sin parámetros de usuario |
| `get_trigger_definition` | 🟢 Bajo | Parametrizado por nombre |
| `list_views` | 🟢 Bajo | Sin parámetros de usuario |
| `get_table_row_counts` | 🟢 Bajo | Sin parámetros de usuario |
| `analyze_query_plan` | 🟡 Medio | SQL libre con validación + SHOWPLAN (no ejecuta) |

## Qué NO Protege Este Server

- **Exfiltración de datos vía lectura:** Un usuario con `db_datareader` puede leer todos los datos. Si la BD contiene información sensible (PII, financiera, etc.), considera usar un usuario con permisos más restrictivos por esquema/tabla.
- **Denial of Service:** Una query compleja en `analyze_query_plan` podría generar un plan pesado. Considera agregar timeouts a nivel de conexión.
- **Connection string exposure:** La cadena de conexión vive en una variable de entorno. Protégela con los mecanismos de tu SO/orquestador.

## Recomendaciones

1. **Usa siempre un usuario dedicado** — nunca `sa` ni `dbo`
2. **Limita permisos por esquema** si la BD tiene datos sensibles
3. **Revisa los logs** en `Logs/` periódicamente
4. **Agrega un timeout** de conexión: `Connection Timeout=30;Command Timeout=30;` en tu connection string
5. **No expongas el server a red pública** — se ejecuta como proceso hijo local

## Reportar Vulnerabilidades

Si encuentras una vulnerabilidad, por favor repórtala por email antes de hacer disclosure público.
