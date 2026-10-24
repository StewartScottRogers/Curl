using System.Diagnostics.CodeAnalysis;

namespace Curl.Mcp;

/// <summary>
/// Entry point for the Curl MCP server.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "ADR-0083: a two-line entry point that serves the real standard input and output, measured by Curl.Mcp.IntegrationTests (BL-2390).")]
internal static class Program
{
    /// <summary>
    /// Builds the host and serves MCP over standard input and output until the client closes it or a stop signal arrives.
    /// </summary>
    /// <returns>A task that completes with the process's exit code when the server stops.</returns>
    internal static async Task<int> Main()
    {
        using McpStdioHost host = CurlMcpHost.Build();
        return await host.RunAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
