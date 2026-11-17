namespace Curl.Conformance.Extended.McpClient.Stdio;

/// <summary>A write-only stream over <c>inner</c> that records, as soon as it happens, that it was closed.</summary>
internal sealed class ClosableObservedStream(Stream inner) : Stream
{
    /// <summary>Gets a value indicating whether this stream was disposed.</summary>
    public bool Closed => closed;

    private volatile bool closed;

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
    public override void Flush() => inner.Flush();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => inner.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        closed = true;
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
