using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// The production <see cref="ITcpDialer" />: connects a TCP <see cref="Socket" /> and
/// returns it as a <see cref="StreamConnection" /> over a <see cref="NetworkStream" />
/// that owns the socket, with the local end point the socket was bound to.
/// </summary>
public sealed class TcpDialer : ITcpDialer
{
    /// <inheritdoc />
    /// <remarks>
    /// Excluded from coverage per ADR-0083: every line after the argument check needs a
    /// connected TCP socket, so the loopback test in the Integration run measures it.
    /// </remarks>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin socket adapter, measured by the Integration run.")]
    public async ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
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

        var localEndPoint = (IPEndPoint)socket.LocalEndPoint!;

        return new DialedTcpConnection(
            new StreamConnection(new NetworkStream(socket, ownsSocket: true), endPoint, localEndPoint),
            localEndPoint);
    }
}
