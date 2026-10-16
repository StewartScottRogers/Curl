namespace Curl.Api.Options.Tls;

/// <summary>
/// The lowest TLS or SSL version a handshake may settle on: the low 16 bits of
/// <c>CURLOPT_SSLVERSION</c> and <c>CURLOPT_PROXY_SSLVERSION</c>, the
/// <c>CURL_SSLVERSION_*</c> values. Each member's value is libcurl's.
/// </summary>
public enum SslVersion
{
    /// <summary><c>CURL_SSLVERSION_DEFAULT</c> (0): the TLS library's own minimum; libcurl's default.</summary>
    Default = 0,

    /// <summary><c>CURL_SSLVERSION_TLSv1</c> (1): TLS 1.0 or later.</summary>
    Tls1 = 1,

    /// <summary><c>CURL_SSLVERSION_SSLv2</c> (2): SSL 2, which libcurl refuses.</summary>
    Ssl2 = 2,

    /// <summary><c>CURL_SSLVERSION_SSLv3</c> (3): SSL 3, which libcurl refuses.</summary>
    Ssl3 = 3,

    /// <summary><c>CURL_SSLVERSION_TLSv1_0</c> (4): TLS 1.0 or later.</summary>
    Tls10 = 4,

    /// <summary><c>CURL_SSLVERSION_TLSv1_1</c> (5): TLS 1.1 or later.</summary>
    Tls11 = 5,

    /// <summary><c>CURL_SSLVERSION_TLSv1_2</c> (6): TLS 1.2 or later.</summary>
    Tls12 = 6,

    /// <summary><c>CURL_SSLVERSION_TLSv1_3</c> (7): TLS 1.3 or later.</summary>
    Tls13 = 7,
}
