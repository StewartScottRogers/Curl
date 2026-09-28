namespace Curl.Protocol.Http;

/// <summary>
/// Reads <paramref name="prefix" />, then the rest of <paramref name="rest" />: the body a
/// resend sends when the stream it was read from cannot seek back, so the bytes of the piece a
/// <c>417</c> cut short go first (measured on curl 8.21.0, BL-319 Notes).
/// </summary>
/// <remarks>
/// Read-only and forward-only. Disposing it does nothing: <paramref name="rest" /> belongs to
/// whoever opened it.
/// </remarks>
/// <param name="prefix">The bytes read first.</param>
/// <param name="rest">The stream read once <paramref name="prefix" /> is used up.</param>
internal sealed class HttpPrefixedStream(ReadOnlyMemory<byte> prefix, Stream rest) : Stream
{
    private ReadOnlyMemory<byte> remainingPrefix = prefix;

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
        if (remainingPrefix.IsEmpty)
        {
            return rest.Read(buffer);
        }

        return TakePrefix(buffer);
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (remainingPrefix.IsEmpty)
        {
            return await rest.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        return TakePrefix(buffer.Span);
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

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

    private int TakePrefix(Span<byte> buffer)
    {
        int length = Math.Min(buffer.Length, remainingPrefix.Length);
        remainingPrefix.Span[..length].CopyTo(buffer);
        remainingPrefix = remainingPrefix[length..];
        return length;
    }
}
