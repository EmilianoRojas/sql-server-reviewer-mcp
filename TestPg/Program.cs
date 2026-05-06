using PeekDbMcp.Providers.Sqlite;

var dbPath = "/tmp/peekdb_test.db";
var connStr = "Data Source=" + dbPath;

var sqlite = new SqliteAnalyzer(connStr);

Console.WriteLine("=== ListTablesAsync ===");
foreach (var t in await sqlite.ListTablesAsync()) Console.WriteLine("  " + t.Schema + "." + t.Table);

Console.WriteLine("\n=== AnalyzeTableSchemaAsync ===");
var schema = await sqlite.AnalyzeTableSchemaAsync("users");
Console.WriteLine("  Table: " + schema.Table);
Console.WriteLine("  Columns: " + schema.Columns.Count());
foreach (var c in schema.Columns) Console.WriteLine("    " + c.ColumnName + ": " + c.DataType + " pk=" + c.IsPrimaryKey);

Console.WriteLine("\n=== ListViewsAsync ===");
foreach (var v in await sqlite.ListViewsAsync()) Console.WriteLine("  " + v.View);

Console.WriteLine("\n=== ListTriggersAsync ===");
foreach (var t in await sqlite.ListTriggersAsync()) Console.WriteLine("  " + t.Trigger + " on " + t.TableName);

Console.WriteLine("\n=== GetTableRelationshipsAsync ===");
foreach (var r in await sqlite.GetTableRelationshipsAsync(null)) Console.WriteLine("  FK: " + r.ForeignKeyName);

Console.WriteLine("\n✅ SQLite fully working!");
