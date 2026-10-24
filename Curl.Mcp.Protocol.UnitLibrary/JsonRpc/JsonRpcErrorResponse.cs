using System.Text.Json;
using System.Text.Json.Serialization;

namespace Curl.Mcp.Protocol.JsonRpc;

/// <summary>A JSON-RPC 2.0 error response: the request's id, or JSON null when unknown, and the error.</summary>
public sealed class JsonRpcErrorResponse
{
    /// <summary>Initializes a new instance of the <see cref="JsonRpcErrorResponse" /> class.</summary>
    /// <param name="id">The id of the request answered, or <see langword="null" /> when it could not be read.</param>
    /// <param name="error">The error.</param>
    public JsonRpcErrorResponse(JsonElement? id, JsonRpcError error)
    {
        Id = id;
        Error = error;
    }

    /// <summary>Gets the protocol version, always <c>2.0</c>.</summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc => "2.0";

    /// <summary>Gets the id of the request answered, or <see langword="null" />, which is written as JSON null.</summary>
    [JsonPropertyName("id")]
    public JsonElement? Id { get; }

    /// <summary>Gets the error.</summary>
    [JsonPropertyName("error")]
    public JsonRpcError Error { get; }
}
