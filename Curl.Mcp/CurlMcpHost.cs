using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Curl.Mcp;

/// <summary>
/// Builds the generic host that serves Curl as a Model Context Protocol server over
/// standard input and output (ADR-0548).
/// </summary>
internal static class CurlMcpHost
{
    /// <summary>
    /// Builds the host: the MCP server on the stdio transport, with every log line sent to
    /// standard error, because standard output carries only the protocol. Tools are
    /// registered one by one, never by scanning an assembly; none is registered yet.
    /// </summary>
    /// <param name="args">The arguments as received from the shell.</param>
    /// <returns>The built host, not yet started.</returns>
    internal static IHost Build(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport();
        return builder.Build();
    }
}
