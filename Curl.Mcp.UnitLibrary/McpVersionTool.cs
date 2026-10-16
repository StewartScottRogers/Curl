using Curl.Api.EngineContracts;
using Curl.Cli;

namespace Curl.Mcp;

/// <summary>
/// The logic of the MCP tool <c>version</c> (ADR-0548): it reports the injected
/// <see cref="ICurlEngine" />'s <see cref="CurlVersionInfo" /> unchanged, with the first line of
/// <see cref="CurlVersionText" /> for the running platform (the Schannel build on Windows).
/// </summary>
/// <param name="engine">The transfer engine whose version is reported.</param>
public sealed class McpVersionTool(ICurlEngine engine)
{
    /// <summary>Reports the engine's version, protocols, features and TLS backend, and <c>curl -V</c>'s first line.</summary>
    /// <returns>The tool's result.</returns>
    public McpVersionToolResult Report()
    {
        CurlVersionInfo info = engine.VersionInfo;
        string versionLine = CurlVersionText.Lines(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS())[0];
        return new McpVersionToolResult(info.Version, info.Protocols, info.Features, info.TlsBackend, versionLine);
    }
}
