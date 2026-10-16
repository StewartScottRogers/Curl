using Curl.Api.Callbacks;

namespace Curl.Api.EngineContracts.Multi;

/// <summary>The special socket values of the multi socket-action API.</summary>
public static class CurlMultiSockets
{
    /// <summary>
    /// <c>CURL_SOCKET_TIMEOUT</c>: passed to <see cref="IMultiTransfer.SocketActionAsync"/> when a
    /// timer expired rather than a socket. Upstream defines it as <c>CURL_SOCKET_BAD</c> (-1).
    /// </summary>
    public static CurlSocket Timeout => CurlSocket.Bad;
}
