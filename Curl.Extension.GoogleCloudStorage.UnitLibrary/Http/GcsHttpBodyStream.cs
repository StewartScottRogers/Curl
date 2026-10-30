namespace Curl.Extension.GoogleCloudStorage.Http;

/// <summary>A read-only, forward-only, asynchronous stream over a response body; each framing (RFC 9112 §6.3) supplies <see cref="ReadBodyAsync"/>.</summary>
internal abstract class GcsHttpBodyStream : Stream
{
    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException("A response body's length is not known in advance.");

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException("A response body cannot seek.");
        set => throw new NotSupportedException("A response body cannot seek.");
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <summary>Not supported: a response body is read asynchronously only.</summary>
    /// <param name="buffer">Unused.</param>
    /// <param name="offset">Unused.</param>
    /// <param name="count">Unused.</param>
    /// <returns>Never returns.</returns>
    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("A response body is read with ReadAsync.");

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        buffer.IsEmpty ? 0 : await ReadBodyAsync(buffer, cancellationToken);

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException("A response body cannot seek.");

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException("A response body cannot be written.");

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("A response body cannot be written.");

    /// <summary>Creates the failure for a connection that closed before the body's framing ended.</summary>
    /// <returns>The failure to throw.</returns>
    protected static GcsHttpException ClosedEarly() =>
        new(GcsHttpFailure.ClosedEarly, "The connection closed before the response body ended.");

    /// <summary>Reads the next bytes of the body.</summary>
    /// <param name="buffer">Where the bytes go; never empty.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The number of bytes read, or zero at the body's end.</returns>
    protected abstract ValueTask<int> ReadBodyAsync(Memory<byte> buffer, CancellationToken cancellationToken);
}
