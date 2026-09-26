namespace Curl.Console;

/// <summary>
/// The header output of a transfer whose header lines go both to the <c>-D</c> output and,
/// for <c>-i</c> or <c>-I</c>, to the body output: each line written is written to
/// <paramref name="dumpHeaderOutput" /> and then to <paramref name="bodyOutput" />, one line
/// at a time.
/// </summary>
/// <param name="dumpHeaderOutput">Where <c>-D</c> sends the header lines.</param>
/// <param name="bodyOutput">The transfer's body output: standard output or the <c>-o</c> file.</param>
/// <remarks>
/// curl 8.21.0 hands each header line to both outputs before the next, so <c>-i -D -</c>
/// writes every line twice in a row on standard output (measured 2026-09-26, BL-232 Notes).
/// A write is therefore split after each line feed, whatever the handler wrote at once. The
/// two streams belong to the caller and are neither flushed nor closed here.
/// </remarks>
internal sealed class HeaderLineTeeStream(Stream dumpHeaderOutput, Stream bodyOutput) : Stream
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
    public override void Flush()
    {
    }

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
            dumpHeaderOutput.Write(line);
            bodyOutput.Write(line);
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
            await dumpHeaderOutput.WriteAsync(line, cancellationToken).ConfigureAwait(false);
            await bodyOutput.WriteAsync(line, cancellationToken).ConfigureAwait(false);
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
