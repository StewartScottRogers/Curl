namespace Curl.Api.Callbacks;

/// <summary>
/// An opaque socket handle as libcurl's callbacks see it: <c>curl_socket_t</c>, the operating
/// system's socket descriptor, carried as a number so a native engine can implement the socket
/// callbacks as well as the managed one (ADR-0506). It is never a <c>System.Net.Sockets.Socket</c>.
/// </summary>
/// <param name="Handle">The socket descriptor (a file descriptor, or a Winsock <c>SOCKET</c>).</param>
public readonly record struct CurlSocket(nint Handle)
{
    /// <summary>
    /// <c>CURL_SOCKET_BAD</c>: no socket. It is -1 on every platform (Winsock's <c>INVALID_SOCKET</c>
    /// is <c>~0</c>, the same bits). An open-socket callback returns it to fail the connection.
    /// </summary>
    public static CurlSocket Bad { get; } = new(-1);
}
