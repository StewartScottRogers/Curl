namespace Curl.Mcp;

/// <summary>
/// What the <c>version</c> tool gives back (ADR-0548): what the engine's <c>curl_version_info</c>
/// reports, field by field and unchanged, the schemes Curl's extensions serve, and the first line <c>curl -V</c> prints on this platform.
/// </summary>
/// <param name="Version">The engine's version text, such as <c>8.22.0</c>.</param>
/// <param name="Protocols">The lower-case schemes the engine supports, in the order it lists them.</param>
/// <param name="ExtendedProtocols">The lower-case schemes Curl's extensions serve, in the order <c>--extensions-help</c> lists them; empty when <c>CURL_EXTENSIONS</c> is <c>0</c>.</param>
/// <param name="Features">The names of the features the engine has built in, in the order it lists them.</param>
/// <param name="TlsBackend">The TLS backend's name and version; <see langword="null" /> when the engine reports none.</param>
/// <param name="VersionLine">The first line <c>curl -V</c> prints on this platform, without a line terminator.</param>
public sealed record McpVersionToolResult(
    string Version,
    IReadOnlyList<string> Protocols,
    IReadOnlyList<string> ExtendedProtocols,
    IReadOnlyList<string> Features,
    string? TlsBackend,
    string VersionLine);
