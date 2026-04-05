# Arquitectura: SQL Server Reviewer MCP

Este documento describe la arquitectura interna del servidor MCP (Model Context Protocol) de solo lectura para SQL Server. La aplicación actúa como un puente sin estado entre un cliente IA (como Claude Desktop o Cursor) y una base de datos SQL Server, permitiendo el análisis de esquemas, dependencias y código de procedimientos almacenados.

## 🏗️ Visión General del Sistema

El proyecto es una **Aplicación de Consola en .NET** que se ejecuta como un proceso hijo gestionado por el cliente IA. La comunicación se realiza estrictamente a través de los flujos de entrada/salida estándar (`stdio`) utilizando el estándar **JSON-RPC 2.0**.

### Tecnologías Core

- **Framework:** .NET 8 o superior (Aplicación de consola C#).
- **Protocolo:** JSON-RPC 2.0 vía `stdio` (Standard Input/Output).
- **Acceso a Datos:** Dapper + Microsoft.Data.SqlClient.
- **Inyección de Dependencias:** Microsoft.Extensions.DependencyInjection.
- **Logging:** Serilog (Exclusivamente hacia archivos locales para no corromper el flujo `stdout`).

## 📂 Estructura del Proyecto

```
SqlServerMcp/
├── Program.cs                  # Punto de entrada, configuración de DI y Host
├── Core/
│   ├── McpDispatcher.cs        # Ciclo infinito de lectura/escritura (Console.In/Out)
│   ├── JsonRpcModels.cs        # Clases de serialización para Requests y Responses
│   └── ToolRegistry.cs         # Registro estático del JSON Schema de las herramientas
├── Services/
│   └── DatabaseAnalyzer.cs     # Lógica de negocio para interactuar con SQL Server
├── Tools/
│   ├── SchemaTools.cs          # Handlers para tablas, columnas, FKs/PKs
│   ├── RoutineTools.cs         # Handlers para SPs, Funciones y Triggers
│   └── PerformanceTools.cs     # Handlers para DMVs (Missing indexes, etc.)
├── Configuration/
│   └── AppSettings.cs          # Manejo de variables de entorno (Cadena de conexión)
└── Logs/                       # Carpeta generada en runtime para Serilog
```

## 🧩 Componentes Principales

### 1. Capa de Transporte (I/O Loop)

Ubicada en `McpDispatcher.cs`, es el corazón del servidor. Su única responsabilidad es escuchar `Console.ReadLineAsync()`, deserializar el string JSON entrante, y enrutar la petición. Todas las salidas deben ser formateadas como JSON válido y enviadas vía `Console.WriteLineAsync()`.

> **Restricción Crítica:** Está estrictamente prohibido usar la consola para logs generales o debugging de la aplicación, ya que el cliente IA fallará al parsear respuestas no-JSON.

### 2. Gestor de Protocolo (Protocol Handler)

Implementa la especificación MCP interceptando dos métodos principales:

- **`tools/list`**: Devuelve el catálogo de herramientas disponibles. Lee las definiciones registradas en el `ToolRegistry`, las cuales incluyen el nombre, la descripción y el JSON Schema de los parámetros esperados.
- **`tools/call`**: Recibe la intención de la IA de ejecutar una herramienta. Extrae los argumentos parseados por el LLM y delega la ejecución a los handlers específicos en la carpeta `Tools/`.

### 3. Capa de Herramientas (Tool Handlers)

Las clases dentro de `Tools/` actúan como controladores. Validan los parámetros extraídos del JSON-RPC y llaman a los servicios correspondientes. Ejemplos de herramientas expuestas:

- `get_sp_definition(string sp_name)`
- `analyze_table_schema(string table_name)`
- `find_object_dependencies(string object_name)`

### 4. Capa de Acceso a Datos (Data Access)

Ubicada en `DatabaseAnalyzer.cs` y utilizando **Dapper**. Esta capa centraliza todas las consultas T-SQL contra las tablas de sistema del motor (`sys.tables`, `sys.sql_modules`, `sys.dm_db_missing_index_details`).

Se prioriza el uso de consultas parametrizadas puras sobre Entity Framework para mantener un footprint de memoria bajo y tiempos de respuesta ultra rápidos (esenciales para mantener la fluidez en la conversación con el LLM).

## 🔄 Flujo de Datos (Data Flow)

1. **Petición del LLM:** Claude decide que necesita ver el código de un procedimiento almacenado. El cliente emite un payload JSON-RPC a través del proceso `stdin`.
2. **Recepción:** `Program.cs` lee la línea, la pasa al `McpDispatcher` y la deserializa al modelo `JsonRpcRequest`.
3. **Ruteo:** El dispatcher identifica el método `tools/call` y el nombre de la herramienta `get_sp_definition`.
4. **Ejecución de Datos:** El `RoutineTools` inyecta el `DatabaseAnalyzer`, el cual abre una conexión de solo lectura usando SqlClient, ejecuta el query contra `sys.sql_modules` vía Dapper usando el parámetro recibido, y obtiene el script T-SQL.
5. **Respuesta:** El string T-SQL se empaqueta en un objeto `JsonRpcResponse`, se serializa y se imprime en `stdout`.
6. **Consumo:** El cliente IA lee el `stdout`, incorpora el código T-SQL en su contexto, y genera la respuesta final para el desarrollador.

## 🔒 Consideraciones de Seguridad

- **Solo Lectura:** El diseño asume y requiere explícitamente el uso de credenciales SQL con permisos limitados (idealmente, roles como `db_datareader` y `VIEW DEFINITION`).
- **Protección de Inyección SQL:** Todas las consultas internas de las herramientas utilizan parámetros SQL seguros de ADO.NET vía Dapper. El input proveniente del LLM **nunca** se concatena directamente en los strings de consulta T-SQL.
