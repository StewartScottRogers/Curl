namespace Curl.Api.Callbacks;

/// <summary>
/// <c>CURLOPT_SOCKOPTFUNCTION</c>: sets options on a new socket before it connects.
/// <c>CURLOPT_SOCKOPTDATA</c> is the delegate's closure.
/// </summary>
/// <param name="socket">The new socket.</param>
/// <param name="purpose">What the socket is for.</param>
/// <returns>Whether to connect, fail, or treat the socket as already connected.</returns>
public delegate SocketOptionResult SocketOptionCallback(CurlSocket socket, CurlSocketType purpose);
