namespace Curl.Console;

/// <summary>
/// Writes a standard handle's bytes at the handle's own, shared file offset, as curl's
/// <c>fwrite</c> to <c>stdout</c> or <c>stderr</c> does, so a file the shell shares
/// (<c>{ echo a; curl ...; echo b; } &gt; f 2&gt;&amp;1</c>) gets each writer's bytes in
/// the order they were written (BL-3937, ADR-0945).
/// </summary>
/// <remarks>
/// A <see cref="FileStream" /> over a seekable handle keeps a position of its own and writes
/// there (<c>pwrite</c>, or <c>WriteFile</c> with an offset), so standard output and standard
/// error opened over one file overwrite each other, and the shell's later writes land at an
/// offset the stream never moved. Each write here goes through
/// <paramref name="writeAtSharedOffset" /> until every byte is written; a write that moves no
/// byte throws an <see cref="IOException" />, as a failed one does.
/// </remarks>
/// <param name="handleValue">The handle, or the file descriptor on Linux and macOS, which the stream does not own.</param>
/// <param name="writeAtSharedOffset">
/// Writes bytes at the handle's shared offset and returns how many it wrote; throws an
/// <see cref="IOException" /> when the write fails.
/// </param>
internal sealed class SharedOffsetHandleStream(nint handleValue, SharedOffsetHandleStream.WriteAtSharedOffset writeAtSharedOffset) : Stream
{
    /// <summary>The message of the <see cref="IOException" /> a write that moves no byte throws.</summary>
    internal const string NothingWrittenMessage = "The write to the standard handle moved no byte.";

    /// <summary>
    /// Writes <paramref name="bytes" />, or a leading part of them, at the shared offset of
    /// <paramref name="handleValue" />.
    /// </summary>
    /// <param name="handleValue">The handle or file descriptor.</param>
    /// <param name="bytes">The bytes to write.</param>
    /// <returns>How many bytes were written.</returns>
    internal delegate int WriteAtSharedOffset(nint handleValue, ReadOnlySpan<byte> bytes);

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
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        while (!buffer.IsEmpty)
        {
            int written = writeAtSharedOffset(handleValue, buffer);
            if (written <= 0)
            {
                throw new IOException(NothingWrittenMessage);
            }

            buffer = buffer[written..];
        }
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }
}
