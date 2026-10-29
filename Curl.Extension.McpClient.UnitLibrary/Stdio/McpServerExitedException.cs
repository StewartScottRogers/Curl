namespace Curl.Extension.McpClient.Stdio;

/// <summary>The MCP server closed its standard output, so it exited, before it sent the awaited response.</summary>
public sealed class McpServerExitedException : Exception
{
    /// <summary>Creates the exception with no message.</summary>
    public McpServerExitedException()
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/>.</summary>
    public McpServerExitedException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/> and the exception that caused it.</summary>
    public McpServerExitedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
