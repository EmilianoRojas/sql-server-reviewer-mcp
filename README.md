# SQL Server Reviewer MCP 🔍

[![Build & Test](https://github.com/EmilianoRojas/peekdb-mcp/actions/workflows/build.yml/badge.svg)](https://github.com/EmilianoRojas/peekdb-mcp/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Un servidor [MCP (Model Context Protocol)](https://modelcontextprotocol.io/) de **solo lectura** para SQL Server. Permite que clientes IA como Claude Desktop, Cursor o cualquier cliente MCP compatible analicen esquemas, dependencias, procedimientos almacenados e índices faltantes de tu base de datos — sin riesgo de modificar nada.

## ⚡ Características

- **9 herramientas** de análisis listas para usar
- **Solo lectura** — no ejecuta DDL ni DML, solo consultas a vistas de sistema
- **Queries parametrizadas** — protección contra inyección SQL
- **Comunicación stdio** — JSON-RPC 2.0 vía stdin/stdout
- **Logging seguro** — Serilog escribe solo a archivos, nunca a stdout

## 🛠️ Herramientas Disponibles

| Herramienta | Descripción |
|---|---|
| `list_tables` | Lista todas las tablas con filas aproximadas y espacio |
| `analyze_table_schema` | Columnas, tipos, PKs, FKs e índices de una tabla |
| `get_sp_definition` | Código fuente de un procedimiento almacenado |
| `list_stored_procedures` | Catálogo de todos los SPs |
| `get_function_definition` | Código fuente de una función |
| `find_object_dependencies` | Qué objetos referencia y quién lo referencia |
| `search_in_code` | Buscar texto en el código de todos los objetos |
| `get_missing_indexes` | Índices faltantes sugeridos por el motor (DMVs) |
| `get_table_relationships` | Mapa de foreign keys |

## 📋 Requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) o superior
- Acceso a una instancia de SQL Server
- Un usuario SQL con permisos de solo lectura (recomendado: `db_datareader` + `VIEW DEFINITION`)

## 🚀 Instalación

### Opción A: Descargar binario (recomendado)

Descarga el ejecutable para tu plataforma desde [Releases](https://github.com/EmilianoRojas/peekdb-mcp/releases):

- `peekdb-mcp-win-x64.zip` — Windows
- `peekdb-mcp-linux-x64.tar.gz` — Linux
- `peekdb-mcp-osx-x64.tar.gz` — macOS

Estos son **self-contained** — no necesitas .NET instalado.

### Opción B: Compilar desde fuente

```bash
git clone https://github.com/EmilianoRojas/peekdb-mcp.git
cd peekdb-mcp/PeekDbMcp
dotnet publish -c Release -o ./publish
```

### Opción C: Docker

```bash
docker build -t peekdb-mcp .
```

### Verificar

```bash
# Linux/macOS
./publish/PeekDbMcp

# Windows
.\publish\PeekDbMcp.exe
```

> El proceso esperará input en stdin. Si se queda esperando, funciona correctamente. Cierra con `Ctrl+C`.

## ⚙️ Configuración

La conexión a SQL Server se configura mediante la variable de entorno `SQLSERVER_CONNECTION_STRING`.

### Formato de la cadena de conexión

```
Server=tu-servidor;Database=tu-base;User Id=tu-usuario;Password=tu-password;TrustServerCertificate=True;
```

Para Windows Authentication:

```
Server=tu-servidor;Database=tu-base;Integrated Security=True;TrustServerCertificate=True;
```

## 🔌 Integración con Clientes MCP

### Claude Desktop

Edita tu archivo de configuración (`claude_desktop_config.json`):

**Windows:** `%APPDATA%\Claude\claude_desktop_config.json`
**macOS:** `~/Library/Application Support/Claude/claude_desktop_config.json`

```json
{
  "mcpServers": {
    "peekdb": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/ruta/a/peekdb-mcp/PeekDbMcp"],
      "env": {
        "SQLSERVER_CONNECTION_STRING": "Server=localhost;Database=MiBase;User Id=reader;Password=secret;TrustServerCertificate=True;"
      }
    }
  }
}
```

O usando el ejecutable publicado:

```json
{
  "mcpServers": {
    "peekdb": {
      "command": "C:/ruta/a/peekdb-mcp/PeekDbMcp/publish/PeekDbMcp.exe",
      "env": {
        "SQLSERVER_CONNECTION_STRING": "Server=localhost;Database=MiBase;User Id=reader;Password=secret;TrustServerCertificate=True;"
      }
    }
  }
}
```

### Cursor

En la configuración de Cursor, agrega el MCP server con los mismos parámetros de `command`, `args` y `env`.

### Docker

```json
{
  "mcpServers": {
    "peekdb": {
      "command": "docker",
      "args": [
        "run", "-i", "--rm",
        "-e", "SQLSERVER_CONNECTION_STRING=Server=host.docker.internal;Database=MiBase;User Id=reader;Password=secret;TrustServerCertificate=True;",
        "peekdb-mcp"
      ]
    }
  }
}
```

> Nota: Usa `host.docker.internal` para conectar al SQL Server del host.

## 💡 Ejemplos de Uso

Una vez configurado, puedes preguntarle a Claude cosas como:

- *"¿Qué tablas tiene esta base de datos?"*
- *"Muéstrame el esquema de la tabla Users"*
- *"Dame el código del SP usp_GetOrderDetails"*
- *"¿Qué objetos dependen de la tabla Products?"*
- *"Busca todas las referencias a 'CustomerID' en el código"*
- *"¿Hay índices faltantes que debería crear?"*
- *"Muéstrame el mapa de relaciones de la tabla Orders"*

## 🔒 Seguridad

### Permisos Recomendados

Crea un usuario SQL dedicado con permisos mínimos:

```sql
CREATE LOGIN McpReader WITH PASSWORD = 'tu-password-seguro';
USE TuBaseDeDatos;
CREATE USER McpReader FOR LOGIN McpReader;
ALTER ROLE db_datareader ADD MEMBER McpReader;
GRANT VIEW DEFINITION TO McpReader;
```

### Principios de Seguridad

- **Solo lectura por diseño:** No existe ningún path de código que ejecute `INSERT`, `UPDATE`, `DELETE` o DDL
- **Queries parametrizadas:** Todo input del LLM pasa por parámetros SQL de ADO.NET/Dapper
- **Sin secrets en stdout:** Los logs van exclusivamente a archivos en `Logs/`

## 📂 Estructura del Proyecto

```
PeekDbMcp/
├── Program.cs                  # Entry point, Serilog config
├── Core/
│   ├── McpDispatcher.cs        # JSON-RPC stdin/stdout loop
│   ├── JsonRpcModels.cs        # Modelos de serialización
│   └── ToolRegistry.cs         # Catálogo de herramientas + JSON Schemas
├── Services/
│   └── DatabaseAnalyzer.cs     # Queries T-SQL con Dapper
├── Tools/
│   └── ToolHandler.cs          # Router de herramientas
├── Configuration/
│   └── AppSettings.cs          # Lectura de variables de entorno
└── Logs/                       # Generado en runtime
```

Para más detalles sobre la arquitectura, ver [ARCHITECTURE.md](ARCHITECTURE.md).

## 📝 Logs

Los logs se escriben en `PeekDbMcp/Logs/` (o junto al ejecutable publicado). Rotación diaria, retención de 7 días.

```bash
# Ver logs en tiempo real
tail -f PeekDbMcp/Logs/mcp-20260405.log
```

## 🤝 Contribuir

1. Fork el repo
2. Crea tu branch (`git checkout -b feature/nueva-herramienta`)
3. Commit (`git commit -m 'feat: agregar herramienta X'`)
4. Push (`git push origin feature/nueva-herramienta`)
5. Abre un Pull Request

## 📄 Licencia

MIT
