using System.Text.Json;

namespace Curl.Extension.Protocol.McpClient.Wire;

/// <summary>
/// Reads the JSON-RPC 2.0 messages an MCP server sends a client (Model Context Protocol
/// 2025-06-18, "Base Protocol &gt; Overview"), and the results of <c>initialize</c>,
/// <c>tools/list</c> and <c>tools/call</c> ("Lifecycle" and "Server Features &gt; Tools").
/// </summary>
public static class McpResponseReader
{
    /// <summary>
    /// Reads one message and returns the response to the request whose id is
    /// <paramref name="awaitedId"/>, or <see langword="null"/> for any other message: a server
    /// request or notification (it has a <c>method</c>) or a response to another id.
    /// </summary>
    /// <exception cref="McpWireFormatException">The message is not a JSON-RPC 2.0 message, or the awaited response has neither a valid result nor a valid error.</exception>
    public static McpResponse? ReadResponse(ReadOnlySpan<byte> message, long awaitedId)
    {
        using var document = ParseMessage(message);
        var root = RequireObject(document.RootElement, "A JSON-RPC message");
        if (RequiredString(root, "jsonrpc") != "2.0")
        {
            throw new McpWireFormatException("The message's jsonrpc member is not \"2.0\".");
        }

        if (root.TryGetProperty("method", out _) || !AnswersRequest(root, awaitedId))
        {
            return null;
        }

        if (root.TryGetProperty("error", out var error))
        {
            return new McpResponse(awaitedId, null, ReadError(error));
        }

        if (root.TryGetProperty("result", out var result))
        {
            return new McpResponse(awaitedId, result.Clone(), null);
        }

        throw new McpWireFormatException("The response has neither a result nor an error.");
    }

    /// <summary>Reads an InitializeResult's <c>protocolVersion</c> ("Lifecycle &gt; Version Negotiation").</summary>
    /// <exception cref="McpWireFormatException">The result is not an object with a string protocolVersion.</exception>
    public static McpInitializeResult ReadInitializeResult(JsonElement result)
    {
        var initializeResult = RequireObject(result, "An InitializeResult");
        return new McpInitializeResult(RequiredString(initializeResult, "protocolVersion"));
    }

    /// <summary>Reads a <c>tools/list</c> result's tools and <c>nextCursor</c> ("Server Features &gt; Tools &gt; Listing Tools").</summary>
    /// <exception cref="McpWireFormatException">The result or one of its tools is not the shape the specification requires.</exception>
    public static McpToolsListResult ReadToolsListResult(JsonElement result)
    {
        var listResult = RequireObject(result, "A tools/list result");
        var tools = new List<McpTool>();
        foreach (var element in RequiredArray(listResult, "tools").EnumerateArray())
        {
            var tool = RequireObject(element, "A tool");
            var inputSchema = tool.TryGetProperty("inputSchema", out var schema) ? schema.GetRawText() : null;
            tools.Add(new McpTool(RequiredString(tool, "name"), OptionalString(tool, "description"), inputSchema));
        }

        return new McpToolsListResult(tools, OptionalString(listResult, "nextCursor"));
    }

    /// <summary>Reads a CallToolResult's content and <c>isError</c> ("Server Features &gt; Tools &gt; Tool Result").</summary>
    /// <exception cref="McpWireFormatException">The result or one of its content items is not the shape the specification requires.</exception>
    public static McpCallToolResult ReadCallToolResult(JsonElement result)
    {
        var callResult = RequireObject(result, "A CallToolResult");
        var content = new List<McpContent>();
        foreach (var element in RequiredArray(callResult, "content").EnumerateArray())
        {
            var item = RequireObject(element, "A content item");
            var type = RequiredString(item, "type");
            var text = type == "text" ? RequiredString(item, "text") : null;
            content.Add(new McpContent(type, text, item.GetRawText()));
        }

        return new McpCallToolResult(content, ReadIsError(callResult));
    }

    private static JsonDocument ParseMessage(ReadOnlySpan<byte> message)
    {
        var reader = new Utf8JsonReader(message);
        try
        {
            return JsonDocument.ParseValue(ref reader);
        }
        catch (JsonException exception)
        {
            throw new McpWireFormatException("The message is not valid JSON.", exception);
        }
    }

    private static bool AnswersRequest(JsonElement message, long awaitedId) =>
        message.TryGetProperty("id", out var id)
        && id.ValueKind == JsonValueKind.Number
        && id.TryGetInt64(out var number)
        && number == awaitedId;

    private static McpError ReadError(JsonElement error)
    {
        var errorObject = RequireObject(error, "A JSON-RPC error");
        if (!errorObject.TryGetProperty("code", out var code)
            || code.ValueKind != JsonValueKind.Number
            || !code.TryGetInt32(out var number))
        {
            throw new McpWireFormatException("The error's code is not an integer.");
        }

        return new McpError(number, RequiredString(errorObject, "message"));
    }

    private static bool ReadIsError(JsonElement callResult)
    {
        if (!callResult.TryGetProperty("isError", out var isError))
        {
            return false;
        }

        return isError.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new McpWireFormatException("The CallToolResult's isError is not a boolean."),
        };
    }

    private static JsonElement RequireObject(JsonElement element, string what) =>
        element.ValueKind == JsonValueKind.Object
            ? element
            : throw new McpWireFormatException($"{what} is not a JSON object.");

    private static JsonElement RequiredArray(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value
            : throw new McpWireFormatException($"The {name} member is not an array.");

    private static string RequiredString(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new McpWireFormatException($"The {name} member is not a string.");

    private static string? OptionalString(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : throw new McpWireFormatException($"The {name} member is not a string.");
    }
}
