namespace Curl.Core.Multipart;

/// <summary>
/// A read-only, forward-only stream that reads each of its segments to the end in turn,
/// and disposes them all when it is disposed.
/// </summary>
/// <remarks>
/// This is how a multipart body streams its file parts instead of reading them into
/// memory: the literal header and boundary bytes are memory segments, and each file is
/// the stream <see cref="Curl.Protocol.Abstractions.IFileSystem" /> opened for it.
/// </remarks>
internal sealed class ConcatenatedReadStream : Stream
{
    private readonly IReadOnlyList<Stream> segments;

    private int current;

    /// <summary>Initialises a stream over <paramref name="segments" />, which it now owns.</summary>
    /// <param name="segments">The streams to read, in order.</param>
    internal ConcatenatedReadStream(IReadOnlyList<Stream> segments) => this.segments = segments;

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        while (buffer.Length > 0 && current < segments.Count)
        {
            int read = segments[current].Read(buffer);
            if (read > 0)
            {
                return read;
            }

            current++;
        }

        return 0;
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        while (buffer.Length > 0 && current < segments.Count)
        {
            int read = await segments[current].ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read > 0)
            {
                return read;
            }

            current++;
        }

        return 0;
    }

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

    /// <inheritdoc />
    /// <remarks>
    /// The segments are disposed whatever <paramref name="disposing" /> says: this sealed type
    /// declares no finalizer, so only <see cref="Stream.Dispose()" /> ever calls this.
    /// </remarks>
    protected override void Dispose(bool disposing)
    {
        foreach (Stream segment in segments)
        {
            segment.Dispose();
        }

        base.Dispose(disposing);
    }
}
