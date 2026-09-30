using System.Text;

namespace Curl.Console;

/// <summary>
/// A write-only standard output that fails with an <see cref="IOException" />, as a closed one does,
/// for each write of exactly <paramref name="refusedText" />, and passes every other write to
/// <paramref name="inner" />; so that under <c>-Z</c> standard output fails for one transfer's write alone.
/// </summary>
/// <param name="inner">Where the writes not refused go.</param>
/// <param name="refusedText">The text, read as Latin-1, whose write fails.</param>
internal sealed class TextRefusingStream(Stream inner, string refusedText) : Stream
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
    public override void Flush() => inner.Flush();

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        if (Encoding.Latin1.GetString(buffer, offset, count) == refusedText)
        {
            throw new IOException("Standard output refused the write.");
        }

        inner.Write(buffer, offset, count);
    }

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Write(buffer.ToArray(), 0, buffer.Length);
        return ValueTask.CompletedTask;
    }
}
