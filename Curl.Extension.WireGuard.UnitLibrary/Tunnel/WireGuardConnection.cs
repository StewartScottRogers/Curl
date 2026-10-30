using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WireGuard.Tunnel;

/// <summary>One TCP connection inside the tunnel, owning its tunnel and the tunnel's UDP socket.</summary>
/// <param name="stream">The user-space TCP connection.</param>
/// <param name="udp">The UDP socket to the peer.</param>
/// <param name="tunnel">The tunnel, holding the transport keys and the keepalive timers.</param>
/// <param name="remote">The server's address and port inside the tunnel.</param>
internal sealed class WireGuardConnection(Stream stream, IWireGuardUdpChannel udp, WireGuardPacketTunnel tunnel, IPEndPoint remote) : IConnection
{
    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => remote;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        stream.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        stream.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) =>
        new(stream.FlushAsync(cancellationToken));

    /// <summary>Closes the TCP connection, then the UDP socket, then stops the tunnel's timers and zeroes its keys.</summary>
    /// <returns>A task that completes when all three are closed.</returns>
    public async ValueTask DisposeAsync()
    {
        await stream.DisposeAsync().ConfigureAwait(false);
        await udp.DisposeAsync().ConfigureAwait(false);
        tunnel.Dispose();
    }
}
