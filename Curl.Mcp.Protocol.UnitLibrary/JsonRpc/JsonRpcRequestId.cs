using System.Text.Json;

namespace Curl.Mcp.Protocol.JsonRpc;

/// <summary>
/// The <c>id</c> of a JSON-RPC 2.0 request: a JSON string or a JSON number, kept as the JSON value it
/// arrived as so the response echoes it byte for byte, however large the number.
/// </summary>
public sealed class JsonRpcRequestId
{
    internal JsonRpcRequestId(JsonElement value) => Value = value;

    /// <summary>Gets the id as it arrived: a JSON string or a JSON number.</summary>
    public JsonElement Value { get; }

    /// <summary>Gets a value indicating whether the id is a JSON string rather than a JSON number.</summary>
    public bool IsString => Value.ValueKind == JsonValueKind.String;
}
