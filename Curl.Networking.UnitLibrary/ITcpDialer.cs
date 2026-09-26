using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Opens one plaintext TCP connection to one address: the only step of a connect that
/// touches a socket, kept behind this seam so <see cref="TcpConnector" /> is tested
/// without a network.
/// </summary>
public interface ITcpDialer
{
    /// <summary>
    /// Connects to <paramref name="endPoint" />.
    /// </summary>
    /// <param name="endPoint">The address and port to connect to.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>The open plaintext connection.</returns>
    /// <exception cref="SocketException">The connection could not be made.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was cancelled.
    /// </exception>
    ValueTask<IConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken);
}
