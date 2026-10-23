using System.Text.Json;
using Curl.Mcp.Protocol.JsonRpc;
using Curl.Mcp.Protocol.Model;
using Curl.Mcp.Protocol.Server;

namespace Curl.Mcp.Protocol.Tools;

/// <summary>
/// Answers <c>tools/call</c>: finds the tool by <c>name</c> and runs it with the request's <c>arguments</c>
/// object (an empty object when absent). A missing <c>name</c>, an unknown tool or <c>arguments</c> that is
/// not an object is a -32602 error; a tool's failure result is passed through with <c>isError</c> true; an
/// exception a tool throws becomes the tool-error result <c>An error occurred invoking '&lt;name&gt;'.</c>,
/// as the SDK 2.2.0 server answered, unless it is the cancellation of the request.
/// </summary>
public sealed class ToolsCallHandler : IMcpMethodHandler
{
    private static readonly JsonElement EmptyArguments = JsonDocument.Parse("{}").RootElement.Clone();

    private readonly McpServerRegistry _registry;

    /// <summary>Initializes a new instance of the <see cref="ToolsCallHandler" /> class.</summary>
    /// <param name="registry">The registry the called tool is found in.</param>
    public ToolsCallHandler(McpServerRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <inheritdoc />
    public string Method => "tools/call";

    /// <inheritdoc />
    public async ValueTask<JsonElement> HandleAsync(JsonElement? parameters, CancellationToken cancellationToken)
    {
        string name = ReadName(parameters);
        IMcpTool tool = _registry.FindTool(name)
            ?? throw new McpProtocolException(JsonRpcErrorCodes.InvalidParams, $"Unknown tool: '{name}'");
        JsonElement arguments = ReadArguments(parameters!.Value);
        McpToolCallResult result = await CallToolAsync(tool, arguments, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.SerializeToElement(result, McpModelJsonContext.Default.McpToolCallResult);
    }

    private static string ReadName(JsonElement? parameters)
    {
        if (parameters is { ValueKind: JsonValueKind.Object } value
            && value.TryGetProperty("name", out JsonElement name)
            && name.ValueKind == JsonValueKind.String)
        {
            return name.GetString()!;
        }

        throw new McpProtocolException(JsonRpcErrorCodes.InvalidParams, "The tools/call request needs a name string in its params.");
    }

    private static JsonElement ReadArguments(JsonElement parameters)
    {
        if (!parameters.TryGetProperty("arguments", out JsonElement arguments) || arguments.ValueKind == JsonValueKind.Null)
        {
            return EmptyArguments;
        }

        return arguments.ValueKind == JsonValueKind.Object
            ? arguments
            : throw new McpProtocolException(JsonRpcErrorCodes.InvalidParams, "The tools/call arguments must be a JSON object.");
    }

    private static async ValueTask<McpToolCallResult> CallToolAsync(IMcpTool tool, JsonElement arguments, CancellationToken cancellationToken)
    {
        try
        {
            return await tool.CallAsync(arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return McpToolCallResult.Error($"An error occurred invoking '{tool.Name}'.");
        }
    }
}
