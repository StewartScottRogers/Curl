namespace Curl.Protocol.File.Fakes;

/// <summary>
/// A stream that fails with <see cref="IOException" /> on a chosen read or write, so the
/// mid-transfer failure exit codes can be driven with no disk involved.
/// </summary>
/// <remarks>
/// A cancelled token leaves this stream as a cancelled task, the way
/// <see cref="FileStream" /> and <see cref="MemoryStream" /> report one, so a test that
/// expects <see cref="OperationCanceledException" /> is really testing the handler and
/// not this fake. <see cref="CancellingStream" /> is the fake for cancellation itself.
/// </remarks>
public sealed class FaultingStream : Stream, IRecordingStream
{
    private readonly MemoryStream inner;
    private readonly int failingReadNumber;
    private readonly int failingWriteNumber;
    private readonly bool readable;
    private readonly bool writable;
    private readonly bool seekable;
    private int reads;
    private int writes;

    private FaultingStream(
        MemoryStream inner,
        int failingReadNumber,
        int failingWriteNumber,
        bool readable,
        bool writable,
        bool seekable)
    {
        this.inner = inner;
        this.failingReadNumber = failingReadNumber;
        this.failingWriteNumber = failingWriteNumber;
        this.readable = readable;
        this.writable = writable;
        this.seekable = seekable;
    }

    /// <inheritdoc />
    public bool WasDisposed { get; private set; }

    /// <inheritdoc />
    public override bool CanRead => readable;

    /// <inheritdoc />
    public override bool CanSeek => seekable;

    /// <inheritdoc />
    public override bool CanWrite => writable;

    /// <inheritdoc />
    public override long Length => seekable ? inner.Length : throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => seekable ? inner.Position : throw new NotSupportedException();
        set => inner.Position = seekable ? value : throw new NotSupportedException();
    }

    /// <summary>
    /// Creates a seekable readable stream over <paramref name="content" /> whose
    /// <paramref name="readNumber" />th read throws.
    /// </summary>
    /// <param name="content">The content the earlier reads return.</param>
    /// <param name="readNumber">The one-based read that fails.</param>
    /// <returns>The stream.</returns>
    public static FaultingStream FailingOnRead(byte[] content, int readNumber)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(readNumber);

        var inner = new MemoryStream();
        inner.Write(content, 0, content.Length);
        inner.Position = 0;

        return new FaultingStream(
            inner,
            readNumber,
            failingWriteNumber: 0,
            readable: true,
            writable: false,
            seekable: true);
    }

    /// <summary>
    /// Creates a non-seekable writable stream whose <paramref name="writeNumber" />th
    /// write throws.
    /// </summary>
    /// <param name="writeNumber">The one-based write that fails.</param>
    /// <returns>The stream.</returns>
    public static FaultingStream FailingOnWrite(int writeNumber)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(writeNumber);

        return new FaultingStream(
            new MemoryStream(),
            failingReadNumber: 0,
            writeNumber,
            readable: false,
            writable: true,
            seekable: false);
    }

    /// <summary>
    /// Gets whatever was written before the failure.
    /// </summary>
    /// <returns>The written bytes.</returns>
    public byte[] ToArray() => inner.ToArray();

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        return ReadCore(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc />
    public override int Read(Span<byte> buffer) => ReadCore(buffer);

    /// <inheritdoc />
    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        return cancellationToken.IsCancellationRequested
            ? Task.FromCanceled<int>(cancellationToken)
            : Task.FromResult(ReadCore(buffer.AsSpan(offset, count)));
    }

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        return cancellationToken.IsCancellationRequested
            ? ValueTask.FromCanceled<int>(cancellationToken)
            : ValueTask.FromResult(ReadCore(buffer.Span));
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) =>
        seekable ? inner.Seek(offset, origin) : throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        WriteCore(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer) => WriteCore(buffer);

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

        WriteCore(buffer.AsSpan(offset, count));

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

        WriteCore(buffer.Span);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;

        base.Dispose(disposing);
    }

    private int ReadCore(Span<byte> buffer)
    {
        reads++;

        if (reads == failingReadNumber)
        {
            throw new IOException($"Simulated failure on read {reads}.");
        }

        return inner.Read(buffer);
    }

    private void WriteCore(ReadOnlySpan<byte> buffer)
    {
        writes++;

        if (writes == failingWriteNumber)
        {
            throw new IOException($"Simulated failure on write {writes}.");
        }

        inner.Write(buffer);
    }
}
