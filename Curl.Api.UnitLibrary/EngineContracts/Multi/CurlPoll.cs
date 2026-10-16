namespace Curl.Api.EngineContracts.Multi;

/// <summary>
/// What a multi socket callback asks the application to wait for on a socket (<c>CURL_POLL_*</c>),
/// with upstream curl 8.22.0's numbers from <c>include/curl/multi.h</c>.
/// </summary>
public enum CurlPoll
{
    /// <summary><c>CURL_POLL_NONE</c> (0): register the socket, waiting for nothing yet.</summary>
    None = 0,

    /// <summary><c>CURL_POLL_IN</c> (1): wait until the socket is readable.</summary>
    In = 1,

    /// <summary><c>CURL_POLL_OUT</c> (2): wait until the socket is writable.</summary>
    Out = 2,

    /// <summary><c>CURL_POLL_INOUT</c> (3): wait until the socket is readable or writable.</summary>
    InOut = 3,

    /// <summary><c>CURL_POLL_REMOVE</c> (4): stop waiting on the socket.</summary>
    Remove = 4,
}
