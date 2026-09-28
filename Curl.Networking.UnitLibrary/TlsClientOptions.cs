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
/// OpenSSL build as <paramref name="CertificateType" /> says, as ADR-0009 decides; the
/// certificate is presented when the server asks for one. <see langword="null" /> presents
/// none.
/// </param>
/// <param name="PrivateKey">
/// curl's <c>--key</c>: the file holding the private key for
/// <paramref name="ClientCertificate" />, in the format <paramref name="PrivateKeyType" />
/// names. Used by the OpenSSL build only, for a PEM or DER certificate, which reads the key
/// from the certificate file when this is <see langword="null" />; the Schannel build, and
/// the OpenSSL build for a PKCS#12 file, take the key from the PKCS#12 file and ignore it.
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
/// <param name="CertificateType">
/// curl's <c>--cert-type</c> value, verbatim: <c>PEM</c>, <c>DER</c> or <c>P12</c>, in any
/// case. The Schannel build accepts only <c>P12</c> and refuses any other with exit 58; the
/// OpenSSL build loads each of the three, as ADR-0009 decides. <see langword="null" /> is
/// PKCS#12 in the Schannel build and PEM in the OpenSSL build.
/// </param>
/// <param name="PrivateKeyType">
/// curl's <c>--key-type</c> value, verbatim: <c>PEM</c> or <c>DER</c>, in any case. The
/// OpenSSL build reads <paramref name="PrivateKey" /> as it says; the Schannel build ignores
/// it. <see langword="null" /> is PEM.
/// </param>
/// <param name="Passphrase">
/// curl's <c>--pass</c>: the passphrase for the private key, used in place of any passphrase
/// in <paramref name="ClientCertificate" />, since curl keeps whichever of the two came last
/// and the caller passes this only when <c>--pass</c> did. It opens an encrypted PEM key in
/// the OpenSSL build and a protected PKCS#12 file in either. <see langword="null" /> uses the
/// passphrase in <paramref name="ClientCertificate" />, if any.
/// </param>
/// <param name="SkipRevocationCheck">
/// <see langword="true" /> for curl's <c>--ssl-no-revoke</c>: the Schannel build does not
/// check whether a certificate in a <paramref name="CaCertificateFile" /> chain is revoked.
/// Without it the Schannel build checks, and a chain whose revocation status is unknown,
/// such as one from a private CA with no revocation endpoint, fails with exit 60, as
/// ADR-0086 decides. The OpenSSL build never checks, so ignores it.
/// </param>
/// <param name="RevocationCheckBestEffort">
/// <see langword="true" /> for curl's <c>--ssl-revoke-best-effort</c>: the Schannel build
/// still checks revocation, but accepts a chain whose only faults are an unknown or offline
/// revocation status (<see cref="System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.RevocationStatusUnknown" />,
/// <see cref="System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.OfflineRevocation" />),
/// as curl 8.21.0 does (measured, BL-490). The OpenSSL build never checks, so ignores it.
/// </param>
/// <param name="UseAlpn">
/// <see langword="true" />, curl's default, to offer in the handshake's ALPN extension the
/// application protocols the connection asks for; <see langword="false" /> for curl's
/// <c>--no-alpn</c>, which sends no ALPN extension, so curl's <c>-v</c> prints no
/// <c>ALPN:</c> line (measured, BL-490).
/// </param>
public sealed record TlsClientOptions(
    bool Insecure = false,
    TlsMinimumVersion MinimumVersion = TlsMinimumVersion.SystemDefault,
    string? CaCertificateFile = null,
    string? CaCertificateDirectory = null,
    string? ClientCertificate = null,
    string? PrivateKey = null,
    string? Ciphers = null,
    string? Tls13Ciphers = null,
    string? CertificateType = null,
    string? PrivateKeyType = null,
    string? Passphrase = null,
    bool SkipRevocationCheck = false,
    bool RevocationCheckBestEffort = false,
    bool UseAlpn = true);
