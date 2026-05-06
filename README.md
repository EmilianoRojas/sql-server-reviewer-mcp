# peekdb-mcp 🔍

[![Build & Test](https://github.com/EmilianoRojas/peekdb-mcp/actions/workflows/build.yml/badge.svg)](https://github.com/EmilianoRojas/peekdb-mcp/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A **read-only** [MCP (Model Context Protocol)](https://modelcontextprotocol.io/) server for SQL Server. Enables AI clients like Claude Desktop, Cursor, or any MCP-compatible client to analyze schemas, dependencies, stored procedures, and missing indexes — without risking any data changes.

## ⚡ Features

- **9 ready-to-use** analysis tools
- **Read-only** — no DDL or DML execution, only system view queries
- **Parameterized queries** — SQL injection protection
- **stdio communication** — JSON-RPC 2.0 via stdin/stdout
- **Secure logging** — Serilog writes to files only, never to stdout

## 🛠️ Available Tools

| Tool | Description |
|---|---|
| `list_tables` | Lists all tables with approximate row counts and space |
| `analyze_table_schema` | Columns, types, PKs, FKs, and indexes of a table |
| `get_sp_definition` | Source code of a stored procedure |
| `list_stored_procedures` | Catalog of all stored procedures |
| `get_function_definition` | Source code of a function |
| `find_object_dependencies` | What objects reference and who references it |
| `search_in_code` | Search text in all objects' code |
| `get_missing_indexes` | Missing indexes suggested by the engine (DMVs) |
| `get_table_relationships` | Foreign key map |

## 📋 Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or higher
- Access to a SQL Server instance
- A SQL user with read-only permissions (recommended: `db_datareader` + `VIEW DEFINITION`)

## 🚀 Installation

### Option A: Download binary (recommended)

Download the executable for your platform from [Releases](https://github.com/EmilianoRojas/peekdb-mcp/releases):

- `peekdb-mcp-win-x64.zip` — Windows
- `peekdb-mcp-linux-x64.tar.gz` — Linux
- `peekdb-mcp-osx-x64.tar.gz` — macOS

These are **self-contained** — no .NET installation required.

### Option B: Build from source

```bash
git clone https://github.com/EmilianoRojas/peekdb-mcp.git
cd peekdb-mcp/PeekDbMcp
dotnet publish -c Release -o ./publish
```

### Option C: Docker

```bash
docker build -t peekdb-mcp .
```

### Verify

```bash
# Linux/macOS
./publish/PeekDbMcp

# Windows
.\publish\PeekDbMcp.exe
```

> The process will wait for input on stdin. If it stays waiting, it's working correctly. Exit with `Ctrl+C`.

## ⚙️ Configuration

SQL Server connection is configured via the `SQLSERVER_CONNECTION_STRING` environment variable.

### Connection String Format

```
Server=your-server;Database=your-db;User Id=your-user;Password=your-password;TrustServerCertificate=True;
```

For Windows Authentication:

```
Server=your-server;Database=your-db;Integrated Security=True;TrustServerCertificate=True;
```

## 🔌 MCP Client Integration

### Claude Desktop

Edit your config file (`claude_desktop_config.json`):

**Windows:** `%APPDATA%\Claude\claude_desktop_config.json`
**macOS:** `~/Library/Application Support/Claude/claude_desktop_config.json`

```json
{
  "mcpServers": {
    "peekdb": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/path/to/peekdb-mcp/PeekDbMcp"],
      "env": {
        "SQLSERVER_CONNECTION_STRING": "Server=localhost;Database=MyDb;User Id=reader;Password=secret;TrustServerCertificate=True;"
      }
    }
  }
}
```

Or using the published executable:

```json
{
  "mcpServers": {
    "peekdb": {
      "command": "C:/path/to/peekdb-mcp/PeekDbMcp/publish/PeekDbMcp.exe",
      "env": {
        "SQLSERVER_CONNECTION_STRING": "Server=localhost;Database=MyDb;User Id=reader;Password=secret;TrustServerCertificate=True;"
      }
    }
  }
}
```

### Cursor

In Cursor's settings, add the MCP server with the same `command`, `args`, and `env` parameters.

### Docker

```json
{
  "mcpServers": {
    "peekdb": {
      "command": "docker",
      "args": [
        "run", "-i", "--rm",
        "-e", "SQLSERVER_CONNECTION_STRING=Server=host.docker.internal;Database=MyDb;User Id=reader;Password=secret;TrustServerCertificate=True;",
        "peekdb-mcp"
      ]
    }
  }
}
```

> Note: Use `host.docker.internal` to connect to the host's SQL Server.

## 💡 Usage Examples

Once configured, you can ask Claude things like:

- *"What tables are in this database?"*
- *"Show me the schema of the Users table"*
- *"Give me the code for the usp_GetOrderDetails SP"*
- *"What objects depend on the Products table?"*
- *"Search for all references to 'CustomerID' in the code"*
- *"Are there missing indexes I should create?"*
- *"Show me the relationship map for the Orders table"*

## 🔒 Security

### Recommended Permissions

Create a dedicated SQL user with minimal permissions:

```sql
CREATE LOGIN McpReader WITH PASSWORD = 'your-secure-password';
USE YourDatabase;
CREATE USER McpReader FOR LOGIN McpReader;
ALTER ROLE db_datareader ADD MEMBER McpReader;
GRANT VIEW DEFINITION TO McpReader;
```

### Security Principles

- **Read-only by design:** No code path executes `INSERT`, `UPDATE`, `DELETE`, or DDL
- **Parameterized queries:** All LLM input goes through ADO.NET/Dapper SQL parameters
- **No secrets in stdout:** Logs go exclusively to files in `Logs/`

## 📂 Project Structure

```
PeekDbMcp/
├── Program.cs                  # Entry point, Serilog config
├── Core/
│   ├── McpDispatcher.cs        # JSON-RPC stdin/stdout loop
│   ├── JsonRpcModels.cs        # Serialization models
│   └── ToolRegistry.cs         # Tool catalog + JSON Schemas
├── Services/
│   └── DatabaseAnalyzer.cs     # T-SQL queries with Dapper
├── Tools/
│   └── ToolHandler.cs          # Tool router
├── Configuration/
│   └── AppSettings.cs          # Environment variable reading
└── Logs/                       # Generated at runtime
```

For architecture details, see [ARCHITECTURE.md](ARCHITECTURE.md).

## 📝 Logs

Logs are written to `PeekDbMcp/Logs/` (or next to the published executable). Daily rotation, 7-day retention.

```bash
# Watch logs in real-time
tail -f PeekDbMcp/Logs/mcp-20260405.log
```

## 🤝 Contributing

1. Fork the repo
2. Create your branch (`git checkout -b feature/new-tool`)
3. Commit (`git commit -m 'feat: add tool X'`)
4. Push (`git push origin feature/new-tool`)
5. Open a Pull Request

## 📄 License

MIT