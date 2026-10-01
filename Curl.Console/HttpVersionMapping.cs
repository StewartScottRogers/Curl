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
    /// <see cref="HttpVersionPreference.Http10" />, <c>--http2</c> to
    /// <see cref="HttpVersionPreference.Http2" />, so a cleartext request asks to upgrade to
    /// <c>h2c</c> (BL-865) while over TLS ALPN still decides, <c>--http2-prior-knowledge</c> to
    /// <see cref="HttpVersionPreference.Http2PriorKnowledge" />, <c>--http3</c> to
    /// <see cref="HttpVersionPreference.Http3" />, <c>--http3-only</c> to
    /// <see cref="HttpVersionPreference.Http3Only" /> (ADR-0144), and <c>--http1.1</c> and no
    /// version option to <see cref="HttpVersionPreference.Http11" />: curl's default never
    /// upgrades over cleartext.
    /// </summary>
    /// <param name="version">The command line's <see cref="CommandLineOptions.HttpVersion" />.</param>
    /// <returns>The preference.</returns>
    internal static HttpVersionPreference ToHttpVersionPreference(RequestedHttpVersion? version) => version switch
    {
        RequestedHttpVersion.Http10 => HttpVersionPreference.Http10,
        RequestedHttpVersion.Http2 => HttpVersionPreference.Http2,
        RequestedHttpVersion.Http2PriorKnowledge => HttpVersionPreference.Http2PriorKnowledge,
        RequestedHttpVersion.Http3 => HttpVersionPreference.Http3,
        RequestedHttpVersion.Http3Only => HttpVersionPreference.Http3Only,
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
    /// <c>h2,http/1.1</c> under <c>--http2</c>, and on every platform under <c>--http3</c> and
    /// <c>--http3-only</c> for the TCP connection they fall back to or take through a proxy
    /// (measured, ADR-0144), <c>h2</c> alone under
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
        RequestedHttpVersion.Http2 or RequestedHttpVersion.Http3 or RequestedHttpVersion.Http3Only => HttpApplicationProtocols.H2ThenHttp11,
        RequestedHttpVersion.Http2PriorKnowledge => HttpApplicationProtocols.H2Only,
        _ => HttpApplicationProtocols.Http11Only,
    };

    private static IReadOnlyList<string> DefaultOffer(bool isWindows) =>
        isWindows ? HttpApplicationProtocols.Http11Only : HttpApplicationProtocols.H2ThenHttp11;
}
