namespace Curl.Protocol.Http;

/// <summary>
/// Reads a request body with every line feed not already after a carriage return turned into a
/// carriage-return line-feed pair, for <c>--crlf</c>
/// (<see cref="Abstractions.ITransferContext.ConvertLineEndings" />).
/// </summary>
/// <remarks>
/// curl 8.21.0 converts a <c>-T</c> upload, a <c>-F</c> body and a <c>-d</c> body this way and,
/// since the converted length is not known before it is sent, sends it chunked (measured,
/// AF-0125, BL-1875 Notes). So this stream cannot seek and has no length. The byte before is
/// remembered between reads, as in the file handler's converter: <c>a\r\nb</c> passes
/// unchanged. Disposing it leaves <paramref name="source" /> open.
/// </remarks>
/// <param name="source">The body as given.</param>
internal sealed class HttpCrlfUploadStream(Stream source) : Stream
{
    private const byte CarriageReturn = (byte)'\r';

    private const byte LineFeed = (byte)'\n';

    private bool previousWasCarriageReturn;

    /// <summary>
    /// <see langword="true" /> when the last read had room for a carriage return but not for
    /// the line feed after it, which the next read gives first.
    /// </summary>
    private bool lineFeedPending;

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
        if (TryGivePending(buffer, out int given))
        {
            return given;
        }

        byte[] raw = new byte[RawSizeFor(buffer.Length)];
        return Convert(raw.AsSpan(0, source.Read(raw)), buffer);
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (TryGivePending(buffer.Span, out int given))
        {
            return given;
        }

        byte[] raw = new byte[RawSizeFor(buffer.Length)];
        int read = await source.ReadAsync(raw, cancellationToken).ConfigureAwait(false);
        return Convert(raw.AsSpan(0, read), buffer.Span);
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

    /// <summary>
    /// Gives how many source bytes to read for a <paramref name="bufferLength" />-byte read:
    /// half, so that a source of nothing but line feeds still fits once doubled, and at least
    /// one.
    /// </summary>
    private static int RawSizeFor(int bufferLength) => Math.Max(1, bufferLength / 2);

    /// <summary>
    /// Gives the line feed held back by the last read, or nothing for an empty
    /// <paramref name="buffer" />.
    /// </summary>
    /// <returns><see langword="true" /> when the read is answered without reading the source.</returns>
    private bool TryGivePending(Span<byte> buffer, out int given)
    {
        given = 0;
        if (buffer.IsEmpty)
        {
            return true;
        }

        if (!lineFeedPending)
        {
            return false;
        }

        buffer[0] = LineFeed;
        lineFeedPending = false;
        previousWasCarriageReturn = false;
        given = 1;
        return true;
    }

    /// <summary>
    /// Converts <paramref name="raw" /> into <paramref name="buffer" />, which holds at least
    /// twice its bytes or, for a one-byte buffer, one byte whose line feed is held back.
    /// </summary>
    /// <returns>The number of bytes written.</returns>
    private int Convert(ReadOnlySpan<byte> raw, Span<byte> buffer)
    {
        int length = 0;
        foreach (byte value in raw)
        {
            if (value == LineFeed && !previousWasCarriageReturn)
            {
                buffer[length++] = CarriageReturn;
                if (length == buffer.Length)
                {
                    lineFeedPending = true;
                    return length;
                }
            }

            buffer[length++] = value;
            previousWasCarriageReturn = value == CarriageReturn;
        }

        return length;
    }
}
