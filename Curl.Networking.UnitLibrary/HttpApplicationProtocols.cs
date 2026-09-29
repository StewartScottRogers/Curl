namespace Curl.Networking;

/// <summary>
/// The ALPN lists an HTTP-over-TLS handshake to the origin offers, one per HTTP version
/// choice ADR-0141 gives: <see cref="Http11Only" />, <see cref="H2ThenHttp11" /> and
/// <see cref="H2Only" />. <see cref="TcpConnector" /> offers the one it was built with, and
/// <see cref="Http11Only" /> to an HTTPS proxy.
/// </summary>
public static class HttpApplicationProtocols
{
    /// <summary>
    /// Gets <c>http/1.1</c> alone: curl's offer under <c>--http1.1</c> and <c>--http1.0</c>
    /// everywhere, and with no version option on Windows, as the Schannel build offers it
    /// (measured, BL-490 and ADR-0141); and, whatever the version options, curl's offer to an
    /// HTTPS proxy on every platform (measured, BL-753 and ADR-0190).
    /// </summary>
    public static IReadOnlyList<string> Http11Only { get; } = ["http/1.1"];

    /// <summary>
    /// Gets <c>h2</c> then <c>http/1.1</c>: curl's offer under <c>--http2</c> everywhere, and
    /// with no version option on Linux and macOS, as the nghttp2 builds offer it (measured,
    /// ADR-0141).
    /// </summary>
    public static IReadOnlyList<string> H2ThenHttp11 { get; } = ["h2", "http/1.1"];

    /// <summary>
    /// Gets <c>h2</c> alone: curl's offer under <c>--http2-prior-knowledge</c> (measured,
    /// ADR-0141).
    /// </summary>
    public static IReadOnlyList<string> H2Only { get; } = ["h2"];
}
