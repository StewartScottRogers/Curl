using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Mqtt.Fakes;

/// <summary>
/// An <see cref="IConnection" /> that hands out scripted reads, one chunk per
/// <see cref="ReadAsync" />, then reports the peer closed; and records every byte written.
/// </summary>
/// <param name="reads">
/// The chunks the peer sends, in order. A <see langword="null" /> chunk makes that read
/// throw an <see cref="IOException" />, as a reset connection does.
/// </param>
public sealed class ScriptedConnection(params byte[]?[] reads) : IConnection
{
    private readonly Queue<byte[]?> pendingReads = new(reads);

    /// <summary>
    /// Gets every read the peer was scripted to send, in order, whether or not it has been read yet.
    /// </summary>
    public IReadOnlyList<byte[]?> Reads { get; } = reads;

    private readonly MemoryStream written = new();

    private int writeCount;

    private int readCount;

    /// <summary>
    /// Gets the zero-based numbers of the scripted reads that do not complete at once,
    /// the way a socket read waits when the peer's next bytes have not arrived yet.
    /// </summary>
    public ISet<int> HeldReads { get; } = new HashSet<int>();

    /// <summary>
    /// Gets or sets a value indicating whether every write throws an <see cref="IOException" />.
    /// </summary>
    public bool FailWrites { get; set; }

    /// <summary>
    /// Gets or sets the exception a <see langword="null" /> read chunk throws, or
    /// <see langword="null" /> for a plain <see cref="IOException" /> with no socket error.
    /// </summary>
    public IOException? ReadFailure { get; set; }

    /// <summary>
    /// Gets or sets the exception every write from <see cref="WritesBeforeFailure" /> on
    /// throws, or <see langword="null" /> for none.
    /// </summary>
    public IOException? WriteFailure { get; set; }

    /// <summary>
    /// Gets or sets how many writes succeed before <see cref="WriteFailure" /> is thrown.
    /// </summary>
    public int WritesBeforeFailure { get; set; }

    /// <summary>
    /// Gets a value indicating whether the handler disposed the connection.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Gets every byte written to the connection, in order.
    /// </summary>
    public byte[] Written => written.ToArray();

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!pendingReads.TryDequeue(out byte[]? chunk))
        {
            return ValueTask.FromResult(0);
        }

        if (chunk is null)
        {
            throw ReadFailure ?? new IOException("The scripted peer reset the connection.");
        }

        chunk.CopyTo(buffer);
        return HeldReads.Contains(readCount++) ? ReadAfterYieldingAsync(chunk.Length) : ValueTask.FromResult(chunk.Length);
    }

    /// <summary>
    /// Returns a read that is still pending when <see cref="ReadAsync" /> returns, as a
    /// socket read is when nothing has arrived yet, and completes on the next turn.
    /// </summary>
    private static async ValueTask<int> ReadAfterYieldingAsync(int length)
    {
        await Task.Yield();
        return length;
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (FailWrites)
        {
            throw new IOException("The scripted peer refused the write.");
        }

        if (WriteFailure is not null && writeCount++ >= WritesBeforeFailure)
        {
            throw WriteFailure;
        }

        written.Write(buffer.Span);
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
