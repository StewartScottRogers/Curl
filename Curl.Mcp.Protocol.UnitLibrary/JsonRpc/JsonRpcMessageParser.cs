using System.Text.Json;
using System.Text.Unicode;

namespace Curl.Mcp.Protocol.JsonRpc;

/// <summary>
/// Reads one line of UTF-8 from the client as a JSON-RPC 2.0 request or notification. Never throws on
/// malformed input: what is not a valid message becomes a <see cref="JsonRpcInvalidMessage" />.
/// </summary>
public sealed class JsonRpcMessageParser
{
    private static readonly JsonRpcError ParseError = new(JsonRpcErrorCodes.ParseError, "Parse error", null);

    private static readonly JsonRpcError InvalidRequest = new(JsonRpcErrorCodes.InvalidRequest, "Invalid Request", null);

    /// <summary>Reads one line as a request, a notification or an invalid message.</summary>
    /// <param name="line">The line's UTF-8 bytes, without its line terminator.</param>
    /// <returns>
    /// A <see cref="JsonRpcRequest" /> or <see cref="JsonRpcNotification" />; or a
    /// <see cref="JsonRpcInvalidMessage" /> carrying <see cref="JsonRpcErrorCodes.ParseError" /> for input that
    /// is not UTF-8 JSON or nests deeper than 64 levels, or <see cref="JsonRpcErrorCodes.InvalidRequest" />
    /// for JSON that is not a request or notification, with the id when it could be read. A key given
    /// twice takes its last value.
    /// </returns>
    public JsonRpcIncoming Parse(ReadOnlyMemory<byte> line)
    {
        // JsonDocument checks the UTF-8 of a string only when the string is read, so check the whole line first.
        if (!Utf8.IsValid(line.Span))
        {
            return new JsonRpcInvalidMessage(ParseError, null);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return new JsonRpcInvalidMessage(ParseError, null);
        }

        using (document)
        {
            return ReadMessage(document.RootElement);
        }
    }

    private static JsonRpcIncoming ReadMessage(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return new JsonRpcInvalidMessage(InvalidRequest, null);
        }

        if (!root.TryGetProperty("id", out JsonElement idElement))
        {
            return ReadCall(root, null);
        }

        return idElement.ValueKind is JsonValueKind.String or JsonValueKind.Number
            ? ReadCall(root, new JsonRpcRequestId(idElement.Clone()))
            : new JsonRpcInvalidMessage(InvalidRequest, null);
    }

    private static JsonRpcIncoming ReadCall(JsonElement root, JsonRpcRequestId? id)
    {
        if (!HasVersion(root) || !TryReadMethod(root, out string method) || !TryReadParams(root, out JsonElement? parameters))
        {
            return new JsonRpcInvalidMessage(InvalidRequest, id);
        }

        return id is null ? new JsonRpcNotification(method, parameters) : new JsonRpcRequest(id, method, parameters);
    }

    private static bool HasVersion(JsonElement root) =>
        root.TryGetProperty("jsonrpc", out JsonElement version)
        && version.ValueKind == JsonValueKind.String
        && version.ValueEquals("2.0");

    private static bool TryReadMethod(JsonElement root, out string method)
    {
        method = string.Empty;
        if (!root.TryGetProperty("method", out JsonElement element) || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        method = element.GetString()!;
        return true;
    }

    private static bool TryReadParams(JsonElement root, out JsonElement? parameters)
    {
        parameters = null;
        if (!root.TryGetProperty("params", out JsonElement element) || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
        {
            return false;
        }

        parameters = element.Clone();
        return true;
    }
}
