namespace Curl.Api.Callbacks;

/// <summary>
/// <c>CURLOPT_CLOSESOCKETFUNCTION</c>: closes a socket in place of libcurl.
/// <c>CURLOPT_CLOSESOCKETDATA</c> is the delegate's closure.
/// </summary>
/// <param name="socket">The socket to close.</param>
/// <returns>0 on success, any other value on failure.</returns>
public delegate int CloseSocketCallback(CurlSocket socket);
