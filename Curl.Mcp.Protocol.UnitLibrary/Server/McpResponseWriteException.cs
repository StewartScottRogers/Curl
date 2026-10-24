namespace Curl.Mcp.Protocol.Server;

/// <summary>
/// The failure <see cref="McpServer.RunAsync" /> ends with when writing a response to the transport failed,
/// so a host can tell it from a failure of reading the input. The transport's own exception is the inner one.
/// </summary>
public sealed class McpResponseWriteException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="McpResponseWriteException" /> class.</summary>
    /// <param name="innerException">The exception the transport's write threw.</param>
    public McpResponseWriteException(Exception innerException)
        : base("Writing a response failed.", innerException)
    {
    }
}
