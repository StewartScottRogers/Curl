using Curl.Cli;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using HttpVersion = Curl.Api.Options.Http.HttpVersion;

namespace Curl.Engine;

/// <summary>
/// Maps the HTTP version of an option group (<see cref="Curl.Api.Options.Http.EasyHttpOptions.HttpVersion" />) onto
/// what the HTTP handler asks for (<see cref="HttpVersionPreference" />) and what the TLS handshake offers through
/// ALPN (<see cref="HttpApplicationProtocols" />), as ADR-0141 decides for each platform.
/// </summary>
internal static class HttpVersionMapping
{
    /// <summary>
    /// Maps the version onto the handler's preference: <see cref="HttpVersion.Http10" /> (<c>-0</c>) to
    /// <see cref="HttpVersionPreference.Http10" />, <see cref="HttpVersion.Http2" /> (<c>--http2</c>) to
    /// <see cref="HttpVersionPreference.Http2" />, so a cleartext request asks to upgrade to
    /// <c>h2c</c> (BL-865) while over TLS ALPN still decides, <see cref="HttpVersion.Http2PriorKnowledge" /> to
    /// <see cref="HttpVersionPreference.Http2PriorKnowledge" />, <see cref="HttpVersion.Http3" /> to
    /// <see cref="HttpVersionPreference.Http3" />, <see cref="HttpVersion.Http3Only" /> to
    /// <see cref="HttpVersionPreference.Http3Only" /> (ADR-0144), and every other version, <c>--http1.1</c> and
    /// <see cref="HttpVersion.None" /> (no version option) among them, to <see cref="HttpVersionPreference.Http11" />:
    /// curl's default never upgrades over cleartext.
    /// </summary>
    /// <param name="version">The option group's HTTP version.</param>
    /// <returns>The preference.</returns>
    internal static HttpVersionPreference ToHttpVersionPreference(HttpVersion version) => version switch
    {
        HttpVersion.Http10 => HttpVersionPreference.Http10,
        HttpVersion.Http2 => HttpVersionPreference.Http2,
        HttpVersion.Http2PriorKnowledge => HttpVersionPreference.Http2PriorKnowledge,
        HttpVersion.Http3 => HttpVersionPreference.Http3,
        HttpVersion.Http3Only => HttpVersionPreference.Http3Only,
        _ => HttpVersionPreference.Http11,
    };

    /// <summary>
    /// Returns what HTTP over TLS offers through ALPN on the running platform
    /// (<see cref="OperatingSystem.IsWindows" />).
    /// </summary>
    /// <param name="version">The option group's HTTP version.</param>
    /// <returns>As <see cref="HttpOverTlsApplicationProtocolsOf(HttpVersion, bool)" />.</returns>
    internal static IReadOnlyList<string> HttpOverTlsApplicationProtocolsOf(HttpVersion version) =>
        HttpOverTlsApplicationProtocolsOf(version, OperatingSystem.IsWindows());

    /// <summary>
    /// Returns what HTTP over TLS offers through ALPN, as the platform's curl offers it
    /// (measured, ADR-0141): <c>http/1.1</c> alone under <c>-0</c> and <c>--http1.1</c>,
    /// <c>h2,http/1.1</c> under <c>--http2</c> (and libcurl's <see cref="HttpVersion.Http2Tls" />), and on every
    /// platform under <c>--http3</c> and <c>--http3-only</c> for the TCP connection they fall back to or take
    /// through a proxy (measured, ADR-0144), <c>h2</c> alone under
    /// <c>--http2-prior-knowledge</c>, and with no version option (<see cref="HttpVersion.None" />)
    /// <c>http/1.1</c> alone on Windows, as the Schannel build offers, and <c>h2,http/1.1</c> elsewhere, as the
    /// OpenSSL builds with nghttp2 offer.
    /// </summary>
    /// <param name="version">The option group's HTTP version.</param>
    /// <param name="isWindows">Whether the running system is Windows.</param>
    /// <returns>One of <see cref="HttpApplicationProtocols" />' lists.</returns>
    internal static IReadOnlyList<string> HttpOverTlsApplicationProtocolsOf(HttpVersion version, bool isWindows) =>
        version == HttpVersion.None ? DefaultOffer(isWindows) : OfferedFor(version);

    /// <summary>
    /// The option group's HTTP version for a command line's <see cref="CommandLineOptions.HttpVersion" />, as
    /// <c>EasyOptionsFromCommandLine.HttpOf</c> maps it: <see cref="HttpVersion.None" /> when no version option
    /// was given. A bridge for the readers not yet handed <see cref="Curl.Api.Options.Http.EasyHttpOptions" />
    /// (<c>EngineComposition</c>, <see cref="OptionGroupConnectionSettings" /> and <see cref="AltSvcTransferCache" />),
    /// which BL-2346 and BL-2350 remove.
    /// </summary>
    /// <param name="version">The command line's version option, or <see langword="null" /> for none.</param>
    /// <returns>The same version in the option group's terms.</returns>
    internal static HttpVersion FromRequested(RequestedHttpVersion? version) => version switch
    {
        RequestedHttpVersion.Http10 => HttpVersion.Http10,
        RequestedHttpVersion.Http11 => HttpVersion.Http11,
        RequestedHttpVersion.Http2 => HttpVersion.Http2,
        RequestedHttpVersion.Http2PriorKnowledge => HttpVersion.Http2PriorKnowledge,
        RequestedHttpVersion.Http3 => HttpVersion.Http3,
        RequestedHttpVersion.Http3Only => HttpVersion.Http3Only,
        _ => HttpVersion.None,
    };

    private static IReadOnlyList<string> OfferedFor(HttpVersion version) => version switch
    {
        HttpVersion.Http2 or HttpVersion.Http2Tls or HttpVersion.Http3 or HttpVersion.Http3Only => HttpApplicationProtocols.H2ThenHttp11,
        HttpVersion.Http2PriorKnowledge => HttpApplicationProtocols.H2Only,
        _ => HttpApplicationProtocols.Http11Only,
    };

    private static IReadOnlyList<string> DefaultOffer(bool isWindows) =>
        isWindows ? HttpApplicationProtocols.Http11Only : HttpApplicationProtocols.H2ThenHttp11;
}
