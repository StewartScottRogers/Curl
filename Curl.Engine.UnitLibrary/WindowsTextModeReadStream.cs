namespace Curl.Engine;

/// <summary>
/// Reads <paramref name="inner" /> as the C runtime's text mode does, as curl's Windows build reads
/// the standard input a <c>telnet</c> transfer without <c>-T</c> sends: a Ctrl-Z byte (0x1A) ends the
/// input and each CR LF pair reads as LF, a CR before any other byte staying. Measured with the local
/// curl 8.22.0 on 2026-10-26 (CGR-112, BL-3641): standard input <c>a</c> CR LF <c>b</c> CR LF Ctrl-Z
/// CR LF <c>c</c> LF sends <c>a</c> LF <c>b</c> LF, while <c>-T -</c> sends every byte unchanged.
/// </summary>
/// <param name="inner">The raw standard input.</param>
internal sealed class WindowsTextModeReadStream(Stream inner) : Stream
{
    private const byte EndOfInput = 0x1A;

    private const int RawReadSize = 4096;

    private readonly byte[] raw = new byte[RawReadSize];

    /// <summary>The text-mode bytes of the last raw read, one more than it for a CR held from the read before.</summary>
    private readonly byte[] decoded = new byte[RawReadSize + 1];

    private int decodedStart;

    private int decodedEnd;

    private bool ended;

    private bool carriageReturnHeld;

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
    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <summary>
    /// Hands over text-mode bytes already decoded, else reads raw bytes until at least one text-mode
    /// byte comes of them or the input ends; a CR at the end of a raw read is held until the next byte
    /// shows whether an LF follows it.
    /// </summary>
    /// <param name="buffer">Where the text-mode bytes go.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>How many bytes were written to <paramref name="buffer" />; 0 at the end of the input.</returns>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        while (decodedStart == decodedEnd && !ended && !buffer.IsEmpty)
        {
            int read = await inner.ReadAsync(raw, cancellationToken).ConfigureAwait(false);
            decodedStart = 0;
            decodedEnd = read == 0 ? EndInput(decoded) : Translate(raw.AsSpan(0, read), decoded);
        }

        int count = Math.Min(buffer.Length, decodedEnd - decodedStart);
        decoded.AsSpan(decodedStart, count).CopyTo(buffer.Span);
        decodedStart += count;
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

    private int EndInput(Span<byte> output)
    {
        ended = true;
        return ReleaseHeldCarriageReturn(output, 0);
    }

    private int Translate(ReadOnlySpan<byte> raw, Span<byte> output)
    {
        int written = 0;
        foreach (byte octet in raw)
        {
            if (octet == EndOfInput)
            {
                return written + EndInput(output[written..]);
            }

            written = octet == (byte)'\n' ? DropHeldCarriageReturn(written) : ReleaseHeldCarriageReturn(output, written);
            carriageReturnHeld = octet == (byte)'\r';
            if (!carriageReturnHeld)
            {
                output[written++] = octet;
            }
        }

        return written;
    }

    private int DropHeldCarriageReturn(int written)
    {
        carriageReturnHeld = false;
        return written;
    }

    private int ReleaseHeldCarriageReturn(Span<byte> output, int written)
    {
        if (!carriageReturnHeld)
        {
            return written;
        }

        carriageReturnHeld = false;
        output[written] = (byte)'\r';
        return written + 1;
    }
}
