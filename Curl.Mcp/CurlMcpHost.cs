using Curl.Engine;
using Curl.Extension.Composition;
using Curl.Protocol.Abstractions;
using Curl.Mcp.Protocol.Model;
using Curl.Mcp.Protocol.Resources;
using Curl.Mcp.Protocol.Server;
using Curl.Mcp.Protocol.Tools;
using Curl.Mcp.Tools;

namespace Curl.Mcp;

/// <summary>
/// Composes the hand-written MCP server that serves Curl over standard input and output (ADR-0548, BL-3388).
/// </summary>
internal static class CurlMcpHost
{
    /// <summary>
    /// The name and version <c>initialize</c> answers with: the assembly's name and version, as the SDK host
    /// this one replaced answered.
    /// </summary>
    internal static readonly McpServerInfo ServerInfo = new(
        typeof(CurlMcpHost).Assembly.GetName().Name!,
        typeof(CurlMcpHost).Assembly.GetName().Version!.ToString());

    /// <summary>
    /// Builds the host over the process's environment, standard input, standard output and standard error, as
    /// <see cref="Build(Func{string, string?}, Stream, Stream, TextWriter)" /> does.
    /// </summary>
    /// <returns>The host, not yet running.</returns>
    internal static McpStdioHost Build() =>
        Build(Environment.GetEnvironmentVariable, System.Console.OpenStandardInput(), System.Console.OpenStandardOutput(), System.Console.Error);

    /// <summary>
    /// Builds the host: an <see cref="McpStdioHost" /> that reads <c>CURL_MCP_ROOT</c> and
    /// <c>CURL_MCP_MAX_RESPONSE_BYTES</c> once when it runs, so a bad value stops the server before it serves, and
    /// makes the root the process's current directory, so curl's relative paths resolve inside it (ADR-0575).
    /// Every log line goes to <paramref name="error" />; <paramref name="output" /> carries only the protocol.
    /// The tools and resources are those of <see cref="CreateRegistry" />.
    /// </summary>
    /// <param name="readEnvironmentVariable">Reads an environment variable, <see langword="null" /> when it is unset.</param>
    /// <param name="input">The stream the client's messages are read from.</param>
    /// <param name="output">The stream the protocol messages are written to.</param>
    /// <param name="error">Where the log lines go.</param>
    /// <returns>The host, not yet running.</returns>
    internal static McpStdioHost Build(Func<string, string?> readEnvironmentVariable, Stream input, Stream output, TextWriter error)
    {
        McpStandardErrorLog log = new(error, TimeProvider.System);
        return new(
            ServerInfo,
            (root, cap) => CreateHandlers(root, cap, readEnvironmentVariable, log.WriteLine),
            input,
            output,
            log,
            readEnvironmentVariable,
            TimeProvider.System,
            McpStdioHost.RegisterProcessStopSignals);
    }

    /// <summary>
    /// Builds the handlers of <c>tools/list</c>, <c>tools/call</c>, <c>resources/list</c> and
    /// <c>resources/read</c> over one <see cref="CreateRegistry" /> registry, and <c>logging/setLevel</c>.
    /// </summary>
    /// <param name="root">The folder every file the <c>curl</c> tool touches must lie in.</param>
    /// <param name="cap">The most bytes a tool returns.</param>
    /// <param name="readEnvironmentVariable">Reads <c>CURL_EXTENSIONS</c>, which turns Curl's extensions off at <c>0</c>.</param>
    /// <param name="logFailure">Receives one line for each exception a tool throws.</param>
    /// <returns>The five handlers.</returns>
    internal static IEnumerable<IMcpMethodHandler> CreateHandlers(McpConfinementRoot root, McpResponseCap cap, Func<string, string?> readEnvironmentVariable, Action<string> logFailure)
    {
        McpServerRegistry registry = CreateRegistry(root, cap, readEnvironmentVariable, ExtensionProtocolHandlers.Create);
        return [new ToolsListHandler(registry), new ToolsCallHandler(registry, logFailure), new ResourcesListHandler(registry), new ResourcesReadHandler(registry), new LoggingSetLevelMcpHandler()];
    }

    /// <summary>
    /// Registers the tools and resources one by one, never by scanning an assembly: the <c>curl</c> tool over
    /// <see cref="InProcessCurlCommandRunner" />, the <c>transfer</c> tool over a production
    /// <see cref="ManagedCurlEngine" /> that lives as long as the process, as <c>Libcurl</c>'s does, the
    /// <c>version</c> tool over that same engine, the <c>parse_url</c> and <c>build_url</c> tools over that engine's
    /// URL handles, and the <c>--ai-help</c> resources through <see cref="AiHelpMcpResourceProvider" /> (ADR-0589), whose
    /// <c>curl://ai-help/extensions</c> reads <see cref="ExtensionListing.Markdown" /> in that environment (BL-4450).
    /// The engine's transfers reach the handlers <paramref name="extensionHandlers" /> builds, as the <c>curl</c>
    /// tool's reach Curl's extensions, unless <paramref name="readEnvironmentVariable" /> sets
    /// <c>CURL_EXTENSIONS</c> to <c>0</c> (ADR-0999, ADR-1071, BL-4098); the URL tools accept, and the <c>version</c> tool
    /// lists, <see cref="ExtensionListing.Schemes" /> in that environment, as they do curl's own schemes (BL-4451).
    /// </summary>
    /// <param name="root">The folder every file the <c>curl</c> tool touches must lie in.</param>
    /// <param name="cap">The most bytes a tool returns.</param>
    /// <param name="readEnvironmentVariable">Reads <c>CURL_EXTENSIONS</c>.</param>
    /// <param name="extensionHandlers">Builds the extensions' handlers.</param>
    /// <returns>The registry of the five tools and the resources.</returns>
    internal static McpServerRegistry CreateRegistry(
        McpConfinementRoot root,
        McpResponseCap cap,
        Func<string, string?> readEnvironmentVariable,
        Func<ProtocolHandlerServices, IEnumerable<IProtocolHandler>> extensionHandlers)
    {
        // One engine for the process, shared by the transfer, version and URL tools.
        ManagedCurlEngine engine = new(ExtensionProtocolHandlers.For(readEnvironmentVariable) is null ? null : extensionHandlers);
        IReadOnlyList<string> extendedSchemes = ExtensionListing.Schemes(readEnvironmentVariable);
        McpUrlTool urlTool = new(engine.CreateUrl, extendedSchemes);
        McpServerRegistry registry = new();
        // tools/list answers in registration order; this is the order the SDK host listed them in (the golden tools-list).
        registry.Add(new ParseUrlMcpTool(urlTool));
        registry.Add(new TransferMcpTool(new McpTransferTool(engine, cap)));
        registry.Add(new CurlCommandMcpTool(new McpCurlTool(new InProcessCurlCommandRunner(), root, cap)));
        registry.Add(new BuildUrlMcpTool(urlTool));
        registry.Add(new VersionMcpTool(new McpVersionTool(engine, extendedSchemes)));
        registry.Add(new AiHelpMcpResourceProvider(() => ExtensionListing.Markdown(readEnvironmentVariable)));
        return registry;
    }
}
