namespace Libcurl.Global;

/// <summary><c>curl_sslbackend</c>: the TLS backend ids, with upstream curl 8.22.0's numbers.</summary>
public enum CurlSslBackendId
{
    /// <summary><c>CURLSSLBACKEND_NONE</c> (0): no id; match by name instead.</summary>
    None = 0,

    /// <summary><c>CURLSSLBACKEND_OPENSSL</c> (1): OpenSSL, the build Curl matches on Linux and macOS.</summary>
    OpenSsl = 1,

    /// <summary><c>CURLSSLBACKEND_GNUTLS</c> (2).</summary>
    GnuTls = 2,

    /// <summary><c>CURLSSLBACKEND_NSS</c> (3), removed upstream.</summary>
    Nss = 3,

    /// <summary><c>CURLSSLBACKEND_GSKIT</c> (5), removed upstream.</summary>
    GsKit = 5,

    /// <summary><c>CURLSSLBACKEND_POLARSSL</c> (6), removed upstream.</summary>
    PolarSsl = 6,

    /// <summary><c>CURLSSLBACKEND_WOLFSSL</c> (7).</summary>
    WolfSsl = 7,

    /// <summary><c>CURLSSLBACKEND_SCHANNEL</c> (8): Schannel, the build Curl matches on Windows.</summary>
    Schannel = 8,

    /// <summary><c>CURLSSLBACKEND_SECURETRANSPORT</c> (9), removed upstream.</summary>
    SecureTransport = 9,

    /// <summary><c>CURLSSLBACKEND_AXTLS</c> (10), removed upstream.</summary>
    AxTls = 10,

    /// <summary><c>CURLSSLBACKEND_MBEDTLS</c> (11).</summary>
    MbedTls = 11,

    /// <summary><c>CURLSSLBACKEND_MESALINK</c> (12), removed upstream.</summary>
    MesaLink = 12,

    /// <summary><c>CURLSSLBACKEND_BEARSSL</c> (13), removed upstream.</summary>
    BearSsl = 13,

    /// <summary><c>CURLSSLBACKEND_RUSTLS</c> (14).</summary>
    Rustls = 14,
}
