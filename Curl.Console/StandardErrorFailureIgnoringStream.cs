namespace Curl.Console;

/// <summary>
/// The write-only stream every standard error line is written through. It passes every
/// write and flush through and ignores an <see cref="IOException" /> from either, the way
/// curl ignores a failed <c>fputs</c> or <c>fflush</c> on <c>stderr</c>, so standard error
/// on a full device (<c>2&gt;/dev/full</c>, ENOSPC) leaves the exit code the transfer's own
/// instead of crashing the process (CGR-008, BL-3537).
/// </summary>
/// <param name="inner">The raw standard error stream, which this stream owns and disposes.</param>
internal sealed class StandardErrorFailureIgnoringStream(Stream inner) : Stream
{
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

    /// <inheritdoc />
    public override void Flush()
    {
        try
        {
            inner.Flush();
        }
        catch (IOException)
        {
        }
    }

    /// <inheritdoc />
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        try
        {
            await inner.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
        }
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        try
        {
            inner.Write(buffer);
        }
        catch (IOException)
        {
        }
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        try
        {
            await inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
        }
    }

    /// <inheritdoc />
    /// <remarks>The class is sealed and has no finalizer, so this only runs from <see cref="Stream.Dispose()" />.</remarks>
    protected override void Dispose(bool disposing)
    {
        inner.Dispose();
        base.Dispose(disposing);
    }
}
