namespace Curl.Mcp.Protocol.JsonRpc;

/// <summary>
/// The JSON-RPC 2.0 error codes the server answers with: the five the JSON-RPC specification defines
/// and the Model Context Protocol's resource-not-found code.
/// </summary>
public static class JsonRpcErrorCodes
{
    /// <summary>The line is not valid JSON, or not valid UTF-8.</summary>
    public const int ParseError = -32700;

    /// <summary>The JSON is not a valid JSON-RPC 2.0 request or notification.</summary>
    public const int InvalidRequest = -32600;

    /// <summary>The method does not exist or is not available.</summary>
    public const int MethodNotFound = -32601;

    /// <summary>The method's parameters are invalid.</summary>
    public const int InvalidParams = -32602;

    /// <summary>The server failed while handling the request.</summary>
    public const int InternalError = -32603;

    /// <summary>The Model Context Protocol's code for a resource URI the server does not have.</summary>
    public const int ResourceNotFound = -32002;
}
