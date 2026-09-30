using System.Net;
using System.Net.Sockets;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// An <see cref="IConnection" /> whose peer sends scripted reads and then resets the
/// connection: once the reads run out, every read throws the <see cref="IOException" /> a
/// <see cref="NetworkStream" /> throws for a reset (BL-991), where
/// <see cref="ScriptedConnection" /> would report the peer closed.
/// </summary>
/// <param name="resetOnWrite">Whether a write throws the reset too, as when it arrived before the client sent anything.</param>
/// <param name="reads">The chunks the peer sends before it resets, in order.</param>
public sealed class ResettingConnection(bool resetOnWrite, params byte[][] reads) : IConnection
{
    private readonly ScriptedConnection scripted = new(reads);

    /// <summary>
    /// Gets every byte written to the connection, in order.
    /// </summary>
    public byte[] Written => scripted.Written;

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <inheritdoc />
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int read = await scripted.ReadAsync(buffer, cancellationToken);
        return read > 0 ? read : throw Reset();
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        resetOnWrite ? throw Reset() : scripted.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => scripted.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static IOException Reset() =>
        new("Unable to read data from the transport connection: An existing connection was forcibly closed by the remote host.", new SocketException((int)SocketError.ConnectionReset));
}
