namespace Curl.Original.Protocol.Http;

/// <summary>
/// A pushed HTTP/2 stream offered to an <see cref="IHttp2PushSink" /> (BL-2516): the
/// <see cref="Http2StreamConnection" /> the session routes the promised stream's frames to,
/// presented as the read-only <see cref="Stream" /> that is its <see cref="Content" />.
/// </summary>
/// <param name="stream">The promised stream, adopted by <see cref="Http2StreamConnection.AdoptPromised" />.</param>
internal sealed class Http2PushedResponse(Http2StreamConnection stream) : Stream, IHttpPushedResponse
{
    /// <inheritdoc />
    public int StreamId => stream.StreamId;

    /// <inheritdoc />
    public Stream Content => this;

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always: a pushed response has no length.</exception>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always: a pushed response has no position.</exception>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    /// <exception cref="HttpTransferException">The pushed stream failed (exit 16, 18, 56 or 92).</exception>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        stream.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always: a pushed response is read asynchronously only.</exception>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always: a pushed response cannot be written.</exception>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    /// <remarks>Does nothing: nothing is ever written.</remarks>
    public override void Flush()
    {
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always: a pushed response cannot seek.</exception>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always: a pushed response has no length.</exception>
    public override void SetLength(long value) => throw new NotSupportedException();
}
