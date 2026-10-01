namespace Curl.Console;

/// <summary>
/// Writes to standard output as curl's Windows build does across a run: in text mode, each line
/// feed as CR LF, until a transfer switches standard output to binary mode, then every byte as
/// it is.
/// </summary>
/// <param name="inner">Standard output, not owned.</param>
/// <param name="isBinary">Tells, at each write, whether standard output has been switched to binary mode.</param>
/// <remarks>
/// curl switches standard output to binary mode when a transfer that sends its body there
/// starts, so a <c>--trace-ascii -</c> dump of an SMTP upload with no <c>-o</c> ends its lines
/// with a bare LF from the first line, while one with <c>-o</c> ends them CR LF (measured on curl
/// 8.21.0, BL-546 and BL-242 Notes). A body sent to standard output under <c>-B</c> is written
/// through this stream too, as curl leaves standard output in text mode for it (BL-961 Notes).
/// </remarks>
internal sealed class TextModeUntilBinaryStream(Stream inner, Func<bool> isBinary) : Stream
{
    private readonly LineFeedToCrLfStream textMode = new(inner);

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
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) =>
        (isBinary() ? inner : textMode).Write(buffer, offset, count);

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        (isBinary() ? inner : textMode).WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
}
