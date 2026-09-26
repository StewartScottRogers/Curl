using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The production <see cref="ITcpDialer" />: connects a TCP <see cref="Socket" /> and
/// returns it as a <see cref="StreamConnection" /> over a <see cref="NetworkStream" />
/// that owns the socket.
/// </summary>
public sealed class TcpDialer : ITcpDialer
{
    /// <inheritdoc />
    public async ValueTask<IConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endPoint);

        var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(endPoint, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        socket.NoDelay = true;

        return new StreamConnection(new NetworkStream(socket, ownsSocket: true), endPoint);
    }
}
