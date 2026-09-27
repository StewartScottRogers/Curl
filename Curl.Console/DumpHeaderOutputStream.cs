namespace Curl.Console;

/// <summary>
/// The stream a transfer's <c>-D</c> header lines are written through: every write is passed
/// to <paramref name="destination" /> and flushed at once, as curl 8.21.0's header callback
/// flushes after each header, and a failed write or flush is recorded in
/// <see cref="HasWriteFailed" /> before its <see cref="IOException" /> is rethrown to the handler.
/// </summary>
/// <param name="destination">The <c>-D</c> destination: standard output or the named file.</param>
/// <remarks>
/// curl prints <c>curl: Failed writing headers to &lt;file&gt;</c> when that flush fails
/// (measured 2026-09-26, BL-111 Notes); the runner prints it from <see cref="HasWriteFailed" />.
/// The stream does not own <paramref name="destination" /> and never disposes it.
/// </remarks>
internal sealed class DumpHeaderOutputStream(Stream destination) : Stream
{
    /// <summary>Gets a value indicating whether a header write or flush has failed.</summary>
    internal bool HasWriteFailed { get; private set; }

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
    /// <exception cref="IOException">The destination failed the write or its flush.</exception>
    public override void Write(byte[] buffer, int offset, int count)
    {
        try
        {
            destination.Write(buffer, offset, count);
            destination.Flush();
        }
        catch (IOException)
        {
            HasWriteFailed = true;
            throw;
        }
    }

    /// <inheritdoc />
    /// <exception cref="IOException">The destination failed the write or its flush.</exception>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        try
        {
            await destination.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            HasWriteFailed = true;
            throw;
        }
    }
}
