namespace Curl.Original.Protocol.Rtsp;

/// <summary>
/// The RTSP request a libcurl transfer makes, numbered as libcurl's <c>CURL_RTSPREQ_*</c> values
/// for <c>CURLOPT_RTSP_REQUEST</c> (BL-2751).
/// </summary>
public enum RtspRequestKind
{
    /// <summary><c>OPTIONS</c>, the curl tool's one request and libcurl's default.</summary>
    Options = 1,

    /// <summary><c>DESCRIBE</c>, sent with <c>Accept: application/sdp</c>.</summary>
    Describe = 2,

    /// <summary><c>ANNOUNCE</c>, sending the body as <c>application/sdp</c>.</summary>
    Announce = 3,

    /// <summary><c>SETUP</c>, which needs a <c>Transport</c>.</summary>
    Setup = 4,

    /// <summary><c>PLAY</c>.</summary>
    Play = 5,

    /// <summary><c>PAUSE</c>.</summary>
    Pause = 6,

    /// <summary><c>TEARDOWN</c>.</summary>
    Teardown = 7,

    /// <summary><c>GET_PARAMETER</c>, sending any body as <c>text/parameters</c>.</summary>
    GetParameter = 8,

    /// <summary><c>SET_PARAMETER</c>, sending any body as <c>text/parameters</c>.</summary>
    SetParameter = 9,

    /// <summary><c>RECORD</c>.</summary>
    Record = 10,

    /// <summary>Sends nothing and takes interleaved RTP frames until the server closes.</summary>
    Receive = 11,
}
