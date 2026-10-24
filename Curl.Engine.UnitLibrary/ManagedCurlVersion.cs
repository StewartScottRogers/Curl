using Curl.Api.EngineContracts;

namespace Curl.Engine;

/// <summary>
/// The <see cref="CurlVersionInfo" /> <see cref="ManagedCurlEngine" /> reports: the build <c>curl -V</c>
/// (<c>Curl.Cli</c>'s <c>CurlVersionText</c>) describes on the platform, so <c>curl_version</c> and
/// <c>curl_version_info</c> agree with it (BL-2465). The values are pinned here, since this library does
/// not reference <c>Curl.Cli.UnitLibrary</c>; <c>ManagedCurlVersionTests</c> checks them against
/// <c>CurlVersionText.Lines</c> for each platform.
/// </summary>
internal static class ManagedCurlVersion
{
    // -V's Protocols: line without ipfs and ipns, which the curl tool adds itself (src/tool_libinfo.c).
    private static readonly string[] Protocols =
    [
        "dict", "file", "ftp", "ftps", "gopher", "gophers", "http", "https", "imap", "imaps", "ldap", "ldaps",
        "mqtt", "mqtts", "pop3", "pop3s", "rtsp", "scp", "sftp", "smtp", "smtps", "telnet", "tftp", "ws", "wss",
    ];

    // The Schannel reference build's Features: line.
    private static readonly string[] WindowsFeatures =
    [
        "alt-svc", "AsynchDNS", "brotli", "HSTS", "HTTPS-proxy", "IDN", "IPv6", "Kerberos", "Largefile",
        "libz", "NTLM", "PSL", "SPNEGO", "SSL", "SSPI", "threadsafe", "UnixSockets", "zstd",
    ];

    // The OpenSSL reference build's Features: line, which Curl's -V shows on Linux and macOS alike.
    private static readonly string[] Features =
    [
        "alt-svc", "AsynchDNS", "brotli", "GSS-API", "HSTS", "HTTP2", "HTTPS-proxy", "HTTPSRR", "IDN", "IPv6",
        "Kerberos", "Largefile", "libz", "NTLM", "PSL", "SPNEGO", "SSL", "SSLS-EXPORT", "threadsafe",
        "UnixSockets", "zstd",
    ];

    /// <summary>Returns a new <see cref="CurlVersionInfo" /> for the platform described.</summary>
    /// <param name="isWindows">Whether the running system is Windows.</param>
    /// <param name="isMacOS">Whether the running system is macOS; read only when <paramref name="isWindows" /> is <see langword="false" />.</param>
    /// <returns>
    /// The mingw host and <c>Schannel</c> with the Windows features on Windows, the Apple host and
    /// <c>SecureTransport</c> on macOS, and the GNU/Linux host and <c>OpenSSL</c> anywhere else.
    /// </returns>
    internal static CurlVersionInfo For(bool isWindows, bool isMacOS) =>
        isWindows ? Of("x86_64-w64-mingw32", "Schannel", WindowsFeatures)
        : isMacOS ? Of("aarch64-apple-darwin25.0.0", "SecureTransport", Features)
        : Of("x86_64-pc-linux-gnu", "OpenSSL", Features);

    private static CurlVersionInfo Of(string host, string tlsBackend, string[] features) => new()
    {
        Host = host,
        TlsBackend = tlsBackend,
        Protocols = Protocols,
        Features = features,
    };
}
