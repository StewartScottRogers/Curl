namespace Libcurl.Misc.Version;

/// <summary>
/// <c>CURLversion</c>: the age of <c>curl_version_info_data</c> a caller asks for, as curl 8.22.0's
/// <c>curl.h</c> numbers them. Each age names the release that added its fields.
/// </summary>
public enum CurlVersionAge
{
    /// <summary><c>CURLVERSION_FIRST</c> (7.10): version, host, features, TLS, libz and protocols.</summary>
    First = 0,

    /// <summary><c>CURLVERSION_SECOND</c> (7.11.1): adds <c>ares</c> and <c>ares_num</c>.</summary>
    Second = 1,

    /// <summary><c>CURLVERSION_THIRD</c> (7.12.0): adds <c>libidn</c>.</summary>
    Third = 2,

    /// <summary><c>CURLVERSION_FOURTH</c> (7.16.1): adds <c>iconv_ver_num</c> and <c>libssh_version</c>.</summary>
    Fourth = 3,

    /// <summary><c>CURLVERSION_FIFTH</c> (7.57.0): adds the brotli version.</summary>
    Fifth = 4,

    /// <summary><c>CURLVERSION_SIXTH</c> (7.66.0): adds the nghttp2 and QUIC versions.</summary>
    Sixth = 5,

    /// <summary><c>CURLVERSION_SEVENTH</c> (7.70.0): adds <c>cainfo</c> and <c>capath</c>.</summary>
    Seventh = 6,

    /// <summary><c>CURLVERSION_EIGHTH</c> (7.72.0): adds the zstd version.</summary>
    Eighth = 7,

    /// <summary><c>CURLVERSION_NINTH</c> (7.75.0): adds <c>hyper_version</c>.</summary>
    Ninth = 8,

    /// <summary><c>CURLVERSION_TENTH</c> (7.77.0): adds <c>gsasl_version</c>.</summary>
    Tenth = 9,

    /// <summary><c>CURLVERSION_ELEVENTH</c> (7.87.0): adds <c>feature_names</c>.</summary>
    Eleventh = 10,

    /// <summary><c>CURLVERSION_TWELFTH</c> (8.8.0): adds <c>rtmp_version</c>.</summary>
    Twelfth = 11,

    /// <summary><c>CURLVERSION_NOW</c>: the newest age, <see cref="Twelfth"/>.</summary>
    Now = Twelfth,
}
