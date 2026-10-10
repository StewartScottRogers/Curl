using System.Threading.Channels;

namespace Curl.Conformance;

/// <summary>
/// One end of an in-memory, two-way byte pipe: what one end of a pair made by
/// <see cref="CreatePair"/> writes, the other end reads, in order. It is how a TLS stand-in
/// server and a client meet without a socket. It is asynchronous only: the synchronous
/// <see cref="Read(byte[], int, int)"/> and <see cref="Write(byte[], int, int)"/> throw.
/// Disposing an end ends the other end's reads at 0 once it has read everything written.
/// </summary>
public sealed class InMemoryDuplexStream : Stream
{
    private readonly ChannelReader<byte[]> incoming;
    private readonly ChannelWriter<byte[]> outgoing;
    private ReadOnlyMemory<byte> pending;

    private InMemoryDuplexStream(ChannelReader<byte[]> incoming, ChannelWriter<byte[]> outgoing)
    {
        this.incoming = incoming;
        this.outgoing = outgoing;
    }

    /// <summary>Always <see langword="true"/>.</summary>
    public override bool CanRead => true;

    /// <summary>Always <see langword="false"/>.</summary>
    public override bool CanSeek => false;

    /// <summary>Always <see langword="true"/>.</summary>
    public override bool CanWrite => true;

    /// <summary>Not supported: a pipe has no length.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override long Length => throw new NotSupportedException();

    /// <summary>Not supported: a pipe has no position.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>Makes two connected ends: each one reads what the other writes.</summary>
    /// <returns>The two ends.</returns>
    public static (InMemoryDuplexStream First, InMemoryDuplexStream Second) CreatePair()
    {
        Channel<byte[]> firstToSecond = Channel.CreateUnbounded<byte[]>();
        Channel<byte[]> secondToFirst = Channel.CreateUnbounded<byte[]>();
        return (new InMemoryDuplexStream(secondToFirst.Reader, firstToSecond.Writer),
            new InMemoryDuplexStream(firstToSecond.Reader, secondToFirst.Writer));
    }

    /// <summary>Does nothing: every write is readable at once.</summary>
    public override void Flush()
    {
    }

    /// <summary>Not supported: use <see cref="ReadAsync(Memory{byte}, CancellationToken)"/>.</summary>
    /// <param name="buffer">Ignored.</param>
    /// <param name="offset">Ignored.</param>
    /// <param name="count">Ignored.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>Not supported: use <see cref="WriteAsync(ReadOnlyMemory{byte}, CancellationToken)"/>.</summary>
    /// <param name="buffer">Ignored.</param>
    /// <param name="offset">Ignored.</param>
    /// <param name="count">Ignored.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>Not supported: a pipe cannot seek.</summary>
    /// <param name="offset">Ignored.</param>
    /// <param name="origin">Ignored.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <summary>Not supported: a pipe has no length.</summary>
    /// <param name="value">Ignored.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <summary>Reads into the array range, as <see cref="ReadAsync(Memory{byte}, CancellationToken)"/> does.</summary>
    /// <param name="buffer">The array to read into.</param>
    /// <param name="offset">Where in it to start.</param>
    /// <param name="count">How many bytes at most.</param>
    /// <param name="cancellationToken">Cancels a read that is waiting for bytes.</param>
    /// <returns>The number of bytes read; 0 once the other end is disposed and drained.</returns>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <summary>
    /// Waits until the other end has written bytes not yet read, then copies as many as fit.
    /// An empty <paramref name="buffer"/> waits the same way and then returns 0.
    /// </summary>
    /// <param name="buffer">Where to copy the bytes.</param>
    /// <param name="cancellationToken">Cancels a read that is waiting for bytes.</param>
    /// <returns>The number of bytes read; 0 once the other end is disposed and drained.</returns>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (pending.IsEmpty)
        {
            try
            {
                pending = await incoming.ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (ChannelClosedException)
            {
                return 0;
            }
        }

        int count = Math.Min(buffer.Length, pending.Length);
        pending.Span[..count].CopyTo(buffer.Span);
        pending = pending[count..];
        return count;
    }

    /// <summary>Writes the array range, as <see cref="WriteAsync(ReadOnlyMemory{byte}, CancellationToken)"/> does.</summary>
    /// <param name="buffer">The array to write from.</param>
    /// <param name="offset">Where in it to start.</param>
    /// <param name="count">How many bytes.</param>
    /// <param name="cancellationToken">Not observed: a write never waits.</param>
    /// <returns>A completed task.</returns>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <summary>
    /// Makes a copy of <paramref name="buffer"/> readable at the other end at once. An empty
    /// buffer writes nothing, so the other end never mistakes it for the end of the stream.
    /// </summary>
    /// <param name="buffer">The bytes to write.</param>
    /// <param name="cancellationToken">Not observed: a write never waits.</param>
    /// <returns>A completed task.</returns>
    /// <exception cref="ObjectDisposedException">This end has been disposed.</exception>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!buffer.IsEmpty && !outgoing.TryWrite(buffer.ToArray()))
        {
            return ValueTask.FromException(new ObjectDisposedException(nameof(InMemoryDuplexStream)));
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Ends the other end's reads once it has read everything already written.</summary>
    /// <param name="disposing">Passed to the base stream.</param>
    protected override void Dispose(bool disposing)
    {
        outgoing.TryComplete();
        base.Dispose(disposing);
    }
}
