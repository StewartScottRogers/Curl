namespace Curl.Console;

/// <summary>
/// The stream standard output is when the process was started with it closed: every write
/// throws an <see cref="IOException" />, as a write to a closed handle or descriptor does.
/// </summary>
/// <remarks>
/// .NET hands back <see cref="Stream.Null" /> for a closed standard output, which accepts
/// every write, so curl's exit 23 would never come out. This stream stands in for it so
/// <see cref="StandardOutputFailureDeferringStream" /> sees the failure. A flush has nothing
/// to write and succeeds.
/// </remarks>
internal sealed class ClosedStandardOutputStream : Stream
{
    /// <summary>
    /// The message of the <see cref="IOException" /> every write throws.
    /// </summary>
    internal const string ClosedMessage = "Standard output is closed.";

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
    /// <exception cref="IOException">Always: standard output is closed.</exception>
    public override void Write(byte[] buffer, int offset, int count) => throw new IOException(ClosedMessage);

    /// <inheritdoc />
    /// <exception cref="IOException">Always: standard output is closed.</exception>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        throw new IOException(ClosedMessage);
}
