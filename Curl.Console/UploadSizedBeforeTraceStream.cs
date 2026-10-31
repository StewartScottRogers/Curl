namespace Curl.Console;

/// <summary>
/// A read-only stream over a <c>-T</c> file that reports the size the file had before the run's trace
/// file opened, rather than its size now. curl 8.22.0 sizes the upload before its first trace event
/// opens (and truncates) the <c>--trace</c> file, so when the two name the same file it sends nothing
/// and fails with exit 26, <c>client read function EOF fail, only 0/9 of needed bytes read</c>
/// (AF-0513, ADR-1174). The bytes and seeks pass through unchanged, each read synchronously; the
/// wrapped stream is owned.
/// </summary>
/// <param name="inner">The opened <c>-T</c> file.</param>
/// <param name="sizeBeforeTrace">The file's size before the trace file opened.</param>
internal sealed class UploadSizedBeforeTraceStream(Stream inner, long sizeBeforeTrace) : Stream
{
    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => inner.CanSeek;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => sizeBeforeTrace;

    /// <inheritdoc />
    public override long Position
    {
        get => inner.Position;
        set => inner.Position = value;
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

    /// <summary>
    /// Reads synchronously, as curl's <c>fread</c> of the file does, so the read has completed when the
    /// HTTP handler looks at it and a short first read sends no request head (AF-0513).
    /// </summary>
    /// <param name="buffer">Where the bytes go.</param>
    /// <param name="cancellationToken">Not used: the read completes before it returns.</param>
    /// <returns>The completed read's byte count.</returns>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        new(inner.Read(buffer.Span));

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await inner.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }
}
