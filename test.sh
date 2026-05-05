#!/bin/bash
# End-to-end test for SQL Server Reviewer MCP
# Sends JSON-RPC messages via stdin and captures stdout responses

export PATH=$PATH:/home/emiliano/.dotnet
export SQLSERVER_CONNECTION_STRING="Server=localhost;Database=McpTestDB;User Id=McpReader;Password=McpRead2024!;TrustServerCertificate=True;"

PROJECT_DIR="$(cd "$(dirname "$0")/PeekDbMcp" && pwd)"
PASS=0
FAIL=0

run_test() {
    local name="$1"
    local input="$2"
    local expect="$3"

    echo -n "  [$name] "
    result=$(echo "$input" | dotnet run --project "$PROJECT_DIR" 2>/dev/null)
    
    if echo "$result" | grep -q "$expect"; then
        echo "✅ PASS"
        ((PASS++))
    else
        echo "❌ FAIL"
        echo "    Expected to find: $expect"
        echo "    Got: $result"
        ((FAIL++))
    fi
}

echo ""
echo "🧪 SQL Server Reviewer MCP — End-to-End Tests"
echo "================================================"
echo ""

echo "--- Protocol Tests ---"

run_test "initialize" \
    '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}' \
    '"protocolVersion"'

run_test "tools/list" \
    '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
{"jsonrpc":"2.0","method":"notifications/initialized"}
{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}' \
    'list_tables'

echo ""
echo "--- Tool Tests (against McpTestDB) ---"

run_test "list_tables" \
    '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
{"jsonrpc":"2.0","method":"notifications/initialized"}
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"list_tables","arguments":{}}}' \
    'Customers'

run_test "analyze_table_schema" \
    '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
{"jsonrpc":"2.0","method":"notifications/initialized"}
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"analyze_table_schema","arguments":{"table_name":"Orders"}}}' \
    'CustomerID'

run_test "get_sp_definition" \
    '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
{"jsonrpc":"2.0","method":"notifications/initialized"}
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"get_sp_definition","arguments":{"sp_name":"usp_GetOrderDetails"}}}' \
    'SET NOCOUNT ON'

run_test "list_stored_procedures" \
    '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
{"jsonrpc":"2.0","method":"notifications/initialized"}
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"list_stored_procedures","arguments":{}}}' \
    'usp_GetCustomerOrders'

run_test "get_function_definition" \
    '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
{"jsonrpc":"2.0","method":"notifications/initialized"}
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"get_function_definition","arguments":{"function_name":"fn_CalculateOrderTotal"}}}' \
    'DECIMAL'

run_test "find_object_dependencies" \
    '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
{"jsonrpc":"2.0","method":"notifications/initialized"}
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"find_object_dependencies","arguments":{"object_name":"Customers"}}}' \
    'usp_GetOrderDetails'

run_test "search_in_code" \
    '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
{"jsonrpc":"2.0","method":"notifications/initialized"}
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"search_in_code","arguments":{"keyword":"CustomerID"}}}' \
    'usp_GetCustomerOrders'

run_test "get_table_relationships" \
    '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
{"jsonrpc":"2.0","method":"notifications/initialized"}
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"get_table_relationships","arguments":{"table_name":"Orders"}}}' \
    'FK_Orders_Customers'

echo ""
echo "--- New Tools ---"

run_test "get_index_usage_stats" \
    '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
{"jsonrpc":"2.0","method":"notifications/initialized"}
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"get_index_usage_stats","arguments":{}}}' \
    'content'

run_test "get_trigger_definition" \
    '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
{"jsonrpc":"2.0","method":"notifications/initialized"}
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"get_trigger_definition","arguments":{"trigger_name":"trg_Orders_AfterInsert"}}}' \
    'SET NOCOUNT ON'

run_test "list_views" \
    '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
{"jsonrpc":"2.0","method":"notifications/initialized"}
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"list_views","arguments":{}}}' \
    'vw_OrderSummary'

run_test "get_table_row_counts" \
    '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
{"jsonrpc":"2.0","method":"notifications/initialized"}
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"get_table_row_counts","arguments":{}}}' \
    'Customers'

run_test "analyze_query_plan" \
    '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
{"jsonrpc":"2.0","method":"notifications/initialized"}
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"analyze_query_plan","arguments":{"query":"SELECT * FROM dbo.Orders WHERE CustomerID = 1"}}}' \
    'ShowPlanXML'

echo ""
echo "================================================"
echo "Results: $PASS passed, $FAIL failed"
echo ""

if [ $FAIL -gt 0 ]; then
    exit 1
fi
