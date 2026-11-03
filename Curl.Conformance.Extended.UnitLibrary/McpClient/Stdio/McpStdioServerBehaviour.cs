namespace Curl.Conformance.Extended.McpClient.Stdio;

/// <summary>How a <see cref="McpStdioReferenceProcess" /> departs from a well-behaved stdio MCP server; the default departs in nothing.</summary>
public sealed record McpStdioServerBehaviour
{
    /// <summary>Gets the 1-based number of the message the server exits on without answering it; 0 for none.</summary>
    public int ExitBeforeAnsweringMessage { get; init; }

    /// <summary>Gets text written to standard output before each answer, such as a line that is not JSON; empty for none.</summary>
    public string LineBeforeEachAnswer { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the server keeps running after its standard input closes, until it is killed.</summary>
    public bool IgnoresClosedStandardInput { get; init; }
}
