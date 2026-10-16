namespace Curl.Api.Options.Protocols;

/// <summary>
/// The RTSP request a transfer sends: libcurl's <c>CURL_RTSPREQ_*</c> values for
/// <c>CURLOPT_RTSP_REQUEST</c>, with the same numbers.
/// </summary>
public enum RtspRequest
{
    /// <summary><c>CURL_RTSPREQ_NONE</c>: no request.</summary>
    None = 0,

    /// <summary><c>CURL_RTSPREQ_OPTIONS</c>: <c>OPTIONS</c> (libcurl's default).</summary>
    Options = 1,

    /// <summary><c>CURL_RTSPREQ_DESCRIBE</c>: <c>DESCRIBE</c>.</summary>
    Describe = 2,

    /// <summary><c>CURL_RTSPREQ_ANNOUNCE</c>: <c>ANNOUNCE</c>.</summary>
    Announce = 3,

    /// <summary><c>CURL_RTSPREQ_SETUP</c>: <c>SETUP</c>.</summary>
    Setup = 4,

    /// <summary><c>CURL_RTSPREQ_PLAY</c>: <c>PLAY</c>.</summary>
    Play = 5,

    /// <summary><c>CURL_RTSPREQ_PAUSE</c>: <c>PAUSE</c>.</summary>
    Pause = 6,

    /// <summary><c>CURL_RTSPREQ_TEARDOWN</c>: <c>TEARDOWN</c>.</summary>
    Teardown = 7,

    /// <summary><c>CURL_RTSPREQ_GET_PARAMETER</c>: <c>GET_PARAMETER</c>.</summary>
    GetParameter = 8,

    /// <summary><c>CURL_RTSPREQ_SET_PARAMETER</c>: <c>SET_PARAMETER</c>.</summary>
    SetParameter = 9,

    /// <summary><c>CURL_RTSPREQ_RECORD</c>: <c>RECORD</c>.</summary>
    Record = 10,

    /// <summary><c>CURL_RTSPREQ_RECEIVE</c>: send nothing and receive interleaved RTP data.</summary>
    Receive = 11,
}
