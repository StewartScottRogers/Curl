using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Presents an <see cref="IConnection" /> as the <see cref="Stream" /> the HTTP/2 frame layer
/// reads and writes, reading ahead into a buffer so that several streams of one
/// <see cref="Http2Session" /> can wait for the connection's next bytes
/// (<see cref="WaitForBytesAsync" />) without taking them, and only the one that then holds the
/// session's frame lock reads a frame (BL-717). Writes and flushes pass straight through. Only
/// the asynchronous members work; the synchronous ones throw <see cref="NotSupportedException" />.
/// </summary>
/// <remarks>
/// One read of the connection is in flight at a time, shared by every waiter. It is not
/// cancelled when a waiter gives up, since other streams may still want its bytes; closing the
/// connection ends it.
/// </remarks>
/// <param name="connection">The connection; the stream does not dispose it.</param>
internal sealed class ReadAheadConnectionStream(IConnection connection) : Stream
{
    /// <summary>The most bytes one read of the connection takes.</summary>
    internal const int BufferSize = 16384;

    private readonly byte[] buffer = new byte[BufferSize];

    private readonly Lock gate = new();

    private int start;

    private int end;

    private bool isEnded;

    private TaskCompletionSource? filling;

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// Gets a value indicating whether a read returns at once: bytes are buffered, or the
    /// connection has ended.
    /// </summary>
    public bool HasBytesOrEnded
    {
        get
        {
            lock (gate)
            {
                return start < end || isEnded;
            }
        }
    }

    /// <summary>
    /// Waits until a read would return at once (<see cref="HasBytesOrEnded" />), reading the
    /// connection when nobody is already reading it; the bytes stay buffered for whoever reads.
    /// </summary>
    /// <param name="cancellationToken">Ends this wait, not the read of the connection.</param>
    /// <returns>A task that completes when bytes are buffered or the connection has ended.</returns>
    /// <exception cref="IOException">The read of the connection failed.</exception>
    public Task WaitForBytesAsync(CancellationToken cancellationToken)
    {
        TaskCompletionSource? started = null;
        Task fill;
        lock (gate)
        {
            if (start < end || isEnded)
            {
                return Task.CompletedTask;
            }

            if (filling is null)
            {
                filling = started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                start = end = 0;
            }

            fill = filling.Task;
        }

        if (started is not null)
        {
            _ = FillAsync(started);
        }

        return fill.WaitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
    {
        await WaitForBytesAsync(cancellationToken).ConfigureAwait(false);
        lock (gate)
        {
            int count = Math.Min(end - start, destination.Length);
            buffer.AsSpan(start, count).CopyTo(destination.Span);
            start += count;
            return count;
        }
    }

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default) =>
        connection.WriteAsync(source, cancellationToken);

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) =>
        connection.FlushAsync(cancellationToken).AsTask();

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Flush() => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    private async Task FillAsync(TaskCompletionSource started)
    {
        try
        {
            int read = await connection.ReadAsync(buffer, CancellationToken.None).ConfigureAwait(false);
            lock (gate)
            {
                end = read;
                isEnded = read == 0;
                filling = null;
            }

            started.SetResult();
        }
        catch (Exception exception)
        {
            lock (gate)
            {
                filling = null;
            }

            started.SetException(exception);
        }
    }
}
