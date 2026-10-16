namespace Curl.Api.Options.Tls;

/// <summary>
/// The highest TLS version a handshake may settle on: the high bits of
/// <c>CURLOPT_SSLVERSION</c> and <c>CURLOPT_PROXY_SSLVERSION</c>, the
/// <c>CURL_SSLVERSION_MAX_*</c> values. Each member's value is libcurl's, already shifted
/// left 16 bits, so OR-ing it with a <see cref="SslVersion"/> gives the option's
/// <c>long</c>.
/// </summary>
public enum SslVersionMaximum
{
    /// <summary><c>CURL_SSLVERSION_MAX_NONE</c> (0): no maximum given; libcurl's default.</summary>
    None = 0,

    /// <summary><c>CURL_SSLVERSION_MAX_DEFAULT</c> (1 &lt;&lt; 16): the TLS library's own maximum.</summary>
    Default = 1 << 16,

    /// <summary><c>CURL_SSLVERSION_MAX_TLSv1_0</c> (4 &lt;&lt; 16): at most TLS 1.0.</summary>
    Tls10 = 4 << 16,

    /// <summary><c>CURL_SSLVERSION_MAX_TLSv1_1</c> (5 &lt;&lt; 16): at most TLS 1.1.</summary>
    Tls11 = 5 << 16,

    /// <summary><c>CURL_SSLVERSION_MAX_TLSv1_2</c> (6 &lt;&lt; 16): at most TLS 1.2.</summary>
    Tls12 = 6 << 16,

    /// <summary><c>CURL_SSLVERSION_MAX_TLSv1_3</c> (7 &lt;&lt; 16): at most TLS 1.3.</summary>
    Tls13 = 7 << 16,
}
