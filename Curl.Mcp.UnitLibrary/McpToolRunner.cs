namespace Curl.Mcp;

/// <summary>
/// Runs curl for a tool with the MCP request's cancellation token and turns what it gave back
/// into a tool result: a nonzero exit code into a tool error, output into capped text or base64,
/// and a cancelled request into the cancelled error with no output (ADR-0548).
/// </summary>
/// <param name="cap">The most bytes of output a result returns.</param>
public sealed class McpToolRunner(McpResponseCap cap)
{
    /// <summary>
    /// Runs <paramref name="run" />, unless <paramref name="cancellationToken" /> is already
    /// cancelled, and gives its tool result.
    /// </summary>
    /// <param name="run">Runs curl, passing the token through.</param>
    /// <param name="cancellationToken">The MCP request's cancellation token.</param>
    /// <returns>The tool result.</returns>
    public async Task<McpToolResult> RunAsync(
        Func<CancellationToken, Task<McpCurlRunOutput>> run,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (cancellationToken.IsCancellationRequested)
        {
            return new McpToolResult(null, McpToolFailure.Cancelled);
        }

        McpCurlRunOutput output;
        try
        {
            output = await run(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new McpToolResult(null, McpToolFailure.Cancelled);
        }

        return ToResult(output, cancellationToken);
    }

    private McpToolResult ToResult(McpCurlRunOutput output, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new McpToolResult(null, McpToolFailure.Cancelled);
        }

        McpToolFailure? failure = McpToolFailure.FromCurlExit(output.ExitCode, output.ErrorMessage);
        return failure is null
            ? new McpToolResult(McpReturnedBytes.Encode(output.Output, cap), null)
            : new McpToolResult(null, failure);
    }
}
