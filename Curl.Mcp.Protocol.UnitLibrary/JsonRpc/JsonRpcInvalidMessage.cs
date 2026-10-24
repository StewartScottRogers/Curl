namespace Curl.Mcp.Protocol.JsonRpc;

/// <summary>
/// A line that is not a valid JSON-RPC 2.0 request or notification, with the error the server answers it with.
/// </summary>
public sealed class JsonRpcInvalidMessage : JsonRpcIncoming
{
    /// <summary>Initializes a new instance of the <see cref="JsonRpcInvalidMessage" /> class.</summary>
    /// <param name="error">The error the server answers with.</param>
    /// <param name="id">The message's id when it could be read, otherwise <see langword="null" />.</param>
    public JsonRpcInvalidMessage(JsonRpcError error, JsonRpcRequestId? id)
    {
        Error = error;
        Id = id;
    }

    /// <summary>
    /// Gets the error the server answers with: <see cref="JsonRpcErrorCodes.ParseError" /> or
    /// <see cref="JsonRpcErrorCodes.InvalidRequest" />.
    /// </summary>
    public JsonRpcError Error { get; }

    /// <summary>Gets the message's id when it could be read, otherwise <see langword="null" />.</summary>
    public JsonRpcRequestId? Id { get; }
}
