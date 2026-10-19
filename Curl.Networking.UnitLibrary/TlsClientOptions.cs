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
/// The lowest TLS version offered; <see cref="TlsVersion.SystemDefault" /> leaves the
/// choice to the operating system unless <paramref name="MaximumVersion" /> is below
/// TLS 1.3, when the range starts at TLS 1.0 (<see cref="TlsVersionRange" />).
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
/// The certificate file path (or Schannel store path) curl's <c>-E</c>/<c>--cert</c> names, already
/// split from its pass phrase as curl's tool splits it (<see cref="ClientCertificateArgument" />,
/// ADR-0514). The Schannel build loads it as PKCS#12, the
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
/// The pass phrase for the private key: curl's <c>--pass</c>, else the one split from
/// <c>--cert</c>'s value, as curl's tool keeps them (ADR-0514). It opens an encrypted PEM key in
/// the OpenSSL build and a protected PKCS#12 file in either. <see langword="null" /> for none.
/// </param>
/// <param name="SkipRevocationCheck">
/// <see langword="true" /> for curl's <c>--ssl-no-revoke</c>: the Schannel build does not
/// check whether a certificate in a <paramref name="CaCertificateFile" /> chain is revoked.
/// Without it the Schannel build checks, and a chain whose revocation status is unknown,
/// such as one from a private CA with no revocation endpoint, fails with exit 60, as
/// ADR-0321 decides. The OpenSSL build never checks, so ignores it.
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
/// <param name="MaximumVersion">
/// curl's <c>--tls-max</c>: the highest TLS version offered; <see cref="TlsVersion.SystemDefault" />
/// sets no ceiling. It must not be below <paramref name="MinimumVersion" />, a pair the
/// command-line parser refuses as curl does; <see cref="SslStreamTlsProvider" /> throws
/// <see cref="ArgumentException" /> for one (BL-502).
/// </param>
/// <param name="Curves">
/// curl's <c>--curves</c> value, verbatim: the key-exchange groups to offer, in OpenSSL 3.5's syntax
/// (<see cref="OpenSslGroupList" />). <see cref="TlsClientRouting" /> sends a connection with it to
/// <see cref="HandBuiltTlsProvider" /> on every platform (ADR-0151, ADR-0284).
/// <see langword="null" /> leaves the choice to the platform.
/// </param>
/// <param name="SignatureAlgorithms">
/// curl's <c>--sigalgs</c> value, verbatim: the signature algorithms to offer, in OpenSSL 3.5's syntax
/// (<see cref="OpenSslSignatureAlgorithmList" />). <see cref="TlsClientRouting" /> sends a connection
/// with it to <see cref="HandBuiltTlsProvider" /> on every platform (ADR-0151, ADR-0284).
/// <see langword="null" /> leaves the choice to the platform.
/// </param>
/// <param name="AllowEarlyData">
/// <see langword="true" /> for curl's <c>--tls-earlydata</c>: send TLS 1.3 early data on a resumed
/// session. ADR-0151 gives it to the hand-built client (BL-710); neither provider applies it yet.
/// </param>
/// <param name="Ech">
/// curl's <c>--ech</c> mode, verbatim (<c>false</c>, <c>grease</c>, <c>true</c> or <c>hard</c>);
/// <see langword="null" /> when not given. ADR-0151 gives every mode but <c>false</c> to the
/// hand-built client, which offers GREASE or ECH as <see cref="EchModes.Of" /> reads it (ADR-0327, BL-711).
/// </param>
/// <param name="EchPublicName">
/// The public name of curl's <c>--ech pn:&lt;name&gt;</c>, without the prefix; <see langword="null" />
/// when not given. Applied with <paramref name="Ech" /> (BL-711).
/// </param>
/// <param name="EchConfigList">
/// The base64 ECHConfigList of curl's <c>--ech ecl:&lt;list&gt;</c>, without the prefix;
/// <see langword="null" /> when not given. Applied with <paramref name="Ech" /> (BL-711).
/// </param>
/// <param name="SslSessionsFile">
/// curl's <c>--ssl-sessions</c> file, which session tickets are loaded from and saved to;
/// <see langword="null" /> when not given. It routes the connection to the hand-built client
/// (ADR-0151), which offers and keeps sessions in the run's <see cref="TlsSessionCache" />
/// (ADR-0319, BL-710).
/// </param>
/// <param name="Engine">
/// curl's <c>--engine</c> name, verbatim; <see langword="null" /> when not given. No provider loads
/// a crypto engine: as ADR-0151 decides, the console ignores it on Windows and fails the transfer
/// before it connects elsewhere, so a handshake never sees one.
/// </param>
/// <param name="RequireCertificateStatus">
/// <see langword="true" /> for curl's <c>--cert-status</c>: the handshake asks for the server's stapled
/// OCSP response and fails with exit 91 unless it vouches for the certificate. <c>SslStream</c>
/// exposes no stapled response, so <see cref="TlsClientRouting" /> sends such a connection to
/// <see cref="HandBuiltTlsProvider" /> on every platform (ADR-0191).
/// </param>
/// <param name="AutoClientCertificate">
/// <see langword="true" /> for curl's <c>--ssl-auto-client-cert</c> (<c>--proxy-ssl-auto-client-cert</c>
/// for an HTTPS proxy): without <paramref name="ClientCertificate" />, a certificate chosen from the
/// user's personal store is presented when the server asks for one, on every platform (ADR-0191,
/// <see cref="AutomaticClientCertificate" />).
/// </param>
/// <param name="PinnedPublicKey">
/// curl's <c>--pinnedpubkey</c>, verbatim: <c>sha256//</c> hashes separated by <c>;</c>, or the path of
/// a PEM or DER public key file; <see langword="null" /> when not given. After the handshake, under
/// <paramref name="Insecure" /> too, both providers fail with exit 90 unless the server certificate's
/// public key matches it (<see cref="Networking.PinnedPublicKey" />, ADR-0193).
/// </param>
/// <param name="CertificateRevocationListFile">
/// curl's <c>--crlfile</c>: a PEM file of certificate revocation lists; <see langword="null" /> when
/// not given. The OpenSSL build, unless <paramref name="Insecure" />, loads it before the
/// handshake (exit 82 when it cannot) and then needs, for every certificate of the verified
/// chain, a list from its issuer that does not revoke it (exit 60,
/// <see cref="Networking.CertificateRevocationListFile" />, ADR-0197). The Schannel build ignores it,
/// as curl 8.21.0's does (measured, BL-609).
/// </param>
/// <param name="NoSessionId">
/// <see langword="true" /> for curl's <c>--no-sessionid</c>: no TLS session is offered or kept, so every
/// connection of the run makes a full handshake. <c>SslStream</c> cannot stop the operating system's
/// process-wide session cache, so <see cref="TlsClientRouting" /> sends such a connection to
/// <see cref="HandBuiltTlsProvider" />, which then neither offers nor keeps a session (ADR-0151, BL-713).
/// </param>
/// <param name="AllowBeast">
/// <see langword="true" /> for curl's <c>--ssl-allow-beast</c> (<c>--proxy-ssl-allow-beast</c> for an HTTPS
/// proxy): a TLS 1.0 CBC write goes out whole, without the empty application data record OpenSSL sends
/// before it (ADR-0150). <see cref="TlsClientRouting" /> sends a connection with it whose range reaches
/// TLS 1.0 to <see cref="HandBuiltTlsProvider" />, since <c>SslStream</c> has no control for the split.
/// </param>
/// <param name="KeyLogFile">
/// The file the <c>SSLKEYLOGFILE</c> environment variable names, which the OpenSSL build appends the
/// connection's secrets to (<see cref="NssKeyLogFile" />, BL-2327); <see langword="null" /> when it is
/// not set, and always on Windows, where the Schannel build ignores it. <c>SslStream</c> cannot log its
/// secrets, so <see cref="TlsClientRouting" /> sends such a connection to <see cref="HandBuiltTlsProvider" />.
/// </param>
/// <param name="ChecksHostNameWhenInsecure">
/// <see langword="true" /> for libcurl's <c>CURLOPT_SSL_VERIFYPEER</c> 0 with <c>CURLOPT_SSL_VERIFYHOST</c>
/// still on: under <paramref name="Insecure" /> the chain is not checked, but a certificate that does not
/// name the host is still exit 60, as curl 8.22.0 checks the name apart from the peer (upstream test2118,
/// ADR-0703). <c>-k</c> turns both off, so the curl tool never sets it.
/// </param>
/// <param name="CaCertificateBlob">
/// libcurl's <c>CURLOPT_CAINFO_BLOB</c> (<c>CURLOPT_PROXY_CAINFO_BLOB</c> for an HTTPS proxy): PEM
/// certificates held in memory, trusted as a <paramref name="CaCertificateFile" /> is and in its place
/// when both are set, as libcurl's blob wins over the file (ADR-0704). <see langword="null" /> when not
/// set; the curl tool never sets it.
/// </param>
public sealed record TlsClientOptions(
    bool Insecure = false,
    TlsVersion MinimumVersion = TlsVersion.SystemDefault,
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
    bool UseAlpn = true,
    TlsVersion MaximumVersion = TlsVersion.SystemDefault,
    string? Curves = null,
    string? SignatureAlgorithms = null,
    bool AllowEarlyData = false,
    string? Ech = null,
    string? EchPublicName = null,
    string? EchConfigList = null,
    string? SslSessionsFile = null,
    string? Engine = null,
    bool RequireCertificateStatus = false,
    bool AutoClientCertificate = false,
    string? PinnedPublicKey = null,
    string? CertificateRevocationListFile = null,
    bool NoSessionId = false,
    bool AllowBeast = false,
    string? KeyLogFile = null,
    bool ChecksHostNameWhenInsecure = false,
    ReadOnlyMemory<byte>? CaCertificateBlob = null)
{
    /// <summary>
    /// Whether the chain must lead to roots of the handshake's own, a <see cref="CaCertificateFile" />
    /// or a <see cref="CaCertificateBlob" />, in place of the system store.
    /// </summary>
    internal bool HasCaCertificates => CaCertificateFile is not null || CaCertificateBlob is not null;
}
