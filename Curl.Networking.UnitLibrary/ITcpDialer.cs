using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Opens one plaintext stream connection, to one TCP address or one Unix domain socket: the only
/// step of a connect that touches a socket, kept behind this seam so <see cref="TcpConnector" /> is
/// tested without a network.
/// </summary>
public interface ITcpDialer
{
    /// <summary>
    /// Connects to <paramref name="endPoint" />.
    /// </summary>
    /// <param name="endPoint">The address and port to connect to.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>The open plaintext connection and the local end point of its socket.</returns>
    /// <exception cref="SocketException">The connection could not be made.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was cancelled.
    /// </exception>
    ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken);

    /// <summary>
    /// Connects to the Unix domain socket <paramref name="address" /> names (<c>--unix-socket</c>,
    /// <c>--abstract-unix-socket</c>).
    /// </summary>
    /// <param name="address">The socket to connect to.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>The open plaintext connection.</returns>
    /// <exception cref="SocketException">The connection could not be made.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was cancelled.
    /// </exception>
    ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken);
}
