namespace Curl.Networking;

/// <summary>
/// The command-line settings <see cref="SslStreamTlsProvider" /> applies to every
/// handshake it runs.
/// </summary>
/// <param name="Insecure">
/// <see langword="true" /> for curl's <c>-k</c>/<c>--insecure</c>: the server certificate
/// and host name are not verified.
/// </param>
/// <param name="MinimumVersion">
/// The lowest TLS version offered; <see cref="TlsMinimumVersion.SystemDefault" /> leaves
/// the choice to the operating system.
/// </param>
/// <param name="CaCertificateFile">
/// curl's <c>--cacert</c>: the path of a PEM file whose certificates are the only roots
/// the server's chain may lead to, in place of the system store; <see langword="null" />
/// verifies against the system store. Ignored when <paramref name="Insecure" /> is set.
/// </param>
/// <param name="CaCertificateDirectory">
/// curl's <c>--capath</c>: a directory of PEM certificate files. The OpenSSL build of curl
/// adds every certificate in it to the roots the server's chain may lead to; the Schannel
/// build ignores it with a warning (<see cref="SslStreamTlsProvider.Warnings" />), as
/// ADR-0009 decides. <see langword="null" /> when not given. Ignored when
/// <paramref name="Insecure" /> is set.
/// </param>
/// <param name="ClientCertificate">
/// curl's <c>-E</c>/<c>--cert</c> value, verbatim: a certificate file path, optionally
/// followed by <c>:</c> and its passphrase, split as curl splits it
/// (<see cref="ClientCertificateArgument" />). The Schannel build loads it as PKCS#12, the
/// OpenSSL build as PEM, as ADR-0009 decides; the certificate is presented when the server
/// asks for one. <see langword="null" /> presents none.
/// </param>
/// <param name="PrivateKey">
/// curl's <c>--key</c>: the PEM file holding the private key for
/// <paramref name="ClientCertificate" />. Used by the OpenSSL build only, which reads the key
/// from the certificate file when this is <see langword="null" />; the Schannel build takes
/// the key from the PKCS#12 file and ignores it.
/// </param>
/// <param name="Ciphers">
/// curl's <c>--ciphers</c> value, verbatim: the TLS 1.2-and-below suites to offer. The
/// Schannel build refuses any value with exit 59; the OpenSSL build offers the suites it
/// names, by IANA or OpenSSL name, as ADR-0011 decides (<see cref="OpenSslCipherSuites" />).
/// <see langword="null" /> leaves the choice to the platform.
/// </param>
/// <param name="Tls13Ciphers">
/// curl's <c>--tls13-ciphers</c> value, verbatim: the TLS 1.3 suites to offer. The Schannel
/// build ignores it; the OpenSSL build offers the suites it names, as ADR-0011 decides.
/// <see langword="null" /> leaves the choice to the platform.
/// </param>
public sealed record TlsClientOptions(
    bool Insecure = false,
    TlsMinimumVersion MinimumVersion = TlsMinimumVersion.SystemDefault,
    string? CaCertificateFile = null,
    string? CaCertificateDirectory = null,
    string? ClientCertificate = null,
    string? PrivateKey = null,
    string? Ciphers = null,
    string? Tls13Ciphers = null);
