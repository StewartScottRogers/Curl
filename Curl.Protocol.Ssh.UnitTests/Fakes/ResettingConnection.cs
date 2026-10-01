using System.Net;
using System.Net.Sockets;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// An <see cref="IConnection" /> whose peer sends what it has and then resets the
/// connection: once the peer's bytes run out, every read throws the <see cref="IOException" />
/// a <see cref="NetworkStream" /> throws for a reset (BL-991), where a closed connection
/// would read the end of the stream. The peer is either a script of reads or another
/// connection, such as <see cref="InMemorySshServer" />'s, whose close becomes the reset
/// (BL-1046).
/// </summary>
public sealed class ResettingConnection : IConnection
{
    private readonly IConnection peer;

    private readonly bool resetOnWrite;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResettingConnection" /> class whose peer
    /// sends scripted reads and then resets.
    /// </summary>
    /// <param name="resetOnWrite">Whether a write throws the reset too, as when it arrived before the client sent anything.</param>
    /// <param name="reads">The chunks the peer sends before it resets, in order.</param>
    public ResettingConnection(bool resetOnWrite, params byte[][] reads)
    {
        peer = new ScriptedConnection(reads);
        this.resetOnWrite = resetOnWrite;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResettingConnection" /> class over
    /// <paramref name="peer" />, whose end of the stream becomes a reset. Writes go to it,
    /// and one after it has closed is dropped, as a socket's send buffer takes it before
    /// the reset arrives, so the reset shows at the next read.
    /// </summary>
    /// <param name="peer">The connection to the peer.</param>
    public ResettingConnection(IConnection peer)
    {
        this.peer = peer;
    }

    /// <summary>
    /// Gets every byte written to a scripted connection, in order; empty over another
    /// connection.
    /// </summary>
    public byte[] Written => peer is ScriptedConnection scripted ? scripted.Written : [];

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <inheritdoc />
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int read = await peer.ReadAsync(buffer, cancellationToken);
        return read > 0 ? read : throw Reset();
    }

    /// <inheritdoc />
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (resetOnWrite)
        {
            throw Reset();
        }

        try
        {
            await peer.WriteAsync(buffer, cancellationToken);
        }
        catch (IOException)
        {
        }
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => peer.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => peer.DisposeAsync();

    private static IOException Reset() =>
        new("Unable to read data from the transport connection: An existing connection was forcibly closed by the remote host.", new SocketException((int)SocketError.ConnectionReset));
}
