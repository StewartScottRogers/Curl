using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// An <see cref="IDnsSocketOpener" /> whose UDP queries go through an <see cref="IDatagramConnector" />,
/// the transport the run's datagram protocols use, so a resolver given it reaches whatever that
/// connector reaches: an in-memory DNS server in the conformance harness (ADR-0492, BL-2248). The
/// local address is not bound, as the connector chooses its own; a query retried over TCP fails as
/// an unreachable server.
/// </summary>
/// <param name="datagramConnector">Opens each query's channel to the server.</param>
public sealed class DatagramConnectorDnsSocketOpener(IDatagramConnector datagramConnector) : IDnsSocketOpener
{
    /// <inheritdoc />
    /// <remarks>The channel opens through the connector on its first send.</remarks>
    public IDatagramChannel OpenDatagramChannel(IPEndPoint server, IPAddress localAddress) =>
        new ConnectorDatagramChannel(datagramConnector, server);

    /// <inheritdoc />
    /// <remarks>Always refused: the connector carries datagrams only.</remarks>
    public ValueTask<Stream> ConnectStreamAsync(IPEndPoint server, IPAddress localAddress, CancellationToken cancellationToken) =>
        ValueTask.FromException<Stream>(new SocketException((int)SocketError.ConnectionRefused));

    /// <summary>A channel that opens through the connector on its first send and forwards to it after.</summary>
    private sealed class ConnectorDatagramChannel(IDatagramConnector datagramConnector, IPEndPoint server) : IDatagramChannel
    {
        private IDatagramChannel? _opened;

        public EndPoint ServerEndPoint => server;

        public async ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
        {
            _opened ??= await OpenAsync(cancellationToken).ConfigureAwait(false);
            await _opened.SendAsync(datagram, destination, cancellationToken).ConfigureAwait(false);
        }

        public ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
            _opened is null
                ? ValueTask.FromException<DatagramReceived>(new SocketException((int)SocketError.NotConnected))
                : _opened.ReceiveAsync(buffer, cancellationToken);

        public ValueTask DisposeAsync() => _opened?.DisposeAsync() ?? ValueTask.CompletedTask;

        private async ValueTask<IDatagramChannel> OpenAsync(CancellationToken cancellationToken)
        {
            var result = await datagramConnector.OpenAsync(server.Address.ToString(), server.Port, cancellationToken).ConfigureAwait(false);
            return result.Channel ?? throw new SocketException((int)SocketError.ConnectionRefused);
        }
    }
}
