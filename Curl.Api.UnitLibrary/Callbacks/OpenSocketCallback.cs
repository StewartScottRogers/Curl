namespace Curl.Api.Callbacks;

/// <summary>
/// <c>CURLOPT_OPENSOCKETFUNCTION</c>: creates the socket libcurl connects with.
/// <c>CURLOPT_OPENSOCKETDATA</c> is the delegate's closure.
/// </summary>
/// <param name="purpose">What the socket is for.</param>
/// <param name="address">The address the socket will connect to.</param>
/// <returns>The new socket, or <see cref="CurlSocket.Bad"/> to fail the connection.</returns>
public delegate CurlSocket OpenSocketCallback(CurlSocketType purpose, CurlSocketAddress address);
