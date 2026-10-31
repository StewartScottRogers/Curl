namespace Curl.Extension.Protocol.McpClient.Wire;

/// <summary>A message from the server is not the JSON-RPC 2.0 or MCP shape the specification requires.</summary>
public sealed class McpWireFormatException : Exception
{
    /// <summary>Creates the exception with no message.</summary>
    public McpWireFormatException()
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/>.</summary>
    public McpWireFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/> and the exception that caused it.</summary>
    public McpWireFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
