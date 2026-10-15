namespace Curl.Api.Options.Tls;

/// <summary>
/// An easy handle's TLS options (ADR-0506, plan row <c>EasyTlsOptions</c>): the origin's,
/// their <c>PROXY_*</c> twins, the options libcurl has only for the origin, and the
/// <c>DOH_SSL_*</c> options for DNS-over-HTTPS requests. Every default is libcurl
/// 8.22.0's, not the Schannel build's behaviour; where the Schannel build does not support
/// an option the property's documentation says so (ADR-0009).
/// </summary>
public sealed record EasyTlsOptions
{
    /// <summary>
    /// The origin's TLS options: <c>CURLOPT_SSLCERT</c>, <c>CURLOPT_SSLVERSION</c>,
    /// <c>CURLOPT_CAINFO</c> and the rest of <see cref="TlsPeerOptions"/>. Default: every
    /// one at libcurl's default.
    /// </summary>
    public TlsPeerOptions Origin { get; init; } = new();

    /// <summary>
    /// An HTTPS proxy's TLS options: <c>CURLOPT_PROXY_SSLCERT</c>,
    /// <c>CURLOPT_PROXY_SSLVERSION</c>, <c>CURLOPT_PROXY_CAINFO</c> and the rest of
    /// <see cref="TlsPeerOptions"/>. Default: every one at libcurl's default.
    /// </summary>
    public TlsPeerOptions Proxy { get; init; } = new();

    /// <summary>
    /// <c>CURLOPT_SSLENGINE</c>: the crypto engine's identifier. Default
    /// <see langword="null"/>. The Schannel build does not support it.
    /// </summary>
    public string? Engine { get; init; }

    /// <summary>
    /// <c>CURLOPT_SSLENGINE_DEFAULT</c>: whether <see cref="Engine"/> becomes the default
    /// for all crypto operations. Default <see langword="false"/> (the option is never set).
    /// The Schannel build does not support it.
    /// </summary>
    public bool EngineIsDefault { get; init; }

    /// <summary>
    /// <c>CURLOPT_SSL_VERIFYSTATUS</c>: whether the origin must staple a good OCSP response.
    /// Default <see langword="false"/> (0). The Schannel build does not support it.
    /// </summary>
    public bool VerifyStatus { get; init; }

    /// <summary>
    /// <c>CURLOPT_CA_CACHE_TIMEOUT</c>: how long a loaded CA store is kept for reuse
    /// (libcurl takes seconds; -1 is <see cref="Timeout.InfiniteTimeSpan"/>). Default
    /// 86400 seconds (24 hours).
    /// </summary>
    public TimeSpan CertificateAuthorityCacheTimeout { get; init; } = TimeSpan.FromSeconds(86400);

    /// <summary>
    /// <c>CURLOPT_CERTINFO</c>: whether the peer's certificate chain is gathered for
    /// <c>CURLINFO_CERTINFO</c>. Default <see langword="false"/> (0).
    /// </summary>
    public bool GatherCertificateInfo { get; init; }

    /// <summary>
    /// <c>CURLOPT_SSL_EC_CURVES</c>: the key-exchange curves, colon-separated. Default
    /// <see langword="null"/>: the TLS library's own list. The Schannel build does not
    /// support it.
    /// </summary>
    public string? EllipticCurves { get; init; }

    /// <summary>
    /// <c>CURLOPT_SSL_SIGNATURE_ALGORITHMS</c>: the signature algorithms, colon-separated.
    /// Default <see langword="null"/>: the TLS library's own list. The Schannel build does
    /// not support it.
    /// </summary>
    public string? SignatureAlgorithms { get; init; }

    /// <summary>
    /// <c>CURLOPT_SSL_SESSIONID_CACHE</c>: whether TLS sessions are cached and resumed.
    /// Default <see langword="true"/> (1).
    /// </summary>
    public bool SessionIdCache { get; init; } = true;

    /// <summary>
    /// <c>CURLOPT_SSL_FALSESTART</c>: whether TLS False Start is used. Default
    /// <see langword="false"/> (0). The Schannel build does not support it.
    /// </summary>
    public bool FalseStart { get; init; }

    /// <summary>
    /// <c>CURLOPT_ECH</c>: the Encrypted Client Hello configuration (<c>false</c>,
    /// <c>grease</c>, <c>true</c>, <c>hard</c>, <c>ecl:</c>..., <c>pn:</c>...). Default
    /// <see langword="null"/>: ECH off. The Schannel build does not support it.
    /// </summary>
    public string? EncryptedClientHello { get; init; }

    /// <summary>
    /// <c>CURLOPT_DOH_SSL_VERIFYPEER</c>: whether a DNS-over-HTTPS server's certificate
    /// chain must verify. Default <see langword="true"/> (1).
    /// </summary>
    public bool DohVerifyPeer { get; init; } = true;

    /// <summary>
    /// <c>CURLOPT_DOH_SSL_VERIFYHOST</c>: whether a DNS-over-HTTPS server's certificate must
    /// name its host. Default <see langword="true"/> (libcurl's 2; 1 is treated as 2).
    /// </summary>
    public bool DohVerifyHost { get; init; } = true;

    /// <summary>
    /// <c>CURLOPT_DOH_SSL_VERIFYSTATUS</c>: whether a DNS-over-HTTPS server must staple a
    /// good OCSP response. Default <see langword="false"/> (0). The Schannel build does not
    /// support it.
    /// </summary>
    public bool DohVerifyStatus { get; init; }
}
