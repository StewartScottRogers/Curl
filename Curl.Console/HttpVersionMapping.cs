using Curl.Cli;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Maps the HTTP version option of a parsed command line onto what the HTTP handler asks for
/// (<see cref="HttpVersionPreference" />) and what the TLS handshake offers through ALPN
/// (<see cref="HttpApplicationProtocols" />), as ADR-0141 decides for each platform.
/// </summary>
internal static class HttpVersionMapping
{
    /// <summary>
    /// Maps the version option onto the handler's preference: <c>-0</c> to
    /// <see cref="HttpVersionPreference.Http10" />, <c>--http2-prior-knowledge</c> to
    /// <see cref="HttpVersionPreference.Http2PriorKnowledge" />, and every other choice, none
    /// included, to <see cref="HttpVersionPreference.Http11" />: <c>--http2</c> asks for HTTP/2
    /// only through ALPN, so its request is HTTP/1.1 unless the server picks <c>h2</c>.
    /// </summary>
    /// <param name="version">The command line's <see cref="CommandLineOptions.HttpVersion" />.</param>
    /// <returns>The preference.</returns>
    internal static HttpVersionPreference ToHttpVersionPreference(RequestedHttpVersion? version) => version switch
    {
        RequestedHttpVersion.Http10 => HttpVersionPreference.Http10,
        RequestedHttpVersion.Http2PriorKnowledge => HttpVersionPreference.Http2PriorKnowledge,
        _ => HttpVersionPreference.Http11,
    };

    /// <summary>
    /// Returns what HTTP over TLS offers through ALPN on the running platform
    /// (<see cref="OperatingSystem.IsWindows" />).
    /// </summary>
    /// <param name="version">The command line's <see cref="CommandLineOptions.HttpVersion" />.</param>
    /// <returns>As <see cref="HttpOverTlsApplicationProtocolsOf(RequestedHttpVersion?, bool)" />.</returns>
    internal static IReadOnlyList<string> HttpOverTlsApplicationProtocolsOf(RequestedHttpVersion? version) =>
        HttpOverTlsApplicationProtocolsOf(version, OperatingSystem.IsWindows());

    /// <summary>
    /// Returns what HTTP over TLS offers through ALPN, as the platform's curl offers it
    /// (measured, ADR-0141): <c>http/1.1</c> alone under <c>-0</c> and <c>--http1.1</c>,
    /// <c>h2,http/1.1</c> under <c>--http2</c>, <c>h2</c> alone under
    /// <c>--http2-prior-knowledge</c>, and with no version option <c>http/1.1</c> alone on
    /// Windows, as the Schannel build offers, and <c>h2,http/1.1</c> elsewhere, as the
    /// OpenSSL builds with nghttp2 offer.
    /// </summary>
    /// <param name="version">The command line's <see cref="CommandLineOptions.HttpVersion" />.</param>
    /// <param name="isWindows">Whether the running system is Windows.</param>
    /// <returns>One of <see cref="HttpApplicationProtocols" />' lists.</returns>
    internal static IReadOnlyList<string> HttpOverTlsApplicationProtocolsOf(RequestedHttpVersion? version, bool isWindows) =>
        version is { } requested ? OfferedFor(requested) : DefaultOffer(isWindows);

    private static IReadOnlyList<string> OfferedFor(RequestedHttpVersion version) => version switch
    {
        RequestedHttpVersion.Http2 => HttpApplicationProtocols.H2ThenHttp11,
        RequestedHttpVersion.Http2PriorKnowledge => HttpApplicationProtocols.H2Only,
        _ => HttpApplicationProtocols.Http11Only,
    };

    private static IReadOnlyList<string> DefaultOffer(bool isWindows) =>
        isWindows ? HttpApplicationProtocols.Http11Only : HttpApplicationProtocols.H2ThenHttp11;
}
