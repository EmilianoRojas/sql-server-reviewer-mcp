using System.Text.Json;
using System.Text.Json.Serialization;

namespace PeekDbMcp.Core;

public class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public JsonElement? Id { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }

    public bool IsNotification => Id is null || Id.Value.ValueKind == JsonValueKind.Undefined;
}

public class JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public JsonElement? Id { get; set; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonRpcError? Error { get; set; }
}

public class JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }

    public static JsonRpcError MethodNotFound(string method) =>
        new() { Code = -32601, Message = $"Method not found: {method}" };

    public static JsonRpcError InvalidParams(string detail) =>
        new() { Code = -32602, Message = $"Invalid params: {detail}" };

    public static JsonRpcError InternalError(string detail) =>
        new() { Code = -32603, Message = $"Internal error: {detail}" };

    public static JsonRpcError ParseError() =>
        new() { Code = -32700, Message = "Parse error" };
}

// --- MCP Protocol Models ---

public class McpServerInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "sql-server-reviewer";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";
}

public class McpCapabilities
{
    [JsonPropertyName("tools")]
    public McpToolsCapability? Tools { get; set; } = new();
}

public class McpToolsCapability
{
    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; set; } = false;
}

public class McpInitializeResult
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = "2024-11-05";

    [JsonPropertyName("capabilities")]
    public McpCapabilities Capabilities { get; set; } = new();

    [JsonPropertyName("serverInfo")]
    public McpServerInfo ServerInfo { get; set; } = new();
}

public class McpToolDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("inputSchema")]
    public JsonElement InputSchema { get; set; }
}

public class McpToolsListResult
{
    [JsonPropertyName("tools")]
    public List<McpToolDefinition> Tools { get; set; } = new();
}

public class McpToolCallResult
{
    [JsonPropertyName("content")]
    public List<McpContentBlock> Content { get; set; } = new();

    [JsonPropertyName("isError")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsError { get; set; }
}

public class McpContentBlock
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}
