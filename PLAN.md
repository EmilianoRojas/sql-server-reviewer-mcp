# peekdb-mcp — Multi-Database Support Plan

## 🎯 Goal
Extend `peekdb-mcp` from SQL Server-only → PostgreSQL + SQLite support.

## ✅ Completed (Branch: `refactor/multi-provider`)

### Phase 1: Abstraction Layer
- [x] Define `IDatabaseAnalyzer` interface — all 14 tool methods + `GetCapabilities()`
- [x] Create common models in `Abstractions/Models/` — TableInfo, SchemaModels, RoutineModels, RelationshipModels, PerformanceModels
- [x] Move SQL Server analyzer to `Providers/SqlServer/SqlServerAnalyzer.cs`

### Phase 2: Provider Infrastructure
- [x] Add `ProviderFactory` with auto-detection from connection string
- [x] Add `PEEKDB_PROVIDER` env var for explicit selection
- [x] Add NuGet packages: Npgsql 8.0.0, Microsoft.Data.Sqlite 8.0.0

### Phase 3: Tool Updates
- [x] Update `ToolHandler` to use `IDatabaseAnalyzer` abstraction
- [x] Add capability checks — returns "not supported" for incompatible tools
- [x] Update `ToolRegistry` with English descriptions and tool compatibility notes

---

## 📊 Current State
```
PeekDbMcp/
├── Abstractions/
│   ├── IDatabaseAnalyzer.cs
│   └── Models/                      # ✅ All DTOs moved here
├── Providers/
│   ├── ProviderFactory.cs           # ✅ Auto-detect + explicit selection
│   ├── SqlServer/
│   │   └── SqlServerAnalyzer.cs     # ✅ Migrated, all methods implemented
│   ├── PostgreSql/
│   │   └── PostgresAnalyzer.cs      # ⚠️ Partial — needs real pg_stat queries
│   └── Sqlite/
│       └── SqliteAnalyzer.cs        # ⚠️ Partial — needs PRAGMA field mapping
├── Core/                             # Unchanged
├── Tools/
│   └── ToolHandler.cs               # ✅ Uses IDatabaseAnalyzer + capability checks
├── Configuration/                    # Unchanged
└── Program.cs                        # ✅ Uses ProviderFactory
```

---

## 🏗️ Target Architecture (Complete)

```
PeekDbMcp/
├── Abstractions/                     # ✅ Done
│   ├── IDatabaseAnalyzer.cs
│   └── Models/
│       ├── TableInfo.cs
│       ├── SchemaModels.cs
│       ├── RoutineModels.cs
│       ├── RelationshipModels.cs
│       └── PerformanceModels.cs
│
├── Providers/                        # ✅ Structure complete
│   ├── ProviderFactory.cs
│   ├── SqlServer/
│   │   └── SqlServerAnalyzer.cs     # ✅ Full implementation
│   ├── PostgreSql/
│   │   └── PostgresAnalyzer.cs       # ⚠️ Needs work
│   └── Sqlite/
│       └── SqliteAnalyzer.cs        # ⚠️ Needs work
│
├── Core/                             # Unchanged
├── Tools/
│   └── ToolHandler.cs               # ✅ Updated
│
└── Program.cs                        # ✅ Updated
```

---

## 🔌 Provider Detection

| DB | Auto-Detect | Env Var |
|---|---|---|
| SQL Server | `Server=`, `Data Source=` | `PEEKDB_PROVIDER=SqlServer` |
| PostgreSQL | `Host=`, `PORT=5432` | `PEEKDB_PROVIDER=PostgreSql` |
| SQLite | `.db` in path | `PEEKDB_PROVIDER=Sqlite` |

### Connection String Examples

**PostgreSQL:**
```
Host=localhost;Database=mydb;Username=user;Password=***
```

**SQLite:**
```
Data Source=/path/to/mydb.db
```

**SQL Server (existing):**
```
Server=localhost;Database=mydb;User Id=user;Password=***
```

---

## 🛠️ Tools by Compatibility

| Tool | SQL Server | PostgreSQL | SQLite |
|---|---|---|---|
| `list_tables` | ✅ | ⚠️ | ⚠️ |
| `analyze_table_schema` | ✅ | ⚠️ | ⚠️ |
| `get_table_row_counts` | ✅ | ⚠️ | ⚠️ |
| `get_table_relationships` | ✅ | ⚠️ | ⚠️ |
| `list_stored_procedures` | ✅ | ❌ | ❌ |
| `get_sp_definition` | ✅ | ❌ | ❌ |
| `list_functions` | ✅ | ⚠️ | ❌ |
| `get_function_definition` | ✅ | ⚠️ | ❌ |
| `find_object_dependencies` | ✅ | ⚠️ | ⚠️ |
| `search_in_code` | ✅ | ⚠️ | ⚠️ |
| `list_views` | ✅ | ⚠️ | ⚠️ |
| `get_view_definition` | ✅ | ⚠️ | ⚠️ |
| `list_triggers` | ✅ | ⚠️ | ❌ |
| `get_trigger_definition` | ✅ | ⚠️ | ❌ |
| `get_missing_indexes` | ✅ | ⚠️ | ❌ |
| `get_index_usage_stats` | ✅ | ⚠️ | ❌ |
| `analyze_query_plan` | ✅ | ⚠️ | ⚠️ |

**Legend:** ✅ = Working | ⚠️ = Implemented but needs testing | ❌ = Not applicable

---

## ⚠️ Pending Work

### PostgreSQL — Needs Implementation
- [ ] `ListTablesAsync` — add approximate row count from `pg_stat_user_tables`
- [ ] `AnalyzeTableSchemaAsync` — verify column mapping, add identity detection
- [ ] `GetTableRowCountsAsync` — real row counts from `pg_stat_user_tables`
- [ ] `GetMissingIndexesAsync` — map `pg_stat_user_indexes` to MissingIndex model
- [ ] `FindObjectDependenciesAsync` — refine `pg_depend` query
- [ ] `ListTriggersAsync` — fix `created`/`modify_date` fields

### SQLite — Needs Implementation
- [ ] `ListTablesAsync` — add schema field (default to "main")
- [ ] `AnalyzeTableSchemaAsync` — map PRAGMA output to ColumnInfo/ForeignKeyInfo/IndexInfo
- [ ] `GetTableRowCountsAsync` — real row counts (need `SELECT COUNT(*)` per table)
- [ ] `GetTableRelationshipsAsync` — map PRAGMA output to TableRelationship model

### Testing & Docs
- [ ] Update `test.sh` with PostgreSQL/SQLite test cases
- [ ] Update `appsettings.json` with example configs for all 3 providers
- [ ] Update `SECURITY.md` with multi-provider permissions
- [ ] Update `ARCHITECTURE.md` with new structure
- [ ] Build & test locally (no dotnet in sandbox)

---

## 📦 Dependencies

```xml
<PackageReference Include="Dapper" Version="2.1.72" />
<PackageReference Include="Microsoft.Data.SqlClient" Version="7.0.0" />
<PackageReference Include="Npgsql" Version="8.0.0" />
<PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.0" />
<PackageReference Include="Serilog" Version="4.3.1" />
<PackageReference Include="Serilog.Sinks.File" Version="7.0.0" />
```

---

## 🚀 Next Steps

1. **Build locally** — run `dotnet build` and fix any compile errors
2. **Test SQL Server** — verify existing functionality still works
3. **Implement PostgreSQL gaps** — fill in the ⚠️ methods
4. **Implement SQLite gaps** — map PRAGMA results to models
5. **Write tests** — update `test.sh` for all three providers
6. **Merge** — PR to `main` when stable
