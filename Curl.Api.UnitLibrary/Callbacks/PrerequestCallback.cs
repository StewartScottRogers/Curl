namespace Curl.Api.Callbacks;

/// <summary>
/// <c>CURLOPT_PREREQFUNCTION</c>: called once a connection is ready, before the request is sent.
/// <c>CURLOPT_PREREQDATA</c> is the delegate's closure.
/// </summary>
/// <param name="primaryIp">The IP address of the peer connected to.</param>
/// <param name="localIp">The local IP address of the connection.</param>
/// <param name="primaryPort">The peer's port.</param>
/// <param name="localPort">The local port.</param>
/// <returns>Whether to send the request.</returns>
public delegate PrerequestResult PrerequestCallback(string primaryIp, string localIp, int primaryPort, int localPort);
