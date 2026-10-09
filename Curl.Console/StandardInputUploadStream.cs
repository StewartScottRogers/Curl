namespace Curl.Console;

/// <summary>
/// A read-only stream over standard input for <c>-T -</c> and <c>-T .</c> that cannot seek, so a
/// protocol handler never learns its size, whatever standard input is: curl 8.21.0 never sizes
/// standard input, so over HTTP/1.1 it sends the upload chunked with <c>Expect: 100-continue</c>
/// and under <c>-0</c> refuses it with exit 25, even when a file is redirected into it
/// (GF-0007, BL-1800). The bytes pass through unchanged. The wrapped stream is not owned.
/// </summary>
/// <param name="inner">Standard input.</param>
internal sealed class StandardInputUploadStream(Stream inner) : Stream
{
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
    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        inner.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
