namespace Curl.Api.Callbacks;

/// <summary>
/// What a socket handed to a socket callback is for: libcurl's <c>curlsocktype</c>
/// (<c>CURLSOCKTYPE_*</c>), with upstream curl 8.22.0's numbers.
/// </summary>
public enum CurlSocketType
{
    /// <summary><c>CURLSOCKTYPE_IPCXN</c> (0): a socket for an outgoing IP connection.</summary>
    IpConnection = 0,

    /// <summary><c>CURLSOCKTYPE_ACCEPT</c> (1): a socket accepted from a listening one (active FTP).</summary>
    Accept = 1,
}
