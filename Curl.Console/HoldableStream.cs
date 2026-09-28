using System.Runtime.InteropServices;

namespace Curl.Console;

/// <summary>
/// A write-only stream that writes to the stream it wraps, except while it is held: from
/// <see cref="Hold" /> to <see cref="Release" /> it keeps what is written, and the release
/// writes it all, in order.
/// </summary>
/// <param name="inner">The stream written to.</param>
/// <remarks>
/// <see cref="CurlCommandRunner" /> writes <c>-v</c> and trace lines to standard error through
/// this stream, and holds it from the handler's "transfer done" report until it has written the
/// progress meter's done status lines, so the connection-end <c>-v</c> line the handler reports in
/// between comes after the meter, as curl 8.21.0 writes it (task BL-411, ADR-0116).
/// </remarks>
internal sealed class HoldableStream(Stream inner) : Stream
{
    private readonly List<byte> held = [];

    private bool isHeld;

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

    /// <summary>
    /// Keeps every write from now on until <see cref="Release" />.
    /// </summary>
    internal void Hold() => isHeld = true;

    /// <summary>
    /// Writes what was kept since <see cref="Hold" /> to the wrapped stream, flushes it, and
    /// passes writes straight through again; does nothing when the stream is not held.
    /// </summary>
    internal void Release()
    {
        if (!isHeld)
        {
            return;
        }

        isHeld = false;
        inner.Write(CollectionsMarshal.AsSpan(held));
        held.Clear();
        inner.Flush();
    }

    /// <inheritdoc />
    /// <remarks>While held, a flush waits for <see cref="Release" />, which flushes.</remarks>
    public override void Flush()
    {
        if (!isHeld)
        {
            inner.Flush();
        }
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
        if (isHeld)
        {
            held.AddRange(buffer.AsSpan(offset, count));
        }
        else
        {
            inner.Write(buffer, offset, count);
        }
    }
}
