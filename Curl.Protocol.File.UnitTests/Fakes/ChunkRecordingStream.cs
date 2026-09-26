namespace Curl.Protocol.File.Fakes;

/// <summary>
/// A write-only, non-seekable destination that records the length of every write as well
/// as the bytes. The lengths are what pin the handler's chunk size; the bytes are what
/// pin its payload.
/// </summary>
/// <remarks>
/// <para>
/// Non-seekable on purpose: curl's default destination is standard output, so a handler
/// that needed to seek its output would be wrong.
/// </para>
/// <para>
/// A cancelled token leaves this stream as a cancelled <see cref="ValueTask" /> rather
/// than as a thrown <see cref="OperationCanceledException" />, because that is what
/// <see cref="FileStream" /> and <see cref="MemoryStream" /> do: the caller sees
/// <see cref="TaskCanceledException" />. A fake that threw
/// <see cref="OperationCanceledException" /> itself would hand every cancellation test
/// the answer it was supposed to be checking for.
/// </para>
/// </remarks>
public sealed class ChunkRecordingStream : Stream, IRecordingStream
{
    private readonly List<int> writeLengths = [];
    private readonly MemoryStream written = new();

    /// <summary>
    /// Gets the length passed to each write, in the order the writes happened.
    /// </summary>
    public IReadOnlyList<int> WriteLengths => writeLengths;

    /// <inheritdoc />
    public bool WasDisposed { get; private set; }

    /// <inheritdoc />
    public override bool CanRead => false;

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
    /// Gets every byte written, concatenated.
    /// </summary>
    /// <returns>The written bytes.</returns>
    public byte[] ToArray() => written.ToArray();

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        Record(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer) => Record(buffer);

    /// <inheritdoc />
    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        Record(buffer.AsSpan(offset, count));

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled(cancellationToken);
        }

        Record(buffer.Span);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;

        base.Dispose(disposing);
    }

    private void Record(ReadOnlySpan<byte> buffer)
    {
        writeLengths.Add(buffer.Length);
        written.Write(buffer);
    }
}
