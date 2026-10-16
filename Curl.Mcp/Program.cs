using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting;

namespace Curl.Mcp;

/// <summary>
/// Entry point for the Curl MCP server.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "ADR-0083: a one-line entry point that serves the real standard input and output, measured by Curl.Mcp.IntegrationTests (BL-2390).")]
internal static class Program
{
    /// <summary>
    /// Builds the host and serves MCP over standard input and output until the client closes it.
    /// </summary>
    /// <param name="args">The arguments as received from the shell.</param>
    /// <returns>A task that completes when the server stops.</returns>
    internal static Task Main(string[] args) => CurlMcpHost.Build(args).RunAsync();
}
