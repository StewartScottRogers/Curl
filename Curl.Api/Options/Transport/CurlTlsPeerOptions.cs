using Curl.Api.Options.Tls;

namespace Curl.Api;

/// <summary>
/// The TLS settings for one peer, the origin server or an HTTPS proxy, mapped one to one onto
/// <see cref="TlsPeerOptions"/> (BL-2502, ADR-0636). Each member starts at libcurl 8.22.0's default.
/// An invalid value throws as it is set.
/// </summary>
public sealed record CurlTlsPeerOptions
{
    /// <summary><c>CURLOPT_SSLCERT</c>: the client certificate's file or name. Default <see langword="null"/>.</summary>
    public string? Certificate { get; init; }

    /// <summary><c>CURLOPT_SSLCERT_BLOB</c>: the client certificate's bytes. Default <see langword="null"/>.</summary>
    public ReadOnlyMemory<byte>? CertificateBlob { get; init; }

    /// <summary><c>CURLOPT_SSLCERTTYPE</c>: the client certificate's format (<c>PEM</c>, <c>DER</c>, <c>P12</c>). Default <see langword="null"/>: PEM.</summary>
    public string? CertificateType { get; init; }

    /// <summary><c>CURLOPT_SSLKEY</c>: the private key's file. Default <see langword="null"/>.</summary>
    public string? Key { get; init; }

    /// <summary><c>CURLOPT_SSLKEY_BLOB</c>: the private key's bytes. Default <see langword="null"/>.</summary>
    public ReadOnlyMemory<byte>? KeyBlob { get; init; }

    /// <summary><c>CURLOPT_SSLKEYTYPE</c>: the private key's format. Default <see langword="null"/>: PEM.</summary>
    public string? KeyType { get; init; }

    /// <summary><c>CURLOPT_KEYPASSWD</c>: the private key's pass phrase. Default <see langword="null"/>.</summary>
    public string? KeyPassword { get; init; }

    /// <summary><c>CURLOPT_SSLVERSION</c>'s minimum: the lowest TLS version allowed. Default <see cref="SslVersion.Default"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a TLS version libcurl defines.</exception>
    public SslVersion SslVersion
    {
        get;
        init => field = TransportOptionGuard.Defined(value, nameof(SslVersion));
    } = SslVersion.Default;

    /// <summary><c>CURLOPT_SSLVERSION</c>'s maximum: the highest TLS version allowed. Default <see cref="SslVersionMaximum.None"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a TLS version maximum libcurl defines.</exception>
    public SslVersionMaximum SslVersionMaximum
    {
        get;
        init => field = TransportOptionGuard.Defined(value, nameof(SslVersionMaximum));
    } = SslVersionMaximum.None;

    /// <summary><c>CURLOPT_SSL_VERIFYPEER</c>: verify the peer's certificate chain. Default <see langword="true"/>.</summary>
    public bool VerifyPeer { get; init; } = true;

    /// <summary><c>CURLOPT_SSL_VERIFYHOST</c>: verify the certificate names the host. Default <see langword="true"/>.</summary>
    public bool VerifyHost { get; init; } = true;

    /// <summary><c>CURLOPT_CAINFO</c>: the certificate authority bundle's file. Default <see langword="null"/>: the build's default.</summary>
    public string? CertificateAuthorityFile { get; init; }

    /// <summary><c>CURLOPT_CAINFO_BLOB</c>: the certificate authority bundle's bytes. Default <see langword="null"/>.</summary>
    public ReadOnlyMemory<byte>? CertificateAuthorityBlob { get; init; }

    /// <summary><c>CURLOPT_CAPATH</c>: a directory of certificate authority certificates. Default <see langword="null"/>.</summary>
    public string? CertificateAuthorityPath { get; init; }

    /// <summary><c>CURLOPT_CRLFILE</c>: the certificate revocation list's file. Default <see langword="null"/>.</summary>
    public string? RevocationListFile { get; init; }

    /// <summary><c>CURLOPT_ISSUERCERT</c>: the issuer certificate the peer's must be signed by. Default <see langword="null"/>.</summary>
    public string? IssuerCertificate { get; init; }

    /// <summary><c>CURLOPT_ISSUERCERT_BLOB</c>: the issuer certificate's bytes. Default <see langword="null"/>.</summary>
    public ReadOnlyMemory<byte>? IssuerCertificateBlob { get; init; }

    /// <summary><c>CURLOPT_PINNEDPUBLICKEY</c>: the public key file or <c>sha256//</c> hashes the peer must present. Default <see langword="null"/>.</summary>
    public string? PinnedPublicKey { get; init; }

    /// <summary><c>CURLOPT_SSL_CIPHER_LIST</c>: the TLS 1.2 and earlier cipher list. Default <see langword="null"/>: the build's.</summary>
    public string? CipherList { get; init; }

    /// <summary><c>CURLOPT_TLS13_CIPHERS</c>: the TLS 1.3 cipher suites. Default <see langword="null"/>: the build's.</summary>
    public string? Tls13Ciphers { get; init; }

    /// <summary><c>CURLOPT_SSL_OPTIONS</c>: the TLS behaviour bits. Default <see cref="SslOptions.None"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value sets a bit libcurl does not define.</exception>
    public SslOptions SslOptions
    {
        get;
        init => field = TransportOptionGuard.Flags(value, nameof(SslOptions));
    } = SslOptions.None;

    /// <summary>A <see cref="TlsPeerOptions"/> holding every member of this record.</summary>
    /// <returns>The seam's options for this peer.</returns>
    internal TlsPeerOptions ToEasy() => new()
    {
        Certificate = Certificate,
        CertificateBlob = CertificateBlob,
        CertificateType = CertificateType,
        Key = Key,
        KeyBlob = KeyBlob,
        KeyType = KeyType,
        KeyPassword = KeyPassword,
        SslVersion = SslVersion,
        SslVersionMaximum = SslVersionMaximum,
        VerifyPeer = VerifyPeer,
        VerifyHost = VerifyHost,
        CertificateAuthorityFile = CertificateAuthorityFile,
        CertificateAuthorityBlob = CertificateAuthorityBlob,
        CertificateAuthorityPath = CertificateAuthorityPath,
        RevocationListFile = RevocationListFile,
        IssuerCertificate = IssuerCertificate,
        IssuerCertificateBlob = IssuerCertificateBlob,
        PinnedPublicKey = PinnedPublicKey,
        CipherList = CipherList,
        Tls13Ciphers = Tls13Ciphers,
        SslOptions = SslOptions,
    };
}
