using System.Text.Json;
using System.Text.Json.Serialization;

namespace Curl.Mcp.Protocol.JsonRpc;

/// <summary>The <c>error</c> object of a JSON-RPC 2.0 error response.</summary>
public sealed class JsonRpcError
{
    /// <summary>Initializes a new instance of the <see cref="JsonRpcError" /> class.</summary>
    /// <param name="code">The error code, one of <see cref="JsonRpcErrorCodes" />.</param>
    /// <param name="message">The short description of the error.</param>
    /// <param name="data">Additional information about the error, or <see langword="null" /> for none.</param>
    public JsonRpcError(int code, string message, JsonElement? data)
    {
        Code = code;
        Message = message;
        Data = data;
    }

    /// <summary>Gets the error code, one of <see cref="JsonRpcErrorCodes" />.</summary>
    [JsonPropertyName("code")]
    public int Code { get; }

    /// <summary>Gets the short description of the error.</summary>
    [JsonPropertyName("message")]
    public string Message { get; }

    /// <summary>Gets additional information about the error, or <see langword="null" />, which is not written.</summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Data { get; }
}
