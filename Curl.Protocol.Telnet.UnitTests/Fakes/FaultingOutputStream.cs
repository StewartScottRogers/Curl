using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Telnet.Fakes;

/// <summary>
/// An output whose every write fails, as a closed standard output does: with a plain
/// <see cref="IOException" />, or, given <paramref name="bytesAccepted" />, with an
/// <see cref="OutputWriteFailedException" /> reporting that many bytes accepted.
/// </summary>
/// <param name="bytesAccepted">
/// The <see cref="OutputWriteFailedException.BytesAccepted" /> each write fails with, or
/// <see langword="null" /> for a plain <see cref="IOException" />.
/// </param>
public sealed class FaultingOutputStream(int? bytesAccepted = null) : Stream
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
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        throw (bytesAccepted is { } accepted
            ? new OutputWriteFailedException(accepted, "The output is closed.")
            : new IOException("The output is closed."));

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
