using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws.Fakes;

/// <summary>
/// An <see cref="IConnection" /> that plays a WebSocket server from a script: each element of
/// <paramref name="reads" /> is returned by one read, in order (split over several when the
/// reader's buffer is shorter), and once the script is
/// exhausted every read returns zero, which is the server closing. A read on a cancelled token
/// throws. Every byte written is recorded in <see cref="Sent" />.
/// </summary>
/// <param name="reads">What the server sends, one non-empty read at a time.</param>
public sealed class ScriptedConnection(params byte[][] reads) : IConnection
{
    private readonly List<byte> sent = [];

    private int nextRead;

    private int readOffset;

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <summary>Gets a value indicating whether the connection has been disposed.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>Gets every byte written so far, in order.</summary>
    public byte[] Sent => [.. sent];

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (nextRead == reads.Length)
        {
            return ValueTask.FromResult(0);
        }

        byte[] read = reads[nextRead];
        int length = Math.Min(read.Length - readOffset, buffer.Length);
        read.AsSpan(readOffset, length).CopyTo(buffer.Span);
        readOffset += length;
        if (readOffset == read.Length)
        {
            nextRead++;
            readOffset = 0;
        }

        return ValueTask.FromResult(length);
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        sent.AddRange(buffer.Span);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
