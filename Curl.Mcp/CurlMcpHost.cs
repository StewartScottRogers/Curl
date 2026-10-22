using Curl.Engine;
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
    /// Builds the host over the process's environment, as <see cref="Build(string[], Func{string, string?})" /> does.
    /// </summary>
    /// <param name="args">The arguments as received from the shell.</param>
    /// <returns>The built host, not yet started.</returns>
    internal static IHost Build(string[] args) => Build(args, Environment.GetEnvironmentVariable);

    /// <summary>
    /// Builds the host: the MCP server on the stdio transport, with every log line sent to
    /// standard error, because standard output carries only the protocol. It reads
    /// <c>CURL_MCP_ROOT</c> and <c>CURL_MCP_MAX_RESPONSE_BYTES</c> once, so a bad value stops the
    /// server before it starts, and makes the root the process's current directory, so curl's
    /// relative paths resolve inside it (ADR-0575). Tools are registered one by one, never by
    /// scanning an assembly: the <c>curl</c> tool over <see cref="InProcessCurlCommandRunner" />, the <c>transfer</c>
    /// tool over a production <see cref="ManagedCurlEngine" /> that lives as long as the process, as
    /// <c>Libcurl</c>'s does, the <c>version</c> tool over that same engine, the <c>parse_url</c> and <c>build_url</c> tools
    /// over that engine's URL handles, and the <c>--ai-help</c>
    /// resources through <see cref="CurlAiHelpResourceHandlers" /> (ADR-0589).
    /// </summary>
    /// <param name="args">The arguments as received from the shell.</param>
    /// <param name="readEnvironmentVariable">Reads an environment variable, <see langword="null" /> when it is unset.</param>
    /// <returns>The built host, not yet started.</returns>
    /// <exception cref="ArgumentException"><c>CURL_MCP_ROOT</c> is set but names no existing absolute folder.</exception>
    /// <exception cref="McpConfigurationException"><c>CURL_MCP_MAX_RESPONSE_BYTES</c> is not a positive whole number.</exception>
    internal static IHost Build(string[] args, Func<string, string?> readEnvironmentVariable)
    {
        McpConfinementRoot root = McpConfinementRoot.FromEnvironment(readEnvironmentVariable);
        McpResponseCap cap = McpResponseCap.FromEnvironment(readEnvironmentVariable);
        Directory.SetCurrentDirectory(root.ResolvedRoot ?? Directory.GetCurrentDirectory());
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        // Drop the default console provider: it writes to standard output, which carries only the protocol.
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Services.AddSingleton(new McpCurlTool(new InProcessCurlCommandRunner(), root, cap));
        // One engine for the process, shared by the transfer, version and URL tools.
        ManagedCurlEngine engine = new();
        builder.Services.AddSingleton(new McpTransferTool(engine, cap));
        builder.Services.AddSingleton(new McpVersionTool(engine));
        builder.Services.AddSingleton(new McpUrlTool(engine.CreateUrl));
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<CurlCommandTool>(CurlMcpJsonContext.ToolOptions)
            .WithTools<CurlTransferTool>(CurlMcpJsonContext.ToolOptions)
            .WithTools<CurlVersionTool>(CurlMcpJsonContext.ToolOptions)
            .WithTools<CurlUrlTool>(CurlMcpJsonContext.ToolOptions)
            .WithListResourcesHandler(CurlAiHelpResourceHandlers.ListAsync)
            .WithReadResourceHandler(CurlAiHelpResourceHandlers.ReadAsync);
        return builder.Build();
    }
}
