namespace Curl.Api.EngineContracts.Multi;

/// <summary>
/// The events an application reports on a socket to <c>curl_multi_socket_action</c>
/// (<c>CURL_CSELECT_*</c>), with upstream curl 8.22.0's numbers from <c>include/curl/multi.h</c>.
/// </summary>
[Flags]
public enum CurlSocketSelect
{
    /// <summary>0: no event given; libcurl works out the socket's state itself.</summary>
    None = 0,

    /// <summary><c>CURL_CSELECT_IN</c> (<c>0x01</c>): the socket is readable.</summary>
    In = 0x01,

    /// <summary><c>CURL_CSELECT_OUT</c> (<c>0x02</c>): the socket is writable.</summary>
    Out = 0x02,

    /// <summary><c>CURL_CSELECT_ERR</c> (<c>0x04</c>): the socket has an error.</summary>
    Error = 0x04,
}
