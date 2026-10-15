namespace Curl.Api.Callbacks;

/// <summary>What a <see cref="SocketOptionCallback"/> returns: libcurl's <c>CURL_SOCKOPT_*</c>, with upstream curl 8.22.0's numbers.</summary>
public enum SocketOptionResult
{
    /// <summary><c>CURL_SOCKOPT_OK</c> (0): carry on and connect.</summary>
    Ok = 0,

    /// <summary><c>CURL_SOCKOPT_ERROR</c> (1): fail the transfer with <c>CURLE_ABORTED_BY_CALLBACK</c>.</summary>
    Error = 1,

    /// <summary><c>CURL_SOCKOPT_ALREADY_CONNECTED</c> (2): the socket is already connected; skip connecting.</summary>
    AlreadyConnected = 2,
}
