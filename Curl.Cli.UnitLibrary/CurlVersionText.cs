namespace Curl.Cli;

/// <summary>
/// The four lines <c>-V</c> / <c>--version</c> prints, as ADR-0021 decides: curl 8.21.0's version,
/// release date and format, the TLS backend <c>SslStream</c> sits on as the one library named, and
/// <c>Protocols:</c> and <c>Features:</c> listing only what Curl serves. The lines carry no line
/// terminator; the console layer chooses the newline (CRLF on Windows, as the mingw reference writes).
/// </summary>
/// <remarks>
/// Keep <see cref="ProtocolsLine"/> and <see cref="FeaturesLine"/> current (ADR-0021, Decision 6): the task
/// that registers a handler in <c>CurlComposition.CreateProtocolHandlers</c>, or lands a feature curl
/// lists, adds it here and to <c>CurlVersionTextTests</c> in the same change.
/// </remarks>
public static class CurlVersionText
{
    /// <summary>The second line, the reference build's release date.</summary>
    public const string ReleaseDateLine = "Release-Date: 2026-06-24";

    /// <summary>
    /// The third line: the schemes the registered handlers serve, in curl's alphabetical order —
    /// <c>file</c>, <c>dict</c>, <c>ftp</c>/<c>ftps</c>, <c>gopher</c>/<c>gophers</c>, <c>telnet</c>, <c>tftp</c>,
    /// <c>imap</c>/<c>imaps</c>, <c>mqtt</c>/<c>mqtts</c>, <c>pop3</c>/<c>pop3s</c>, <c>rtsp</c>, <c>scp</c>/<c>sftp</c>, <c>smb</c>/<c>smbs</c>, <c>smtp</c>/<c>smtps</c>, <c>ws</c>/<c>wss</c> and <c>http</c>/<c>https</c> —
    /// and <c>ipfs</c>/<c>ipns</c>, which the tool serves by rewriting to a gateway as curl does and which
    /// every curl 8.21.0 build lists (ADR-0021 amendment, BL-1417).
    /// </summary>
    public const string ProtocolsLine = "Protocols: dict file ftp ftps gopher gophers http https imap imaps ipfs ipns ldap ldaps mqtt mqtts pop3 pop3s rtsp scp sftp smb smbs smtp smtps telnet tftp ws wss";

    /// <summary>
    /// The fourth line: the curl features the code gives evidence for, in curl's order (alphabetical,
    /// ignoring case). ADR-0021's four, plus <c>brotli</c> and <c>libz</c> now that the registered
    /// HTTP handler decodes <c>br</c>, <c>gzip</c> and <c>deflate</c> bodies, and <c>HTTP2</c> on every
    /// platform now that <c>--http2</c> is accepted (ADR-0141, Decision 5), and <c>GSS-API</c>,
    /// <c>Kerberos</c> and <c>SPNEGO</c> on every platform now that <c>--negotiate</c> is answered
    /// (ADR-0142, ADR-0176), <c>NTLM</c> on every platform now that <c>--ntlm</c> is (ADR-0181), and
    /// <c>HTTP3</c> on every platform now that <c>--http3</c> and <c>--http3-only</c> work (ADR-0144), and
    /// <c>TLS-SRP</c> on every platform now that <c>--tlsuser</c> and <c>--proxy-tlsuser</c> authenticate the
    /// hand-built handshake with SRP (ADR-0328, BL-1135), after <c>SSL</c> as curl 8.18.0's OpenSSL build lists it.
    /// BL-1417's audit added <c>alt-svc</c>, <c>ECH</c>, <c>HSTS</c>, <c>HTTPS-proxy</c>, <c>HTTPSRR</c>, <c>IDN</c>,
    /// <c>PSL</c>, <c>UnixSockets</c> and <c>zstd</c>, each with evidence in the code (ADR-0021 amendment).
    /// <c>threadsafe</c> on every platform, after <c>SSL</c> (and <c>SSPI</c>) as both reference builds list it,
    /// because Curl's process-wide set-up is thread-safe (ADR-0444, BL-1828).
    /// </summary>
    public const string FeaturesLine = "Features: alt-svc AsynchDNS brotli ECH GSS-API HSTS HTTP2 HTTP3 HTTPS-proxy HTTPSRR IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL threadsafe TLS-SRP UnixSockets zstd";

    /// <summary>
    /// The fourth line on Windows: <see cref="FeaturesLine"/> with <c>SSPI</c> after <c>SSL</c>, as the
    /// Schannel reference build lists it, because NTLM, Negotiate and Kerberos answer through SSPI on
    /// Windows (ADR-0142) and the features must say so (ADR-0439, BL-1796), and without <c>ECH</c>, which
    /// the Schannel reference build does not list, though <c>--ech</c> still works (ADR-0448, BL-1822), and
    /// without <c>GSS-API</c>, which the Schannel reference build does not list because Negotiate and
    /// Kerberos answer through SSPI there (ADR-0449, BL-1823), and without <c>HTTP2</c>, which the Schannel
    /// reference build does not list, though <c>--http2</c> still works (ADR-0450, BL-1824), and without
    /// <c>HTTP3</c>, which the Schannel reference build does not list, though <c>--http3</c> still works
    /// (ADR-0451, BL-1825), and without <c>TLS-SRP</c>, which the Schannel reference build does not list,
    /// though the TLS-SRP options still work (ADR-0452, BL-1826).
    /// </summary>
    public const string WindowsFeaturesLine = "Features: alt-svc AsynchDNS brotli HSTS HTTPS-proxy HTTPSRR IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL SSPI threadsafe UnixSockets zstd";

    /// <summary>Returns the four lines for the platform described, without line terminators.</summary>
    /// <param name="isWindows">Whether the running system is Windows (<see cref="OperatingSystem.IsWindows"/>).</param>
    /// <param name="isMacOS">Whether the running system is macOS (<see cref="OperatingSystem.IsMacOS"/>); read only when <paramref name="isWindows"/> is <see langword="false"/>.</param>
    /// <returns>
    /// The version line, <see cref="ReleaseDateLine"/>, <see cref="ProtocolsLine"/>, and
    /// <see cref="WindowsFeaturesLine"/> on Windows or <see cref="FeaturesLine"/> anywhere else.
    /// The version line names the mingw triple and <c>Schannel</c> on Windows, the Apple triple and
    /// <c>SecureTransport</c> on macOS, and the GNU/Linux triple and <c>OpenSSL</c> anywhere else.
    /// </returns>
    public static IReadOnlyList<string> Lines(bool isWindows, bool isMacOS) =>
        [VersionLine(isWindows, isMacOS), ReleaseDateLine, ProtocolsLine, isWindows ? WindowsFeaturesLine : FeaturesLine];

    private static string VersionLine(bool isWindows, bool isMacOS) =>
        isWindows ? "curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel"
        : isMacOS ? "curl 8.21.0 (aarch64-apple-darwin25.0.0) libcurl/8.21.0 SecureTransport"
        : "curl 8.21.0 (x86_64-pc-linux-gnu) libcurl/8.21.0 OpenSSL";
}
