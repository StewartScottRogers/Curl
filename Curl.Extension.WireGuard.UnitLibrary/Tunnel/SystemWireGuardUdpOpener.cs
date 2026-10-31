using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using Curl.Extension.WireGuard.Configuration;

namespace Curl.Extension.WireGuard.Tunnel;

/// <summary>Opens a system UDP socket to a peer's endpoint, resolving its host with the system resolver.</summary>
[ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin UDP socket adapter; every line needs a live socket.")]
internal sealed class SystemWireGuardUdpOpener : IWireGuardUdpOpener
{
    /// <inheritdoc />
    public async ValueTask<IWireGuardUdpChannel> OpenAsync(WireGuardEndpoint endpoint, CancellationToken cancellationToken)
    {
        IPAddress[] addresses = await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken).ConfigureAwait(false);
        Socket socket = new(addresses[0].AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        try
        {
            await socket.ConnectAsync(new IPEndPoint(addresses[0], endpoint.Port), cancellationToken).ConfigureAwait(false);
            return new SystemWireGuardUdpChannel(socket);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>A connected UDP socket as the tunnel's datagram path.</summary>
    /// <param name="socket">The connected socket, owned from here on.</param>
    private sealed class SystemWireGuardUdpChannel(Socket socket) : IWireGuardUdpChannel
    {
        private const int LargestDatagram = 65535;

        /// <inheritdoc />
        public async ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken) =>
            await socket.SendAsync(datagram, SocketFlags.None, cancellationToken).ConfigureAwait(false);

        /// <inheritdoc />
        public async ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[LargestDatagram];
            int received = await socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);
            return buffer.AsMemory(0, received);
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            socket.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
