namespace Curl.Console;

/// <summary>
/// The write-only stream a transfer to standard output writes through. It passes every
/// write through, and when standard output fails - closed, or its reader gone - it defers
/// the failure the way curl 8.21.0's 4096-byte stdio buffer does.
/// </summary>
/// <param name="inner">The raw standard output stream.</param>
/// <remarks>
/// <para>
/// curl writes standard output through a C <c>FILE</c> with a <see cref="StdioBufferSize" />
/// buffer, so a failure only surfaces when the buffer is written out. Measured on
/// 2026-09-26 with a <c>file</c> transfer to a closed standard output: a body of up to
/// 4095 bytes sits in the buffer, the transfer succeeds, and the flush at its end fails,
/// which curl reports as <c>curl: Failed writing body</c>; a body of 4096 bytes or more
/// fails on the write itself, which curl reports as
/// <c>curl: (23) Failure writing output to destination, passed N returned 0</c>.
/// </para>
/// <para>
/// So after the first <see cref="IOException" /> from <paramref name="inner" /> this stream
/// sets <see cref="HasWriteFailed" /> and discards writes without throwing until
/// <see cref="StdioBufferSize" /> bytes have been offered since the failure, counting the
/// write that failed; from then on every write throws an <see cref="IOException" />, so the
/// handler stops and reports its own write failure. A flush never throws: its failure also
/// sets <see cref="HasWriteFailed" />. The stream does not own <paramref name="inner" /> and
/// never disposes it.
/// </para>
/// </remarks>
internal sealed class StandardOutputFailureDeferringStream(Stream inner) : Stream
{
    /// <summary>
    /// The size of curl 8.21.0's standard-output stdio buffer on Windows, measured as the
    /// smallest body whose write to a closed standard output fails at once.
    /// </summary>
    internal const int StdioBufferSize = 4096;

    private long bytesOfferedSinceFailure;

    /// <summary>
    /// Gets a value indicating whether a write or flush to standard output has failed since
    /// the stream was created or <see cref="ClearWriteFailure" /> was last called.
    /// </summary>
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

    /// <summary>
    /// Forgets any write failure, so the next transfer starts with an empty buffer, as curl's
    /// does after the flush that ends each transfer.
    /// </summary>
    internal void ClearWriteFailure()
    {
        HasWriteFailed = false;
        bytesOfferedSinceFailure = 0;
    }

    /// <inheritdoc />
    public override void Flush()
    {
        try
        {
            inner.Flush();
        }
        catch (IOException)
        {
            HasWriteFailed = true;
        }
    }

    /// <inheritdoc />
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        try
        {
            await inner.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            HasWriteFailed = true;
        }
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    /// <exception cref="IOException">
    /// Standard output has failed and <see cref="StdioBufferSize" /> bytes have been offered
    /// since.
    /// </exception>
    public override void Write(byte[] buffer, int offset, int count)
    {
        if (!HasWriteFailed)
        {
            try
            {
                inner.Write(buffer, offset, count);

                return;
            }
            catch (IOException)
            {
                HasWriteFailed = true;
            }
        }

        AbsorbIntoStdioBuffer(count);
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    /// <exception cref="IOException">
    /// Standard output has failed and <see cref="StdioBufferSize" /> bytes have been offered
    /// since.
    /// </exception>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!HasWriteFailed)
        {
            try
            {
                await inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);

                return;
            }
            catch (IOException)
            {
                HasWriteFailed = true;
            }
        }

        AbsorbIntoStdioBuffer(buffer.Length);
    }

    /// <summary>
    /// Discards a write made after standard output failed, as long as curl's stdio buffer
    /// would still have room for it.
    /// </summary>
    /// <param name="count">The size of the write.</param>
    /// <exception cref="IOException">The buffer would be full, so curl's write would fail.</exception>
    private void AbsorbIntoStdioBuffer(int count)
    {
        bytesOfferedSinceFailure += count;

        if (bytesOfferedSinceFailure >= StdioBufferSize)
        {
            throw new IOException("Standard output is closed or its reader has gone.");
        }
    }
}
