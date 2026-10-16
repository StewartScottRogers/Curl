namespace Curl.Conformance;

/// <summary>
/// Gives back bytes already read from a stream: reads return <paramref name="prefix"/> first and
/// then whatever <paramref name="inner"/> reads, and writes go straight to <paramref name="inner"/>.
/// <see cref="TlsServerStream.AcceptAsync"/> reads the client's first record to choose a TLS
/// server, then hands it back this way to the <see cref="System.Net.Security.SslStream"/> it chose.
/// Like <see cref="InMemoryDuplexStream"/>, it is asynchronous only.
/// </summary>
/// <param name="prefix">The bytes read before.</param>
/// <param name="inner">The stream they were read from; disposed with this one.</param>
internal sealed class ReplayedPrefixStream(byte[] prefix, Stream inner) : Stream
{
    private int prefixOffset;

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (prefixOffset == prefix.Length)
        {
            return inner.ReadAsync(buffer, cancellationToken);
        }

        int count = Math.Min(buffer.Length, prefix.Length - prefixOffset);
        prefix.AsMemory(prefixOffset, count).CopyTo(buffer);
        prefixOffset += count;
        return ValueTask.FromResult(count);
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        inner.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        inner.WriteAsync(buffer, offset, count, cancellationToken);

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    /// <inheritdoc/>
    public override void Flush() => inner.Flush();

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
