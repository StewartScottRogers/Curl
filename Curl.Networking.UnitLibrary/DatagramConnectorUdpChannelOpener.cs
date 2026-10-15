using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// An <see cref="IUdpChannelOpener" /> whose QUIC channels go through an <see cref="IDatagramConnector" />,
/// the transport the run's datagram protocols use, so a <see cref="QuicDialer" /> given it reaches
/// whatever that connector reaches: an in-memory HTTP/3 server in the conformance harness (BL-2260).
/// Each channel opens to the endpoint's address and port through the connector on its first send or
/// receive. No local end is bound: <c>--interface</c> and <c>--local-port</c> are left to the connector,
/// which chooses its own, so <see cref="OpenFrom" /> and <see cref="OpenFromDeviceAsync" /> open as
/// <see cref="Open" /> does and write no bind lines. A connector that cannot open the channel fails
/// that send or receive with <see cref="SocketError.ConnectionRefused" />.
/// </summary>
/// <param name="datagramConnector">Opens each channel to the server.</param>
public sealed class DatagramConnectorUdpChannelOpener(IDatagramConnector datagramConnector) : IUdpChannelOpener
{
    /// <inheritdoc />
    public IDatagramChannel Open(IPEndPoint serverEndPoint)
    {
        ArgumentNullException.ThrowIfNull(serverEndPoint);
        return new ConnectorDatagramChannel(datagramConnector, serverEndPoint);
    }

    /// <inheritdoc />
    /// <remarks>The local end is not bound; the channel opens as <see cref="Open" /> opens it.</remarks>
    public IDatagramChannel OpenFrom(IPEndPoint serverEndPoint, IPEndPoint localEndPoint, int localPortCount, ITransferEvents events) =>
        Open(serverEndPoint);

    /// <inheritdoc />
    /// <remarks>No device or local end is bound; the channel opens as <see cref="Open" /> opens it.</remarks>
    public ValueTask<IDatagramChannel> OpenFromDeviceAsync(
        IPEndPoint serverEndPoint,
        string deviceName,
        bool bindsAddressAfterDevice,
        Func<CancellationToken, ValueTask<IPEndPoint>> chooseLocalEndAsync,
        int localPortCount,
        ITransferEvents events,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Open(serverEndPoint));

    /// <summary>
    /// A channel that opens through the connector on its first send or receive, both waiting on the
    /// one open, and forwards to the opened channel after.
    /// </summary>
    private sealed class ConnectorDatagramChannel(IDatagramConnector datagramConnector, IPEndPoint server) : IDatagramChannel
    {
        private readonly Lock _gate = new();
        private Task<IDatagramChannel>? _opening;
        private IDatagramChannel? _opened;

        public EndPoint ServerEndPoint => server;

        public EndPoint? LocalEndPoint => _opened?.LocalEndPoint;

        public async ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
        {
            var opened = await OpenedAsync(cancellationToken).ConfigureAwait(false);
            await opened.SendAsync(datagram, destination, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            var opened = await OpenedAsync(cancellationToken).ConfigureAwait(false);
            return await opened.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        public ValueTask DisposeAsync() => _opened?.DisposeAsync() ?? ValueTask.CompletedTask;

        private Task<IDatagramChannel> OpenedAsync(CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                return _opening ??= OpenAsync(cancellationToken);
            }
        }

        private async Task<IDatagramChannel> OpenAsync(CancellationToken cancellationToken)
        {
            var result = await datagramConnector.OpenAsync(server.Address.ToString(), server.Port, cancellationToken).ConfigureAwait(false);
            _opened = result.Channel ?? throw new SocketException((int)SocketError.ConnectionRefused);
            return _opened;
        }
    }
}
