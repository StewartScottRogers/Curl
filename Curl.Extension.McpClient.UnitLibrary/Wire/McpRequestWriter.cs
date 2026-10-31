using System.Text.Json;

namespace Curl.Extension.McpClient.Wire;

/// <summary>
/// Writes the JSON-RPC 2.0 messages an MCP client sends to list and call a server's tools
/// (Model Context Protocol 2025-06-18, "Base Protocol &gt; Overview", "Lifecycle" and
/// "Server Features &gt; Tools"). Each request takes the next id, starting at 1, so no id is
/// reused within the session.
/// </summary>
public sealed class McpRequestWriter
{
    /// <summary>The protocol revision this client asks for in <c>initialize</c>.</summary>
    public const string LatestProtocolVersion = "2025-06-18";

    private long lastId;

    /// <summary>
    /// Writes the <c>initialize</c> request ("Lifecycle &gt; Initialization"): the protocol version,
    /// an empty capabilities object (the client offers no roots, sampling or elicitation) and the
    /// client's name and version.
    /// </summary>
    public McpOutgoingRequest WriteInitialize(string clientName, string clientVersion)
    {
        ArgumentNullException.ThrowIfNull(clientName);
        ArgumentNullException.ThrowIfNull(clientVersion);
        return WriteRequest("initialize", writer =>
        {
            writer.WriteString("protocolVersion", LatestProtocolVersion);
            writer.WriteStartObject("capabilities");
            writer.WriteEndObject();
            writer.WriteStartObject("clientInfo");
            writer.WriteString("name", clientName);
            writer.WriteString("version", clientVersion);
            writer.WriteEndObject();
        });
    }

    /// <summary>
    /// Writes the <c>notifications/initialized</c> notification the client sends once the server
    /// has answered <c>initialize</c> ("Lifecycle &gt; Initialization"). A notification has no id.
    /// </summary>
    public static byte[] WriteInitializedNotification() =>
        WriteMessage(writer => writer.WriteString("method", "notifications/initialized"));

    /// <summary>
    /// Writes a <c>tools/list</c> request ("Server Features &gt; Tools &gt; Listing Tools"), giving
    /// <paramref name="cursor"/> as <c>params.cursor</c> when the previous page returned a
    /// <c>nextCursor</c>, and no params for the first page.
    /// </summary>
    public McpOutgoingRequest WriteListTools(string? cursor) =>
        cursor is null
            ? WriteRequest("tools/list", writeParameters: null)
            : WriteRequest("tools/list", writer => writer.WriteString("cursor", cursor));

    /// <summary>
    /// Writes a <c>tools/call</c> request ("Server Features &gt; Tools &gt; Calling Tools") for the
    /// tool <paramref name="name"/> with <paramref name="argumentsJson"/>, which must be one JSON
    /// object, as <c>params.arguments</c>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="argumentsJson"/> is not a JSON object.</exception>
    public McpOutgoingRequest WriteCallTool(string name, string argumentsJson)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(argumentsJson);
        using var arguments = ParseArgumentsObject(argumentsJson);
        return WriteRequest("tools/call", writer =>
        {
            writer.WriteString("name", name);
            writer.WritePropertyName("arguments");
            arguments.RootElement.WriteTo(writer);
        });
    }

    private static JsonDocument ParseArgumentsObject(string argumentsJson)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(argumentsJson);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("The tool arguments are not valid JSON.", nameof(argumentsJson), exception);
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            document.Dispose();
            throw new ArgumentException("The tool arguments must be a JSON object.", nameof(argumentsJson));
        }

        return document;
    }

    private McpOutgoingRequest WriteRequest(string method, Action<Utf8JsonWriter>? writeParameters)
    {
        var id = ++lastId;
        var bytes = WriteMessage(writer =>
        {
            writer.WriteNumber("id", id);
            writer.WriteString("method", method);
            if (writeParameters is not null)
            {
                writer.WriteStartObject("params");
                writeParameters(writer);
                writer.WriteEndObject();
            }
        });
        return new McpOutgoingRequest(id, bytes);
    }

    private static byte[] WriteMessage(Action<Utf8JsonWriter> writeMembers)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("jsonrpc", "2.0");
            writeMembers(writer);
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }
}
