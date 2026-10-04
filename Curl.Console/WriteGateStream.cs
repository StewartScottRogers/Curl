namespace Curl.Console;

/// <summary>
/// A write-only stream that writes and flushes the stream it wraps holding a <see cref="WriteGate" />,
/// so a write is never split by another transfer's. The bytes pass through unchanged. The wrapped
/// stream is not owned.
/// </summary>
/// <param name="inner">The stream written to.</param>
/// <param name="gate">The run's write gate.</param>
internal sealed class WriteGateStream(Stream inner, WriteGate gate) : Stream
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
    public override void Flush() => gate.RunExclusive(inner.Flush);

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) =>
        gate.RunExclusiveAsync(() => inner.FlushAsync(cancellationToken));

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) =>
        gate.RunExclusive(() => inner.Write(buffer, offset, count));

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        gate.WriteExclusiveAsync(inner, buffer, cancellationToken);
}
