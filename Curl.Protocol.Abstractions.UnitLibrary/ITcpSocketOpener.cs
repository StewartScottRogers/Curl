using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Decides, before each TCP connect attempt of a connect, how its socket is opened: libcurl's
/// <c>CURLOPT_OPENSOCKETFUNCTION</c>, which curl 8.22.0's <c>cf_socket_open</c> calls before the
/// connect, and <c>CURLOPT_CLOSESOCKETFUNCTION</c> for an attempt whose connect fails (ADR-0818).
/// </summary>
/// <remarks>
/// Set on <see cref="ConnectTarget.SocketOpener" />; the connector asks it for every address its
/// address race dials, the proxy's included.
/// </remarks>
public interface ITcpSocketOpener
{
    /// <summary>
    /// Decides how the socket for the attempt to <paramref name="remoteEndPoint" /> is opened.
    /// </summary>
    /// <param name="remoteEndPoint">The address and port the attempt connects to.</param>
    /// <returns>
    /// <see cref="TcpSocketOpenDecision.Proceed" /> for the connector's own socket,
    /// <see cref="TcpSocketOpenDecision.Refuse" /> to fail the attempt as a socket that would not open,
    /// or <see cref="TcpSocketOpenDecision.Supplied" /> to use a socket the application made.
    /// </returns>
    TcpSocketOpenDecision Open(IPEndPoint remoteEndPoint);

    /// <summary>
    /// Tells the opener that the socket of the attempt to <paramref name="remoteEndPoint" /> closed
    /// because its connect failed or was abandoned; never called for a refused attempt, which opened none.
    /// </summary>
    /// <param name="remoteEndPoint">The address and port the attempt connected to.</param>
    void SocketClosed(IPEndPoint remoteEndPoint);
}
