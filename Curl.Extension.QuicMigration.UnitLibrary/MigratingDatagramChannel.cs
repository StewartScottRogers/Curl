using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.QuicMigration;

/// <summary>
/// A QUIC connection's datagram channel that moves to a new local address when the old one is lost (RFC 9000 section
/// 9.2): once the handshake is complete, a send or receive that fails with <see cref="LocalPathLoss" /> opens a fresh
/// channel to the same server through curl's datagram connector, disposes the old one and carries on, so the QUIC
/// packets go out byte for byte as before from the new address, which the server validates as RFC 9000 section 9.3
/// says. Before that, and for any other failure, the failure ends the transfer as it would without the extension.
/// </summary>
/// <remarks>
/// It holds one path at a time, as draft-ietf-quic-multipath section 2 requires of an endpoint that has not negotiated
/// multipath: the old channel is disposed before the new one is used.
/// </remarks>
internal sealed class MigratingDatagramChannel : IDatagramChannel
{
    private readonly IDatagramConnector connector;
    private readonly string host;
    private readonly int port;
    private readonly SemaphoreSlim migrating = new(1, 1);
    private volatile IDatagramChannel current;
    private volatile bool sentOneRtt;
    private volatile bool receivedOneRtt;

    /// <summary>Initializes a new instance of the <see cref="MigratingDatagramChannel" /> class.</summary>
    /// <param name="first">The channel curl's connector opened first.</param>
    /// <param name="connector">curl's datagram connector, which opens each migrated channel.</param>
    /// <param name="host">The host the first channel was opened to.</param>
    /// <param name="port">The port the first channel was opened to.</param>
    public MigratingDatagramChannel(IDatagramChannel first, IDatagramConnector connector, string host, int port)
    {
        current = first;
        this.connector = connector;
        this.host = host;
        this.port = port;
    }

    /// <summary>Gets how many times the channel moved to a new local address.</summary>
    public int Migrations { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the handshake is complete as far as the datagrams show: a 1-RTT packet went
    /// each way (RFC 9000 section 9: no migration before the handshake is confirmed).
    /// </summary>
    public bool HandshakeComplete => sentOneRtt && receivedOneRtt;

    /// <inheritdoc />
    public EndPoint ServerEndPoint => current.ServerEndPoint;

    /// <inheritdoc />
    public EndPoint? LocalEndPoint => current.LocalEndPoint;

    /// <inheritdoc />
    public async ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
    {
        sentOneRtt |= QuicPacketForm.IsOneRtt(datagram.Span);
        IDatagramChannel channel = current;
        try
        {
            await channel.SendAsync(datagram, destination, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (MayMoveAfter(channel, error))
        {
            IDatagramChannel moved = await MoveFromAsync(channel, error, cancellationToken).ConfigureAwait(false);
            await moved.SendAsync(datagram, destination, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        IDatagramChannel channel = current;
        DatagramReceived received;
        try
        {
            received = await channel.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (MayMoveAfter(channel, error))
        {
            IDatagramChannel moved = await MoveFromAsync(channel, error, cancellationToken).ConfigureAwait(false);
            received = await moved.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        receivedOneRtt |= QuicPacketForm.IsOneRtt(buffer.Span[..received.Length]);
        return received;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await current.DisposeAsync().ConfigureAwait(false);
        migrating.Dispose();
    }

    /// <summary>
    /// Says whether a failure on <paramref name="channel" /> is answered on another channel: the channel was already
    /// replaced (a receive pending when a send moved), or the handshake is complete and the local path was lost.
    /// </summary>
    private bool MayMoveAfter(IDatagramChannel channel, Exception error) =>
        !ReferenceEquals(channel, current) || (HandshakeComplete && LocalPathLoss.Means(error));

    /// <summary>
    /// Gives the channel to carry on over: the current one when another operation already moved off
    /// <paramref name="failed" />, otherwise a new one opened through curl's connector, after disposing
    /// <paramref name="failed" />. When no new channel opens, <paramref name="error" /> ends the transfer as before.
    /// </summary>
    private async ValueTask<IDatagramChannel> MoveFromAsync(IDatagramChannel failed, Exception error, CancellationToken cancellationToken)
    {
        await migrating.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!ReferenceEquals(failed, current))
            {
                return current;
            }

            DatagramOpenResult opened = await connector.OpenAsync(host, port, cancellationToken).ConfigureAwait(false);
            IDatagramChannel moved = opened.Channel ?? throw error;
            current = moved;
            Migrations++;
            await failed.DisposeAsync().ConfigureAwait(false);
            return moved;
        }
        finally
        {
            migrating.Release();
        }
    }
}
