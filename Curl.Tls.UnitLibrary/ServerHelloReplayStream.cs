using System.Runtime.InteropServices;

namespace Curl.Tls;

/// <summary>
/// Wraps the transport while <see cref="TlsClientConnection" /> reads the server's first
/// records to see which version it chose: every byte read is kept until
/// <see cref="Replay" />, after which reads return the kept bytes again before reading the
/// transport, so the TLS 1.3 or TLS 1.2 record layer reads the ServerHello from its first
/// byte. Writes and flushes go straight to the transport, and disposing it disposes the
/// transport.
/// </summary>
internal sealed class ServerHelloReplayStream(Stream transport) : Stream
{
    private readonly List<byte> kept = [];
    private bool replaying;
    private int replayed;

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => transport.CanWrite;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>Stops keeping what is read; the next reads return what was kept, then the transport's bytes.</summary>
    public void Replay() => replaying = true;

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        if (TryReplay(buffer) is { } length)
        {
            return length;
        }

        int read = transport.Read(buffer);
        Keep(buffer[..read]);
        return read;
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (TryReplay(buffer.Span) is { } length)
        {
            return length;
        }

        int read = await transport.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        Keep(buffer.Span[..read]);
        return read;
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => transport.Write(buffer, offset, count);

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        transport.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        transport.WriteAsync(buffer, offset, count, cancellationToken);

    /// <inheritdoc />
    public override void Flush() => transport.Flush();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => transport.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            transport.Dispose();
        }

        base.Dispose(disposing);
    }

    // Copies kept bytes not yet replayed; null once there are none, or before Replay.
    private int? TryReplay(Span<byte> buffer)
    {
        if (!replaying || replayed == kept.Count)
        {
            return null;
        }

        int length = Math.Min(buffer.Length, kept.Count - replayed);
        CollectionsMarshal.AsSpan(kept).Slice(replayed, length).CopyTo(buffer);
        replayed += length;
        return length;
    }

    private void Keep(ReadOnlySpan<byte> read)
    {
        if (!replaying)
        {
            kept.AddRange(read);
        }
    }
}
