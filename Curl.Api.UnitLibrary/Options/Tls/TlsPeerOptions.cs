namespace Curl.Api.Options.Tls;

/// <summary>
/// The TLS options libcurl has twice, once for the origin and once (with a <c>PROXY_</c>
/// prefix) for an HTTPS proxy. <see cref="EasyTlsOptions.Origin"/> and
/// <see cref="EasyTlsOptions.Proxy"/> each carry one. Every default is libcurl 8.22.0's,
/// which is the same for the origin option and its proxy twin.
/// </summary>
public sealed record TlsPeerOptions
{
    /// <summary>
    /// <c>CURLOPT_SSLCERT</c> / <c>CURLOPT_PROXY_SSLCERT</c>: the client certificate's file
    /// name (or, in the Schannel build, its store path). Default <see langword="null"/>: no
    /// client certificate.
    /// </summary>
    public string? Certificate { get; init; }

    /// <summary>
    /// <c>CURLOPT_SSLCERT_BLOB</c> / <c>CURLOPT_PROXY_SSLCERT_BLOB</c>: the client
    /// certificate's bytes, used instead of <see cref="Certificate"/>. Default
    /// <see langword="null"/>.
    /// </summary>
    public ReadOnlyMemory<byte>? CertificateBlob { get; init; }

    /// <summary>
    /// <c>CURLOPT_SSLCERTTYPE</c> / <c>CURLOPT_PROXY_SSLCERTTYPE</c>: the client
    /// certificate's format (<c>PEM</c>, <c>DER</c>, <c>P12</c>). Default <c>"PEM"</c>.
    /// </summary>
    public string CertificateType { get; init; } = "PEM";

    /// <summary>
    /// <c>CURLOPT_SSLKEY</c> / <c>CURLOPT_PROXY_SSLKEY</c>: the private key's file name.
    /// Default <see langword="null"/>: the key is looked for in <see cref="Certificate"/>.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// <c>CURLOPT_SSLKEY_BLOB</c> / <c>CURLOPT_PROXY_SSLKEY_BLOB</c>: the private key's
    /// bytes, used instead of <see cref="Key"/>. Default <see langword="null"/>.
    /// </summary>
    public ReadOnlyMemory<byte>? KeyBlob { get; init; }

    /// <summary>
    /// <c>CURLOPT_SSLKEYTYPE</c> / <c>CURLOPT_PROXY_SSLKEYTYPE</c>: the private key's format
    /// (<c>PEM</c>, <c>DER</c>, <c>ENG</c>, <c>PROV</c>). Default <c>"PEM"</c>.
    /// </summary>
    public string KeyType { get; init; } = "PEM";

    /// <summary>
    /// <c>CURLOPT_KEYPASSWD</c> / <c>CURLOPT_PROXY_KEYPASSWD</c>: the pass phrase that
    /// unlocks the private key. Default <see langword="null"/>.
    /// </summary>
    public string? KeyPassword { get; init; }

    /// <summary>
    /// The low 16 bits of <c>CURLOPT_SSLVERSION</c> / <c>CURLOPT_PROXY_SSLVERSION</c>: the
    /// lowest version allowed. Default <see cref="SslVersion.Default"/>.
    /// </summary>
    public SslVersion SslVersion { get; init; } = SslVersion.Default;

    /// <summary>
    /// The high bits of <c>CURLOPT_SSLVERSION</c> / <c>CURLOPT_PROXY_SSLVERSION</c>: the
    /// highest version allowed. Default <see cref="Tls.SslVersionMaximum.None"/> (the option's
    /// default <c>long</c> is 0).
    /// </summary>
    public SslVersionMaximum SslVersionMaximum { get; init; } = SslVersionMaximum.None;

    /// <summary>
    /// <c>CURLOPT_SSL_VERIFYPEER</c> / <c>CURLOPT_PROXY_SSL_VERIFYPEER</c>: whether the
    /// peer's certificate chain must verify. Default <see langword="true"/> (1).
    /// </summary>
    public bool VerifyPeer { get; init; } = true;

    /// <summary>
    /// <c>CURLOPT_SSL_VERIFYHOST</c> / <c>CURLOPT_PROXY_SSL_VERIFYHOST</c>: whether the
    /// peer's certificate must name the host. Default <see langword="true"/> (libcurl's 2;
    /// libcurl treats 1 as 2, so the option is on or off).
    /// </summary>
    public bool VerifyHost { get; init; } = true;

    /// <summary>
    /// <c>CURLOPT_CAINFO</c> / <c>CURLOPT_PROXY_CAINFO</c>: the CA bundle file. Default
    /// <see langword="null"/>: the build's built-in default, which in the Schannel build is
    /// none, so Windows' certificate store is used.
    /// </summary>
    public string? CertificateAuthorityFile { get; init; }

    /// <summary>
    /// <c>CURLOPT_CAINFO_BLOB</c> / <c>CURLOPT_PROXY_CAINFO_BLOB</c>: the CA bundle's PEM
    /// bytes. Default <see langword="null"/>.
    /// </summary>
    public ReadOnlyMemory<byte>? CertificateAuthorityBlob { get; init; }

    /// <summary>
    /// <c>CURLOPT_CAPATH</c> / <c>CURLOPT_PROXY_CAPATH</c>: a directory of CA certificates.
    /// Default <see langword="null"/>: the build's built-in default. The Schannel build does
    /// not support it.
    /// </summary>
    public string? CertificateAuthorityPath { get; init; }

    /// <summary>
    /// <c>CURLOPT_CRLFILE</c> / <c>CURLOPT_PROXY_CRLFILE</c>: a file of certificate
    /// revocation lists. Default <see langword="null"/>.
    /// </summary>
    public string? RevocationListFile { get; init; }

    /// <summary>
    /// <c>CURLOPT_ISSUERCERT</c> / <c>CURLOPT_PROXY_ISSUERCERT</c>: the file of the
    /// certificate that must have issued the peer's. Default <see langword="null"/>. The
    /// Schannel build does not support it.
    /// </summary>
    public string? IssuerCertificate { get; init; }

    /// <summary>
    /// <c>CURLOPT_ISSUERCERT_BLOB</c> / <c>CURLOPT_PROXY_ISSUERCERT_BLOB</c>: the issuer
    /// certificate's bytes. Default <see langword="null"/>. The Schannel build does not
    /// support it.
    /// </summary>
    public ReadOnlyMemory<byte>? IssuerCertificateBlob { get; init; }

    /// <summary>
    /// <c>CURLOPT_PINNEDPUBLICKEY</c> / <c>CURLOPT_PROXY_PINNEDPUBLICKEY</c>: the public key
    /// file, or <c>sha256//</c> hashes, the peer's key must match. Default
    /// <see langword="null"/>.
    /// </summary>
    public string? PinnedPublicKey { get; init; }

    /// <summary>
    /// <c>CURLOPT_SSL_CIPHER_LIST</c> / <c>CURLOPT_PROXY_SSL_CIPHER_LIST</c>: the TLS 1.2
    /// and older cipher list. Default <see langword="null"/>: the TLS library's own list.
    /// </summary>
    public string? CipherList { get; init; }

    /// <summary>
    /// <c>CURLOPT_TLS13_CIPHERS</c> / <c>CURLOPT_PROXY_TLS13_CIPHERS</c>: the TLS 1.3 cipher
    /// suites. Default <see langword="null"/>: the TLS library's own list.
    /// </summary>
    public string? Tls13Ciphers { get; init; }

    /// <summary>
    /// <c>CURLOPT_SSL_OPTIONS</c> / <c>CURLOPT_PROXY_SSL_OPTIONS</c>: the
    /// <c>CURLSSLOPT_*</c> bits. Default <see cref="SslOptions.None"/> (0).
    /// </summary>
    public SslOptions SslOptions { get; init; } = SslOptions.None;
}
