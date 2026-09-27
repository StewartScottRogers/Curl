namespace Curl.Protocol.Http;

/// <summary>
/// The read-only stream a BCL decompression stream pulls encoded body bytes from, one
/// received piece at a time: <see cref="Pending" /> is set to each piece, and a read returns
/// what is left of it, or 0 once it is used up.
/// </summary>
/// <remarks>
/// <see cref="System.IO.Compression.GZipStream" />, <see cref="System.IO.Compression.ZLibStream" />
/// and <see cref="System.IO.Compression.DeflateStream" /> each read their source only when they
/// need more input, and a read of 0 makes them return 0 without ending the stream for good,
/// so a later read decodes the next piece. The tests in
/// <c>HttpContentDecoderTests</c> feed every coding one byte at a time to hold that
/// in place.
/// </remarks>
internal sealed class HttpContentInput : Stream
{
    /// <summary>
    /// Gets or sets the encoded bytes not yet read.
    /// </summary>
    internal ReadOnlyMemory<byte> Pending { get; set; }

    /// <summary>
    /// Gets how many times the stream has been read. A decompression stream that has reached
    /// the end of its stream reads its source no more.
    /// </summary>
    internal int ReadCount { get; private set; }

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
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        ReadCount++;
        int count = Math.Min(buffer.Length, Pending.Length);
        Pending.Span[..count].CopyTo(buffer);
        Pending = Pending[count..];
        return count;
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
