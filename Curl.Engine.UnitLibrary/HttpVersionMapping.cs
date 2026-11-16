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
    /// Returns what HTTP over TLS offers through ALPN, as curl 8.21.0 offers it: <c>http/1.1</c> alone under
    /// <c>-0</c> and <c>--http1.1</c>, <c>h2,http/1.1</c> under <c>--http2</c> (and libcurl's
    /// <see cref="HttpVersion.Http2Tls" />), under <c>--http3</c> and <c>--http3-only</c> for the TCP connection they
    /// fall back to or take through a proxy (measured, ADR-0144), and with no version option
    /// (<see cref="HttpVersion.None" />) on every platform, because curl negotiates HTTP/2 in the TLS handshake by
    /// default (ADR-1273, which amends ADR-0141 for Windows), and <c>h2</c> alone under <c>--http2-prior-knowledge</c>.
    /// </summary>
    /// <param name="version">The option group's HTTP version.</param>
    /// <returns>One of <see cref="HttpApplicationProtocols" />' lists.</returns>
    internal static IReadOnlyList<string> HttpOverTlsApplicationProtocolsOf(HttpVersion version) =>
        version == HttpVersion.None ? HttpApplicationProtocols.H2ThenHttp11 : OfferedFor(version);

    private static IReadOnlyList<string> OfferedFor(HttpVersion version) => version switch
    {
        HttpVersion.Http2 or HttpVersion.Http2Tls or HttpVersion.Http3 or HttpVersion.Http3Only => HttpApplicationProtocols.H2ThenHttp11,
        HttpVersion.Http2PriorKnowledge => HttpApplicationProtocols.H2Only,
        _ => HttpApplicationProtocols.Http11Only,
    };
}
