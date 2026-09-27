using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// What <see cref="ITcpDialer.DialAsync(IPEndPoint, CancellationToken)" /> opened: the
/// plaintext connection and the local address and port of its socket.
/// </summary>
/// <param name="connection">The open plaintext connection.</param>
/// <param name="localEndPoint">The local address and port the socket was bound to.</param>
public sealed class DialedTcpConnection(IConnection connection, IPEndPoint localEndPoint)
{
    /// <summary>
    /// Gets the open plaintext connection; its <see cref="IConnection.RemoteEndPoint" /> is
    /// the address dialed, the source of <c>%{remote_ip}</c> and <c>%{remote_port}</c>.
    /// </summary>
    public IConnection Connection { get; } = connection;

    /// <summary>
    /// Gets the local address and port the socket was bound to, the source of
    /// <c>%{local_ip}</c> and <c>%{local_port}</c>.
    /// </summary>
    public IPEndPoint LocalEndPoint { get; } = localEndPoint;
}
