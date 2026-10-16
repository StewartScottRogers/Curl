namespace Curl.Mcp;

/// <summary>
/// What one curl run gave back, before it is turned into a tool result.
/// </summary>
/// <param name="ExitCode">curl's exit code.</param>
/// <param name="Output">The output bytes the tool returns.</param>
/// <param name="ErrorMessage">curl's own error message, as it wrote it to standard error; empty when it wrote none.</param>
public sealed record McpCurlRunOutput(int ExitCode, byte[] Output, string ErrorMessage);
