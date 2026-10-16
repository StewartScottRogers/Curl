using Curl.Api.Options.Tls;

namespace Curl.Api;

/// <summary>
/// TLS settings for the origin server and an HTTPS proxy, mapped one to one onto
/// <see cref="EasyTlsOptions"/> (BL-2502, ADR-0636). Each member starts at libcurl 8.22.0's
/// default, so <c>new CurlTlsOptions()</c> changes nothing. Set on <see cref="CurlClientOptions.Tls"/>
/// for every request or on <see cref="CurlRequest.Tls"/> for one; a request's record replaces the
/// client's whole. An invalid value throws as it is set.
/// </summary>
public sealed record CurlTlsOptions
{
    /// <summary>The TLS settings for the origin server (<c>CURLOPT_SSL*</c>, <c>CURLOPT_CAINFO</c> and the rest).</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public CurlTlsPeerOptions Origin
    {
        get;
        init => field = TransportOptionGuard.NotNull(value, nameof(Origin));
    } = new();

    /// <summary>The TLS settings for an HTTPS proxy (the <c>CURLOPT_PROXY_SSL*</c> twins).</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public CurlTlsPeerOptions Proxy
    {
        get;
        init => field = TransportOptionGuard.NotNull(value, nameof(Proxy));
    } = new();

    /// <summary><c>CURLOPT_SSLENGINE</c>: the crypto engine's identifier. Default <see langword="null"/>. The Schannel build does not support it.</summary>
    public string? Engine { get; init; }

    /// <summary><c>CURLOPT_SSLENGINE_DEFAULT</c>: make <see cref="Engine"/> the default for all crypto operations. Default <see langword="false"/>.</summary>
    public bool EngineIsDefault { get; init; }

    /// <summary><c>CURLOPT_SSL_VERIFYSTATUS</c>: require a good OCSP stapled response. Default <see langword="false"/>.</summary>
    public bool VerifyStatus { get; init; }

    /// <summary>
    /// <c>CURLOPT_CA_CACHE_TIMEOUT</c>: how long a loaded certificate authority store is reused. Default
    /// 24 hours; zero disables the cache and <see cref="Timeout.InfiniteTimeSpan"/> keeps it forever.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative and not <see cref="Timeout.InfiniteTimeSpan"/>.</exception>
    public TimeSpan CertificateAuthorityCacheTimeout
    {
        get;
        init => field = TransportOptionGuard.NonNegativeOrInfinite(value, nameof(CertificateAuthorityCacheTimeout));
    } = TimeSpan.FromSeconds(86400);

    /// <summary><c>CURLOPT_CERTINFO</c>: gather the peer's certificate chain into the transfer info. Default <see langword="false"/>.</summary>
    public bool GatherCertificateInfo { get; init; }

    /// <summary><c>CURLOPT_SSL_EC_CURVES</c>: the key exchange curves offered. Default <see langword="null"/>: the build's.</summary>
    public string? EllipticCurves { get; init; }

    /// <summary><c>CURLOPT_SSL_SIGNATURE_ALGORITHMS</c>: the signature algorithms offered. Default <see langword="null"/>: the build's.</summary>
    public string? SignatureAlgorithms { get; init; }

    /// <summary><c>CURLOPT_SSL_SESSIONID_CACHE</c>: reuse TLS session ids. Default <see langword="true"/>.</summary>
    public bool SessionIdCache { get; init; } = true;

    /// <summary><c>CURLOPT_SSL_FALSESTART</c>: use TLS False Start. Default <see langword="false"/>.</summary>
    public bool FalseStart { get; init; }

    /// <summary><c>CURLOPT_ECH</c>: the Encrypted Client Hello mode. Default <see langword="null"/>: off.</summary>
    public string? EncryptedClientHello { get; init; }

    /// <summary>The Encrypted Client Hello public name (curl's <c>--ech pn:</c>). Default <see langword="null"/>.</summary>
    public string? EncryptedClientHelloPublicName { get; init; }

    /// <summary>The Encrypted Client Hello configuration list (curl's <c>--ech ecl:</c>). Default <see langword="null"/>.</summary>
    public string? EncryptedClientHelloConfigList { get; init; }

    /// <summary><c>CURLOPT_DOH_SSL_VERIFYPEER</c>: verify the DNS-over-HTTPS server's certificate chain. Default <see langword="true"/>.</summary>
    public bool DohVerifyPeer { get; init; } = true;

    /// <summary><c>CURLOPT_DOH_SSL_VERIFYHOST</c>: verify the DNS-over-HTTPS server's certificate names it. Default <see langword="true"/>.</summary>
    public bool DohVerifyHost { get; init; } = true;

    /// <summary><c>CURLOPT_DOH_SSL_VERIFYSTATUS</c>: require a good OCSP response from the DNS-over-HTTPS server. Default <see langword="false"/>.</summary>
    public bool DohVerifyStatus { get; init; }

    /// <summary>An <see cref="EasyTlsOptions"/> holding every member of this record.</summary>
    /// <returns>The seam's TLS options.</returns>
    internal EasyTlsOptions ToEasy() => new()
    {
        Origin = Origin.ToEasy(),
        Proxy = Proxy.ToEasy(),
        Engine = Engine,
        EngineIsDefault = EngineIsDefault,
        VerifyStatus = VerifyStatus,
        CertificateAuthorityCacheTimeout = CertificateAuthorityCacheTimeout,
        GatherCertificateInfo = GatherCertificateInfo,
        EllipticCurves = EllipticCurves,
        SignatureAlgorithms = SignatureAlgorithms,
        SessionIdCache = SessionIdCache,
        FalseStart = FalseStart,
        EncryptedClientHello = EncryptedClientHello,
        EncryptedClientHelloPublicName = EncryptedClientHelloPublicName,
        EncryptedClientHelloConfigList = EncryptedClientHelloConfigList,
        DohVerifyPeer = DohVerifyPeer,
        DohVerifyHost = DohVerifyHost,
        DohVerifyStatus = DohVerifyStatus,
    };
}
