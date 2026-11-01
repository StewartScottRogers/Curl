using Curl.Api.EngineContracts;
using Curl.Cli;

namespace Curl.Mcp;

/// <summary>
/// The logic of the MCP tool <c>version</c> (ADR-0548): it reports the injected
/// <see cref="ICurlEngine" />'s <see cref="CurlVersionInfo" /> unchanged, with the first line of
/// <see cref="CurlVersionText" /> for the running platform (the Schannel build on Windows), and the extended
/// protocols in a field of their own, so <c>protocols</c> stays curl's (ADR-0999).
/// </summary>
/// <param name="engine">The transfer engine whose version is reported.</param>
/// <param name="extendedProtocols">The schemes Curl's extensions serve, reported beside the engine's own (BL-4451).</param>
public sealed class McpVersionTool(ICurlEngine engine, IReadOnlyList<string> extendedProtocols)
{
    /// <summary>Reports the engine's version, protocols, extended protocols, features and TLS backend, and <c>curl -V</c>'s first line.</summary>
    /// <returns>The tool's result.</returns>
    public McpVersionToolResult Report()
    {
        CurlVersionInfo info = engine.VersionInfo;
        string versionLine = CurlVersionText.Lines(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS())[0];
        return new McpVersionToolResult(info.Version, info.Protocols, extendedProtocols, info.Features, info.TlsBackend, versionLine);
    }
}
