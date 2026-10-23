namespace Curl.Mcp.Protocol.Server;

/// <summary>Thrown by a method handler to answer its request with a JSON-RPC error carrying <see cref="Code" /> and the exception's message.</summary>
public sealed class McpProtocolException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="McpProtocolException" /> class.</summary>
    /// <param name="code">The JSON-RPC error code, one of <see cref="JsonRpc.JsonRpcErrorCodes" />.</param>
    /// <param name="message">The error message sent to the client.</param>
    public McpProtocolException(int code, string message)
        : base(message) => Code = code;

    /// <summary>Gets the JSON-RPC error code.</summary>
    public int Code { get; }
}
