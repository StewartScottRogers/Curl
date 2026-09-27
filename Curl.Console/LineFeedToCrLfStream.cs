namespace Curl.Console;

/// <summary>
/// A write-only stream that writes each line feed as CR LF to the stream it wraps, as a C
/// stream in text mode does on Windows. Every other byte, a carriage return included, passes
/// through unchanged, so <c>\r\n</c> becomes <c>\r\r\n</c> as it does in curl.
/// </summary>
/// <param name="inner">The stream written to.</param>
/// <param name="ownsInner">
/// Whether disposing this stream disposes <paramref name="inner" />; standard output and
/// standard error are not owned, a <c>%output{file}</c> file is (<see cref="DiskWriteOutFileOpener" />).
/// </param>
/// <remarks>
/// curl 8.21.0 on Windows writes standard error, and standard output until a transfer sends
/// its body there, in text mode. <see cref="CurlCommandRunner" /> renders <c>-w</c> through
/// this stream for those targets; measured on 2026-09-26 (BL-235 Notes).
/// </remarks>
internal sealed class LineFeedToCrLfStream(Stream inner, bool ownsInner = false) : Stream
{
    private static readonly byte[] CrLf = [(byte)'\r', (byte)'\n'];

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
    public override void Write(byte[] buffer, int offset, int count) =>
        inner.Write(Translate(buffer.AsSpan(offset, count)));

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        inner.WriteAsync(Translate(buffer.Span), cancellationToken);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (ownsInner)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Copies <paramref name="bytes" /> with every line feed written as CR LF.</summary>
    /// <param name="bytes">The bytes to write.</param>
    /// <returns>The translated bytes.</returns>
    private static byte[] Translate(ReadOnlySpan<byte> bytes)
    {
        using MemoryStream translated = new(bytes.Length);
        foreach (byte value in bytes)
        {
            if (value == (byte)'\n')
            {
                translated.Write(CrLf);
            }
            else
            {
                translated.WriteByte(value);
            }
        }

        return translated.ToArray();
    }
}
