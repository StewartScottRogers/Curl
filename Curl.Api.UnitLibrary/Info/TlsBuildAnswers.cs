namespace Curl.Api.Info;

/// <summary>
/// The build-constant TLS answers <c>curl_easy_getinfo</c> gives on a platform (ADR-0549): the
/// <c>curl_sslbackend</c> value for <c>CURLINFO_TLS_SSL_PTR</c> and the default CA bundle and
/// directory for <c>CURLINFO_CAINFO</c> and <c>CURLINFO_CAPATH</c>. Curl matches the Schannel build
/// on Windows and the OpenSSL build elsewhere, configured as curl 8.22.0's <c>configure</c> configures
/// itself by default on that platform.
/// </summary>
/// <param name="SslBackend">The <c>curl_sslbackend</c> value: 8 for Schannel, 1 for OpenSSL.</param>
/// <param name="CaInfo">The default CA bundle file, or <see langword="null" /> when the build has none.</param>
/// <param name="CaPath">The default CA certificate directory, or <see langword="null" /> when the build has none.</param>
internal sealed record TlsBuildAnswers(long SslBackend, string? CaInfo, string? CaPath)
{
    /// <summary><c>CURLSSLBACKEND_SCHANNEL</c>.</summary>
    internal const long SchannelBackend = 8;

    /// <summary><c>CURLSSLBACKEND_OPENSSL</c>.</summary>
    internal const long OpenSslBackend = 1;

    /// <summary>The Schannel build's answers: no CA bundle or directory, since it trusts the Windows store.</summary>
    internal static TlsBuildAnswers Schannel { get; } = new(SchannelBackend, null, null);

    /// <summary>
    /// The OpenSSL build's answers on Linux: <c>configure</c> takes the first bundle it finds, Debian's
    /// and Ubuntu's <c>/etc/ssl/certs/ca-certificates.crt</c>, and <c>/etc/ssl/certs</c> as the
    /// directory, since that directory holds OpenSSL's hashed <c>.0</c> links.
    /// </summary>
    internal static TlsBuildAnswers OpenSslLinux { get; } = new(OpenSslBackend, "/etc/ssl/certs/ca-certificates.crt", "/etc/ssl/certs");

    /// <summary>
    /// The OpenSSL build's answers on macOS: <c>configure</c> finds only <c>/etc/ssl/cert.pem</c>, and
    /// no directory, since macOS's <c>/etc/ssl/certs</c> holds no hashed <c>.0</c> links.
    /// </summary>
    internal static TlsBuildAnswers OpenSslMacOS { get; } = new(OpenSslBackend, "/etc/ssl/cert.pem", null);

    /// <summary>The answers of the platform this process runs on.</summary>
    internal static TlsBuildAnswers Current { get; } = For(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS());

    /// <summary>The answers of a platform.</summary>
    /// <param name="windows">Whether the platform is Windows, where Curl is the Schannel build.</param>
    /// <param name="macOS">Whether the platform is macOS.</param>
    /// <returns><see cref="Schannel" /> on Windows, <see cref="OpenSslMacOS" /> on macOS, else <see cref="OpenSslLinux" />.</returns>
    internal static TlsBuildAnswers For(bool windows, bool macOS) =>
        windows ? Schannel : macOS ? OpenSslMacOS : OpenSslLinux;
}
