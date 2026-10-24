using System.Text.Json;
using System.Text.Json.Serialization;

namespace Curl.Mcp.Protocol.JsonRpc;

/// <summary>A JSON-RPC 2.0 success response: the request's id and the method's result.</summary>
public sealed class JsonRpcSuccessResponse
{
    /// <summary>Initializes a new instance of the <see cref="JsonRpcSuccessResponse" /> class.</summary>
    /// <param name="id">The id of the request answered.</param>
    /// <param name="result">The method's result.</param>
    public JsonRpcSuccessResponse(JsonElement id, JsonElement result)
    {
        Id = id;
        Result = result;
    }

    /// <summary>Gets the protocol version, always <c>2.0</c>.</summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc => "2.0";

    /// <summary>Gets the id of the request answered.</summary>
    [JsonPropertyName("id")]
    public JsonElement Id { get; }

    /// <summary>Gets the method's result.</summary>
    [JsonPropertyName("result")]
    public JsonElement Result { get; }
}
