namespace Libcurl.Misc.Version;

/// <summary>
/// <c>curl_version_info_data</c>: what <see cref="CurlVersion.Info"/> answers, every field of curl
/// 8.22.0's newest age (<see cref="CurlVersionAge.Now"/>) whatever age was asked for, as libcurl
/// answers. A library or version the engine does not report is <see langword="null"/> or 0, as
/// libcurl reports one it was built without (ADR-0591).
/// </summary>
public sealed record CurlVersionInfoData
{
    /// <summary><c>age</c>: the age of this data, always <see cref="CurlVersionAge.Now"/>.</summary>
    public CurlVersionAge Age { get; init; } = CurlVersionAge.Now;

    /// <summary><c>version</c>: the libcurl version text, such as <c>8.22.0</c>.</summary>
    public required string Version { get; init; }

    /// <summary><c>version_num</c>: the version as <c>0xXXYYZZ</c>.</summary>
    public required int VersionNumber { get; init; }

    /// <summary><c>host</c>: the platform the engine was built for.</summary>
    public required string Host { get; init; }

    /// <summary><c>features</c>: the <c>CURL_VERSION_*</c> bits of <see cref="FeatureNames"/>.</summary>
    public required CurlVersionFeatures Features { get; init; }

    /// <summary><c>ssl_version</c>: the TLS backend, or <see langword="null"/> without TLS.</summary>
    public string? SslVersion { get; init; }

    /// <summary><c>ssl_version_num</c>: not used any more, always 0.</summary>
    public long SslVersionNumber { get; init; }

    /// <summary><c>libz_version</c>: the zlib version, <see langword="null"/> since the engine reports none.</summary>
    public string? LibzVersion { get; init; }

    /// <summary><c>protocols</c>: the lower-case schemes supported.</summary>
    public required IReadOnlyList<string> Protocols { get; init; }

    /// <summary><c>ares</c> (age 2): the c-ares version, <see langword="null"/> since the engine reports none.</summary>
    public string? Ares { get; init; }

    /// <summary><c>ares_num</c> (age 2): the c-ares version number, 0.</summary>
    public int AresNumber { get; init; }

    /// <summary><c>libidn</c> (age 3): the libidn version, <see langword="null"/>.</summary>
    public string? Libidn { get; init; }

    /// <summary><c>iconv_ver_num</c> (age 4): the iconv version number, 0.</summary>
    public int IconvVersionNumber { get; init; }

    /// <summary><c>libssh_version</c> (age 4): the SSH library version, <see langword="null"/>.</summary>
    public string? LibsshVersion { get; init; }

    /// <summary><c>brotli_ver_num</c> (age 5): the brotli version number, 0.</summary>
    public int BrotliVersionNumber { get; init; }

    /// <summary><c>brotli_version</c> (age 5): the brotli version, <see langword="null"/>.</summary>
    public string? BrotliVersion { get; init; }

    /// <summary><c>nghttp2_ver_num</c> (age 6): the nghttp2 version number, 0.</summary>
    public int Nghttp2VersionNumber { get; init; }

    /// <summary><c>nghttp2_version</c> (age 6): the nghttp2 version, <see langword="null"/>.</summary>
    public string? Nghttp2Version { get; init; }

    /// <summary><c>quic_version</c> (age 6): the QUIC library version, <see langword="null"/>.</summary>
    public string? QuicVersion { get; init; }

    /// <summary><c>cainfo</c> (age 7): the built-in default <c>CURLOPT_CAINFO</c>, <see langword="null"/>.</summary>
    public string? CaInfo { get; init; }

    /// <summary><c>capath</c> (age 7): the built-in default <c>CURLOPT_CAPATH</c>, <see langword="null"/>.</summary>
    public string? CaPath { get; init; }

    /// <summary><c>zstd_ver_num</c> (age 8): the zstd version number, 0.</summary>
    public int ZstdVersionNumber { get; init; }

    /// <summary><c>zstd_version</c> (age 8): the zstd version, <see langword="null"/>.</summary>
    public string? ZstdVersion { get; init; }

    /// <summary><c>hyper_version</c> (age 9): the hyper version, <see langword="null"/>.</summary>
    public string? HyperVersion { get; init; }

    /// <summary><c>gsasl_version</c> (age 10): the libgsasl version, <see langword="null"/>.</summary>
    public string? GsaslVersion { get; init; }

    /// <summary><c>feature_names</c> (age 11): the names of the features built in, as the engine lists them.</summary>
    public required IReadOnlyList<string> FeatureNames { get; init; }

    /// <summary><c>rtmp_version</c> (age 12): the RTMP library version, <see langword="null"/>.</summary>
    public string? RtmpVersion { get; init; }
}
