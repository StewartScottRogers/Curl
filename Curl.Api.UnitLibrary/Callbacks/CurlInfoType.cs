namespace Curl.Api.Callbacks;

/// <summary>
/// The kind of data a <see cref="DebugCallback"/> is handed: libcurl's <c>curl_infotype</c>
/// (<c>CURLINFO_*</c>), with upstream curl 8.22.0's numbers. Upstream's <c>CURLINFO_END</c>
/// (7) only marks the end of the list and is never passed, so it has no member.
/// </summary>
public enum CurlInfoType
{
    /// <summary><c>CURLINFO_TEXT</c> (0): informational text.</summary>
    Text = 0,

    /// <summary><c>CURLINFO_HEADER_IN</c> (1): header data received from the peer.</summary>
    HeaderIn = 1,

    /// <summary><c>CURLINFO_HEADER_OUT</c> (2): header data sent to the peer.</summary>
    HeaderOut = 2,

    /// <summary><c>CURLINFO_DATA_IN</c> (3): protocol data received from the peer.</summary>
    DataIn = 3,

    /// <summary><c>CURLINFO_DATA_OUT</c> (4): protocol data sent to the peer.</summary>
    DataOut = 4,

    /// <summary><c>CURLINFO_SSL_DATA_IN</c> (5): TLS data received from the peer.</summary>
    SslDataIn = 5,

    /// <summary><c>CURLINFO_SSL_DATA_OUT</c> (6): TLS data sent to the peer.</summary>
    SslDataOut = 6,
}
