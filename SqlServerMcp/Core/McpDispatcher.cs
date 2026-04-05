using System.Text.Json;
using Serilog;

namespace SqlServerMcp.Core;

public class McpDispatcher
{
    private readonly IToolHandler _toolHandler;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _initialized;

    public McpDispatcher(IToolHandler toolHandler)
    {
        _toolHandler = toolHandler;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        Log.Information("MCP Dispatcher started. Waiting for JSON-RPC messages on stdin...");

        using var reader = new StreamReader(Console.OpenStandardInput());

        while (!cancellationToken.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (line is null)
            {
                Log.Information("stdin closed. Shutting down.");
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
                continue;

            Log.Debug("Received: {Line}", line);

            JsonRpcRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<JsonRpcRequest>(line, _jsonOptions);
                if (request is null)
                {
                    await SendErrorAsync(null, JsonRpcError.ParseError());
                    continue;
                }
            }
            catch (JsonException ex)
            {
                Log.Error(ex, "Failed to parse JSON-RPC request");
                await SendErrorAsync(null, JsonRpcError.ParseError());
                continue;
            }

            var response = await HandleRequestAsync(request);

            // Notifications don't get responses
            if (request.IsNotification)
            {
                Log.Debug("Notification handled: {Method}", request.Method);
                continue;
            }

            if (response is not null)
            {
                await SendResponseAsync(response);
            }
        }

        Log.Information("MCP Dispatcher stopped.");
    }

    private async Task<JsonRpcResponse?> HandleRequestAsync(JsonRpcRequest request)
    {
        Log.Information("Handling method: {Method}", request.Method);

        return request.Method switch
        {
            "initialize" => HandleInitialize(request),
            "notifications/initialized" => HandleNotificationsInitialized(request),
            "tools/list" => HandleToolsList(request),
            "tools/call" => await HandleToolsCallAsync(request),
            "ping" => new JsonRpcResponse { Id = request.Id, Result = new { } },
            _ => new JsonRpcResponse
            {
                Id = request.Id,
                Error = JsonRpcError.MethodNotFound(request.Method)
            }
        };
    }

    private JsonRpcResponse HandleInitialize(JsonRpcRequest request)
    {
        _initialized = true;
        Log.Information("Client initialized.");

        return new JsonRpcResponse
        {
            Id = request.Id,
            Result = new McpInitializeResult()
        };
    }

    private JsonRpcResponse? HandleNotificationsInitialized(JsonRpcRequest request)
    {
        Log.Information("Client confirmed initialization.");
        return null; // Notification, no response
    }

    private JsonRpcResponse HandleToolsList(JsonRpcRequest request)
    {
        return new JsonRpcResponse
        {
            Id = request.Id,
            Result = new McpToolsListResult { Tools = ToolRegistry.Tools.ToList() }
        };
    }

    private async Task<JsonRpcResponse> HandleToolsCallAsync(JsonRpcRequest request)
    {
        if (!_initialized)
        {
            return new JsonRpcResponse
            {
                Id = request.Id,
                Error = new JsonRpcError { Code = -32600, Message = "Server not initialized" }
            };
        }

        string toolName;
        JsonElement arguments;

        try
        {
            var paramsElement = request.Params!.Value;
            toolName = paramsElement.GetProperty("name").GetString()!;
            arguments = paramsElement.TryGetProperty("arguments", out var args)
                ? args
                : JsonDocument.Parse("{}").RootElement;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to parse tools/call params");
            return new JsonRpcResponse
            {
                Id = request.Id,
                Error = JsonRpcError.InvalidParams("Expected 'name' and optional 'arguments'")
            };
        }

        Log.Information("Calling tool: {ToolName}", toolName);

        try
        {
            var result = await _toolHandler.HandleAsync(toolName, arguments);
            return new JsonRpcResponse { Id = request.Id, Result = result };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Tool execution failed: {ToolName}", toolName);
            return new JsonRpcResponse
            {
                Id = request.Id,
                Result = new McpToolCallResult
                {
                    IsError = true,
                    Content = new List<McpContentBlock>
                    {
                        new() { Text = $"Error executing {toolName}: {ex.Message}" }
                    }
                }
            };
        }
    }

    private async Task SendResponseAsync(JsonRpcResponse response)
    {
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        Log.Debug("Sending: {Json}", json);
        await Console.Out.WriteLineAsync(json);
        await Console.Out.FlushAsync();
    }

    private async Task SendErrorAsync(JsonElement? id, JsonRpcError error)
    {
        var response = new JsonRpcResponse { Id = id, Error = error };
        await SendResponseAsync(response);
    }
}

public interface IToolHandler
{
    Task<McpToolCallResult> HandleAsync(string toolName, JsonElement arguments);
}
