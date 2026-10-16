using System.Text;

namespace Curl.Mcp;

/// <summary>
/// The logic of the MCP tool <c>curl</c> (ADR-0548, ADR-0569): it checks the argument array against
/// <c>CURL_MCP_ROOT</c> with <see cref="McpArgumentChecker" />, and only when every path lies inside
/// runs it through <see cref="ICurlCommandRunner" /> with the root as working directory, the given
/// standard input (empty when none), and standard output and error captured in memory up to the cap.
/// Every argument is passed on unchanged, so <c>--help</c> and <c>--ai-help</c> output comes back as
/// curl wrote it, and a leading <c>curl</c> is a URL, as curl itself would read it.
/// </summary>
/// <param name="runner">Runs curl in-process.</param>
/// <param name="root">The folder every path must lie in; <see cref="McpConfinementRoot.NotConfigured" /> refuses every path but still allows transfers that touch no file.</param>
/// <param name="cap">The most bytes of standard output, and of standard error, the tool returns.</param>
public sealed class McpCurlTool(ICurlCommandRunner runner, McpConfinementRoot root, McpResponseCap cap)
{
    private readonly McpArgumentChecker checker = new(root);

    /// <summary>
    /// Runs <paramref name="arguments" /> as curl would run them, unless a path they name lies outside
    /// the root or <paramref name="cancellationToken" /> is cancelled first.
    /// </summary>
    /// <param name="arguments">The command line without the program name.</param>
    /// <param name="standardInput">The text curl reads as standard input, as UTF-8; <see langword="null" /> for none.</param>
    /// <param name="cancellationToken">The MCP request's cancellation token, passed to the runner.</param>
    /// <returns>The tool's result.</returns>
    public async Task<McpCurlToolResult> RunAsync(IReadOnlyList<string> arguments, string? standardInput, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        McpArgumentCheck check = checker.Check(arguments);
        if (!check.IsAllowed)
        {
            return McpCurlToolResult.NotRun(McpToolFailure.PathRefused(check.Option!, check.Reason));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return McpCurlToolResult.NotRun(McpToolFailure.Cancelled);
        }

        using MemoryStream input = new(Encoding.UTF8.GetBytes(standardInput ?? string.Empty), writable: false);
        using McpCappedCaptureStream output = new(cap);
        using McpCappedCaptureStream error = new(cap);
        int exitCode;
        try
        {
            exitCode = await runner.RunAsync(arguments, input, output, error, root.ResolvedRoot, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return McpCurlToolResult.NotRun(McpToolFailure.Cancelled);
        }

        return ToResult(exitCode, output.ToReturnedBytes(), error.ToReturnedBytes(), cancellationToken);
    }

    private static McpCurlToolResult ToResult(int exitCode, McpReturnedBytes output, McpReturnedBytes error, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return McpCurlToolResult.NotRun(McpToolFailure.Cancelled);
        }

        string errorText = error.IsBase64 ? string.Empty : error.Content;
        return new McpCurlToolResult(exitCode, output, error, McpToolFailure.FromCurlExit(exitCode, errorText));
    }
}
