using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Presents one <see cref="IMultiplexedStream" /> as a <see cref="Stream" />, the form
/// <c>Curl.Http3</c>'s frame reader and stream openers take: reads pass through, writes go out
/// without ending the stream, and the stream cannot seek. Only the asynchronous members work;
/// the synchronous ones throw <see cref="NotSupportedException" />.
/// </summary>
/// <param name="stream">The QUIC stream; the adapter does not dispose it.</param>
internal sealed class MultiplexedStreamAdapter(IMultiplexedStream stream) : Stream
{
    /// <inheritdoc />
    public override bool CanRead => true;

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
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        stream.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        stream.WriteAsync(buffer, endStream: false, cancellationToken);

    /// <summary>
    /// Does nothing: a QUIC stream sends what it is given without buffering here.
    /// </summary>
    /// <param name="cancellationToken">Not used.</param>
    /// <returns>A completed task.</returns>
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
