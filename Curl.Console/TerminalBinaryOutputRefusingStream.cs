namespace Curl.Console;

/// <summary>
/// Passes a body on to standard output while it is a terminal, but refuses a write holding a NUL byte
/// when fewer than <see cref="CheckedByteCount" /> bytes were written before it (the whole write is
/// searched), as curl 8.22.0's <c>tool_write_cb</c> (<c>src/tool_cb_wrt.c</c>) refuses binary output to
/// a terminal unless <c>--output -</c> was given. A refused write reaches nothing and throws an
/// <see cref="IOException" />.
/// </summary>
/// <param name="inner">The standard output the body goes to; disposing this stream disposes it.</param>
internal sealed class TerminalBinaryOutputRefusingStream(Stream inner) : Stream
{
    /// <summary>
    /// The count of bytes already written below which a write is checked for a NUL byte; curl checks no
    /// write once it has written this many.
    /// </summary>
    internal const int CheckedByteCount = 2000;

    /// <summary>
    /// The warning curl 8.22.0 writes, unwrapped, when it refuses binary output to a terminal. Real curl's
    /// release build cannot be shown a terminal through a redirected standard output, so the text is taken
    /// from <c>src/tool_cb_wrt.c</c> at tag <c>curl-8_22_0</c> (BL-2908).
    /// </summary>
    internal const string RefusalWarning =
        "Warning: Binary output can mess up your terminal. Use \"--output -\" to tell curl to output it to your terminal anyway, or consider \"--output <FILE>\" to save to a file.";

    /// <summary>The bytes passed on to standard output so far; a refused write adds none.</summary>
    private long bytesWritten;

    /// <summary>Gets a value indicating whether a write was refused for holding a NUL byte.</summary>
    internal bool HasRefused { get; private set; }

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
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        RefuseBinary(buffer);
        inner.Write(buffer);
        bytesWritten += buffer.Length;
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        RefuseBinary(buffer.Span);
        await inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        bytesWritten += buffer.Length;
    }

    /// <inheritdoc />
    /// <remarks>The stream has no finalizer, so this runs only from <see cref="Stream.Dispose()" />.</remarks>
    protected override void Dispose(bool disposing)
    {
        inner.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>
    /// Throws, and records the refusal, when <paramref name="buffer" /> holds a NUL byte and fewer than
    /// <see cref="CheckedByteCount" /> bytes were written before it.
    /// </summary>
    /// <param name="buffer">The bytes about to be written.</param>
    /// <exception cref="IOException">The write is refused.</exception>
    private void RefuseBinary(ReadOnlySpan<byte> buffer)
    {
        if (bytesWritten < CheckedByteCount && buffer.Contains((byte)0))
        {
            HasRefused = true;
            throw new IOException("Binary output to a terminal refused.");
        }
    }
}
