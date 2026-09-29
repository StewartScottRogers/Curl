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
    /// <c>imap</c>/<c>imaps</c>, <c>mqtt</c>/<c>mqtts</c>, <c>pop3</c>/<c>pop3s</c>, <c>smtp</c>/<c>smtps</c>, <c>ws</c>/<c>wss</c> and <c>http</c>/<c>https</c>.
    /// </summary>
    public const string ProtocolsLine = "Protocols: dict file ftp ftps gopher gophers http https imap imaps mqtt mqtts pop3 pop3s smtp smtps telnet tftp ws wss";

    /// <summary>
    /// The fourth line: the curl features the code gives evidence for, in curl's order (alphabetical,
    /// ignoring case). ADR-0021's four, plus <c>brotli</c> and <c>libz</c> now that the registered
    /// HTTP handler decodes <c>br</c>, <c>gzip</c> and <c>deflate</c> bodies, and <c>HTTP2</c> on every
    /// platform now that <c>--http2</c> is accepted (ADR-0141, Decision 5), and <c>GSS-API</c>,
    /// <c>Kerberos</c> and <c>SPNEGO</c> on every platform now that <c>--negotiate</c> is answered
    /// (ADR-0142, ADR-0173).
    /// </summary>
    public const string FeaturesLine = "Features: AsynchDNS brotli GSS-API HTTP2 IPv6 Kerberos Largefile libz SPNEGO SSL";

    /// <summary>Returns the four lines for the platform described, without line terminators.</summary>
    /// <param name="isWindows">Whether the running system is Windows (<see cref="OperatingSystem.IsWindows"/>).</param>
    /// <param name="isMacOS">Whether the running system is macOS (<see cref="OperatingSystem.IsMacOS"/>); read only when <paramref name="isWindows"/> is <see langword="false"/>.</param>
    /// <returns>
    /// The version line, <see cref="ReleaseDateLine"/>, <see cref="ProtocolsLine"/> and <see cref="FeaturesLine"/>.
    /// The version line names the mingw triple and <c>Schannel</c> on Windows, the Apple triple and
    /// <c>SecureTransport</c> on macOS, and the GNU/Linux triple and <c>OpenSSL</c> anywhere else.
    /// </returns>
    public static IReadOnlyList<string> Lines(bool isWindows, bool isMacOS) =>
        [VersionLine(isWindows, isMacOS), ReleaseDateLine, ProtocolsLine, FeaturesLine];

    private static string VersionLine(bool isWindows, bool isMacOS) =>
        isWindows ? "curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel"
        : isMacOS ? "curl 8.21.0 (aarch64-apple-darwin25.0.0) libcurl/8.21.0 SecureTransport"
        : "curl 8.21.0 (x86_64-pc-linux-gnu) libcurl/8.21.0 OpenSSL";
}
