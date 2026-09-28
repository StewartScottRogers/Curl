using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// An <see cref="IConnection" /> that hands out scripted reads, one chunk per
/// <see cref="ReadAsync" /> (split further when the reader's buffer is smaller), then reports
/// the peer closed; and records every byte written.
/// </summary>
/// <param name="reads">The chunks the peer sends, in order.</param>
public sealed class ScriptedConnection(params byte[][] reads) : IConnection
{
    private Queue<byte[]> pendingReads = new(reads);

    private readonly MemoryStream written = new();

    /// <summary>
    /// Gets how many times the connection was flushed.
    /// </summary>
    public int FlushCount { get; private set; }

    /// <summary>
    /// Gets every byte written to the connection, in order.
    /// </summary>
    public byte[] Written => written.ToArray();

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <summary>
    /// Creates a connection whose peer sends <paramref name="bytes" /> in chunks of at most
    /// <paramref name="chunkSize" /> bytes, then closes.
    /// </summary>
    /// <param name="bytes">Everything the peer sends.</param>
    /// <param name="chunkSize">The most bytes one read returns.</param>
    /// <returns>The connection.</returns>
    public static ScriptedConnection InChunks(byte[] bytes, int chunkSize) =>
        new([.. bytes.Chunk(chunkSize)]);

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!pendingReads.TryPeek(out byte[]? chunk))
        {
            return ValueTask.FromResult(0);
        }

        int taken = Math.Min(chunk.Length, buffer.Length);
        chunk.AsSpan(0, taken).CopyTo(buffer.Span);
        pendingReads.Dequeue();
        if (taken < chunk.Length)
        {
            pendingReads = new Queue<byte[]>([chunk[taken..], .. pendingReads]);
        }

        return ValueTask.FromResult(taken);
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        written.Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        FlushCount++;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
