namespace Curl.Mcp;

/// <summary>
/// A setting of the MCP server is not valid, so the server refuses to start rather than guess
/// what was meant (ADR-0548).
/// </summary>
public sealed class McpConfigurationException : Exception
{
    /// <summary>Creates the exception with no message.</summary>
    public McpConfigurationException()
    {
    }

    /// <summary>Creates the exception with <paramref name="message" />.</summary>
    /// <param name="message">Which setting is wrong and why.</param>
    public McpConfigurationException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with <paramref name="message" /> and the exception that caused it.</summary>
    /// <param name="message">Which setting is wrong and why.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public McpConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
