using System.Net;
using Curl.Extension.WireGuard.Noise;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WireGuard.Tunnel;

/// <summary>One TCP connection inside the tunnel, owning the tunnel's UDP socket and transport keys.</summary>
/// <param name="stream">The user-space TCP connection.</param>
/// <param name="udp">The UDP socket to the peer.</param>
/// <param name="session">The transport keys.</param>
/// <param name="remote">The server's address and port inside the tunnel.</param>
internal sealed class WireGuardConnection(Stream stream, IWireGuardUdpChannel udp, WireGuardTransportSession session, IPEndPoint remote) : IConnection
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

    /// <summary>Closes the TCP connection, then the UDP socket, then zeroes the keys.</summary>
    /// <returns>A task that completes when all three are closed.</returns>
    public async ValueTask DisposeAsync()
    {
        await stream.DisposeAsync().ConfigureAwait(false);
        await udp.DisposeAsync().ConfigureAwait(false);
        session.Dispose();
    }
}
