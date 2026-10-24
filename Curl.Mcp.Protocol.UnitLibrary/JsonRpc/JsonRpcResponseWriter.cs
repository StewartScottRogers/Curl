using System.Text.Json;

namespace Curl.Mcp.Protocol.JsonRpc;

/// <summary>
/// Writes JSON-RPC 2.0 responses as UTF-8 JSON on one line, with no line terminator: the transport adds it.
/// </summary>
public sealed class JsonRpcResponseWriter
{
    /// <summary>Writes a success response.</summary>
    /// <param name="id">The id of the request answered.</param>
    /// <param name="result">The method's result.</param>
    /// <returns>The response's UTF-8 JSON.</returns>
    public byte[] WriteSuccess(JsonRpcRequestId id, JsonElement result) =>
        JsonSerializer.SerializeToUtf8Bytes(new JsonRpcSuccessResponse(id.Value, result), JsonRpcJsonContext.Default.JsonRpcSuccessResponse);

    /// <summary>Writes an error response.</summary>
    /// <param name="id">The id of the request answered, or <see langword="null" /> to write JSON null.</param>
    /// <param name="error">The error.</param>
    /// <returns>The response's UTF-8 JSON.</returns>
    public byte[] WriteError(JsonRpcRequestId? id, JsonRpcError error) =>
        JsonSerializer.SerializeToUtf8Bytes(new JsonRpcErrorResponse(id?.Value, error), JsonRpcJsonContext.Default.JsonRpcErrorResponse);

    /// <summary>Writes the error response that answers a message the parser found invalid.</summary>
    /// <param name="message">The invalid message.</param>
    /// <returns>The response's UTF-8 JSON.</returns>
    public byte[] WriteError(JsonRpcInvalidMessage message) => WriteError(message.Id, message.Error);
}
