namespace Curl.Mcp;

/// <summary>
/// A tool's result: either its output or a tool error, never both (ADR-0548).
/// </summary>
/// <param name="Output">The returned output; <see langword="null" /> when the tool failed.</param>
/// <param name="Failure">The tool error; <see langword="null" /> when the tool succeeded.</param>
public sealed record McpToolResult(McpReturnedBytes? Output, McpToolFailure? Failure)
{
    /// <summary>Whether the result is a tool error.</summary>
    public bool IsError => Failure is not null;
}
