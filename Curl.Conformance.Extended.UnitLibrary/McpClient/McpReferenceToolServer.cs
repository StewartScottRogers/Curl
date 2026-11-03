using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Curl.Conformance.Extended.McpClient;

/// <summary>
/// The JSON-RPC side of a reference MCP server, written from the Model Context Protocol 2025-06-18 ("Base Protocol",
/// "Lifecycle", "Server Features &gt; Tools" and "Utilities &gt; Pagination") and apart from any transport: it answers
/// one message at a time, so the Streamable HTTP server and a stdio server serve it alike.
/// </summary>
/// <param name="tools">The tools <c>tools/list</c> lists and <c>tools/call</c> calls.</param>
public sealed class McpReferenceToolServer(IReadOnlyList<McpReferenceTool> tools)
{
    /// <summary>JSON-RPC's "Method not found" error code.</summary>
    public const int MethodNotFound = -32601;

    /// <summary>JSON-RPC's "Invalid params" error code, which the specification uses for an unknown tool and an invalid cursor.</summary>
    public const int InvalidParams = -32602;

    private const string CursorPrefix = "opaque:";

    /// <summary>Gets the protocol version <c>initialize</c> answers with; the latest, 2025-06-18, unless a case sets another.</summary>
    public string ProtocolVersion { get; init; } = "2025-06-18";

    /// <summary>Gets a value indicating whether <c>initialize</c> declares the <c>tools</c> capability.</summary>
    public bool DeclaresTools { get; init; } = true;

    /// <summary>Gets how many tools one <c>tools/list</c> page holds; a page that leaves tools out ends with a <c>nextCursor</c>.</summary>
    public int PageSize { get; init; } = int.MaxValue;

    /// <summary>Answers one JSON-RPC message.</summary>
    /// <param name="message">The message, one JSON object.</param>
    /// <returns>The response, or <see langword="null" /> for a notification, which has no id.</returns>
    public string? Answer(string message)
    {
        using JsonDocument document = JsonDocument.Parse(message);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("id", out JsonElement id))
        {
            return null;
        }

        string? method = root.GetProperty("method").GetString();
        root.TryGetProperty("params", out JsonElement parameters);
        return method switch
        {
            "initialize" => Result(id, WriteInitializeResult),
            "tools/list" => ListTools(id, parameters),
            "tools/call" => CallTool(id, parameters),
            _ => Error(id, MethodNotFound, $"Method not found: {method}"),
        };
    }

    private void WriteInitializeResult(Utf8JsonWriter writer)
    {
        writer.WriteString("protocolVersion", ProtocolVersion);
        writer.WriteStartObject("capabilities");
        if (DeclaresTools)
        {
            writer.WriteStartObject("tools");
            writer.WriteBoolean("listChanged", false);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.WriteStartObject("serverInfo");
        writer.WriteString("name", "reference-mcp-server");
        writer.WriteString("version", "1.0.0");
        writer.WriteEndObject();
    }

    private string ListTools(JsonElement id, JsonElement parameters)
    {
        int offset = 0;
        if (parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty("cursor", out JsonElement cursor) && !TryReadCursor(cursor.GetString(), out offset))
        {
            return Error(id, InvalidParams, "Invalid cursor");
        }

        IEnumerable<McpReferenceTool> page = tools.Skip(offset).Take(PageSize);
        int next = offset + page.Count();
        return Result(id, writer =>
        {
            writer.WriteStartArray("tools");
            foreach (McpReferenceTool tool in page)
            {
                writer.WriteStartObject();
                writer.WriteString("name", tool.Name);
                writer.WriteString("description", tool.Description);
                writer.WriteStartObject("inputSchema");
                writer.WriteString("type", "object");
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            if (next < tools.Count)
            {
                writer.WriteString("nextCursor", CursorPrefix + next.ToString(CultureInfo.InvariantCulture));
            }
        });
    }

    private bool TryReadCursor(string? cursor, out int offset)
    {
        offset = 0;
        return cursor is not null
            && cursor.StartsWith(CursorPrefix, StringComparison.Ordinal)
            && int.TryParse(cursor.AsSpan(CursorPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out offset)
            && offset <= tools.Count;
    }

    private string CallTool(JsonElement id, JsonElement parameters)
    {
        string? name = parameters.GetProperty("name").GetString();
        McpReferenceTool? tool = tools.FirstOrDefault(candidate => candidate.Name == name);
        return tool is null
            ? Error(id, InvalidParams, $"Unknown tool: {name}")
            : Result(id, writer =>
            {
                writer.WritePropertyName("result");
                writer.WriteRawValue(tool.ResultJson);
            }, wrapInResult: false);
    }

    private static string Result(JsonElement id, Action<Utf8JsonWriter> writeResult, bool wrapInResult = true) =>
        Message(id, writer =>
        {
            if (wrapInResult)
            {
                writer.WriteStartObject("result");
                writeResult(writer);
                writer.WriteEndObject();
            }
            else
            {
                writeResult(writer);
            }
        });

    private static string Error(JsonElement id, int code, string message) =>
        Message(id, writer =>
        {
            writer.WriteStartObject("error");
            writer.WriteNumber("code", code);
            writer.WriteString("message", message);
            writer.WriteEndObject();
        });

    private static string Message(JsonElement id, Action<Utf8JsonWriter> writeBody)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("jsonrpc", "2.0");
            writer.WritePropertyName("id");
            id.WriteTo(writer);
            writeBody(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
