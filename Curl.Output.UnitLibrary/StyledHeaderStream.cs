using System.Buffers;

namespace Curl.Output;

/// <summary>
/// The header output <c>-i</c> and <c>-I</c> write to a terminal under
/// <c>--styled-output</c>: each header line written is styled by a
/// <see cref="StyledHeaderLines" /> on its way to the terminal (ADR-0246, BL-736).
/// </summary>
/// <param name="output">Where the styled lines go: standard output. It belongs to the caller and is not closed here.</param>
/// <param name="styles">The styles, which follow the <c>Location</c> values they link.</param>
/// <remarks>
/// Lines are styled whole, as curl styles each header line it is handed, however the
/// writes split them (BL-1658): a write is split after each line feed, and the bytes after
/// the last line feed are held until a later write finishes their line. A held line with
/// no line feed is styled and written by <see cref="Flush" /> or by disposing the stream.
/// </remarks>
public sealed class StyledHeaderStream(Stream output, StyledHeaderLines styles) : Stream
{
    private readonly ArrayBufferWriter<byte> unfinishedLine = new();

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

    /// <summary>Styles and writes any held line with no line feed, then flushes the output.</summary>
    public override void Flush()
    {
        if (unfinishedLine.WrittenCount > 0)
        {
            output.Write(StyleHeldLine());
        }

        output.Flush();
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ReadOnlySpan<byte> rest = buffer;
        int lineFeed;
        while ((lineFeed = rest.IndexOf((byte)'\n')) >= 0)
        {
            output.Write(StyleFinishedLine(rest[..(lineFeed + 1)]));
            rest = rest[(lineFeed + 1)..];
        }

        unfinishedLine.Write(rest);
    }

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ReadOnlyMemory<byte> rest = buffer;
        int lineFeed;
        while ((lineFeed = rest.Span.IndexOf((byte)'\n')) >= 0)
        {
            await output.WriteAsync(StyleFinishedLine(rest.Span[..(lineFeed + 1)]), cancellationToken).ConfigureAwait(false);
            rest = rest[(lineFeed + 1)..];
        }

        unfinishedLine.Write(rest.Span);
    }

    /// <summary>Styles and writes any held line with no line feed when the stream is disposed.</summary>
    /// <param name="disposing">Always <see langword="true" />: this sealed stream has no finalizer to pass <see langword="false" />.</param>
    protected override void Dispose(bool disposing)
    {
        Flush();

        base.Dispose(disposing);
    }

    /// <summary>
    /// Styles the line that <paramref name="lineEnd" /> finishes: the held bytes followed by
    /// <paramref name="lineEnd" />, which ends in a line feed.
    /// </summary>
    private byte[] StyleFinishedLine(ReadOnlySpan<byte> lineEnd)
    {
        if (unfinishedLine.WrittenCount == 0)
        {
            return styles.Style(lineEnd);
        }

        unfinishedLine.Write(lineEnd);
        return StyleHeldLine();
    }

    /// <summary>Styles the held bytes as one line and stops holding them.</summary>
    private byte[] StyleHeldLine()
    {
        byte[] styled = styles.Style(unfinishedLine.WrittenSpan);
        unfinishedLine.Clear();
        return styled;
    }
}
