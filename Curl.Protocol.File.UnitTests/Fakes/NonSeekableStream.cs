namespace Curl.Protocol.File.Fakes;

/// <summary>
/// A forward-only readable stream, standing in for the standard input that
/// <c>-T -</c> uploads from. Nothing about it can be seeked or measured.
/// </summary>
/// <remarks>
/// A cancelled token leaves this stream as a cancelled task, the way a real stream
/// reports one, so no test of the handler's cancellation behaviour is answered by this
/// fake. <see cref="CancellingStream" /> is the fake for cancellation itself.
/// </remarks>
public sealed class NonSeekableStream : Stream, IRecordingStream
{
    private readonly MemoryStream inner;

    /// <summary>
    /// Creates a stream that yields <paramref name="content" /> and nothing more.
    /// </summary>
    /// <param name="content">The content to yield.</param>
    public NonSeekableStream(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        inner = new MemoryStream();
        inner.Write(content, 0, content.Length);
        inner.Position = 0;
    }

    /// <inheritdoc />
    public bool WasDisposed { get; private set; }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

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
    }

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        return inner.Read(buffer, offset, count);
    }

    /// <inheritdoc />
    public override int Read(Span<byte> buffer) => inner.Read(buffer);

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
            : Task.FromResult(inner.Read(buffer, offset, count));
    }

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        return cancellationToken.IsCancellationRequested
            ? ValueTask.FromCanceled<int>(cancellationToken)
            : ValueTask.FromResult(inner.Read(buffer.Span));
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;

        base.Dispose(disposing);
    }
}
