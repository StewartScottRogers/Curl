namespace Curl.Output;

/// <summary>
/// The header output <c>-i</c> and <c>-I</c> write to a terminal under
/// <c>--styled-output</c>: each header line written is styled by a
/// <see cref="StyledHeaderLines" /> on its way to the terminal (ADR-0246, BL-736).
/// </summary>
/// <param name="output">Where the styled lines go: standard output. It belongs to the caller and is not closed here.</param>
/// <param name="styles">The styles, which follow the <c>Location</c> values they link.</param>
/// <remarks>
/// A write is split after each line feed, whatever the handler wrote at once, as curl styles
/// each header line it is handed.
/// </remarks>
public sealed class StyledHeaderStream(Stream output, StyledHeaderLines styles) : Stream
{
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
    public override void Flush() => output.Flush();

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        ReadOnlySpan<byte> rest = buffer.AsSpan(offset, count);
        while (!rest.IsEmpty)
        {
            ReadOnlySpan<byte> line = rest[..LengthOfFirstLine(rest)];
            output.Write(styles.Style(line));
            rest = rest[line.Length..];
        }
    }

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ReadOnlyMemory<byte> rest = buffer;
        while (!rest.IsEmpty)
        {
            ReadOnlyMemory<byte> line = rest[..LengthOfFirstLine(rest.Span)];
            await output.WriteAsync(styles.Style(line.Span), cancellationToken).ConfigureAwait(false);
            rest = rest[line.Length..];
        }
    }

    /// <summary>
    /// Measures the first line of <paramref name="bytes" />: up to and including its first
    /// line feed, or all of it when there is none.
    /// </summary>
    private static int LengthOfFirstLine(ReadOnlySpan<byte> bytes)
    {
        int lineFeed = bytes.IndexOf((byte)'\n');

        return lineFeed < 0 ? bytes.Length : lineFeed + 1;
    }
}
