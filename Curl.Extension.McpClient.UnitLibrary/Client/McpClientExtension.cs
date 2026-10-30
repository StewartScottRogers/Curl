using Curl.Extension.Abstractions.Description;
using Curl.Extension.McpClient.Stdio;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.McpClient.Client;

/// <summary>The <c>mcp://</c>, <c>mcps://</c> and <c>mcp+stdio://</c> extension's description and handler factory (ADR-1074).</summary>
public static class McpClientExtension
{
    /// <summary>Gets what <c>curl --extensions</c> and <c>curl --ai-help extensions</c> say of it.</summary>
    public static ExtensionDescription Description { get; } = new(
        "mcp",
        "Lists and calls a Model Context Protocol server's tools (MCP 2025-06-18): mcp:// and mcps:// reach a Streamable HTTP endpoint, mcp+stdio://localhost/command?arg1&arg2 starts a local server; without -d it writes one line per tool (name, tab, description), with -d '{\"name\":\"tool\",\"arguments\":{...}}' it calls the tool and writes each content item on its own line, exit 22 when the tool reports an error.",
        ["mcp", "mcps", "mcp+stdio"],
        [],
        new Uri("https://modelcontextprotocol.io/specification/2025-06-18"));

    /// <summary>Creates the handler over curl's connector and the system's process starter.</summary>
    /// <param name="services">Curl's connection services.</param>
    /// <returns>The handler.</returns>
    public static IProtocolHandler CreateHandler(ProtocolHandlerServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new McpProtocolHandler(services.Connector, new McpServerProcessStarter());
    }
}
