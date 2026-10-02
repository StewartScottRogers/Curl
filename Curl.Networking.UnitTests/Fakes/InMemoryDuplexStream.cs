namespace Curl.Networking.Fakes;

/// <summary>
/// One end of an in-memory, bidirectional byte pipe: what one end writes the other
/// reads. Lets a test run a client and a server <see cref="System.Net.Security.SslStream" />
/// against each other without a socket. Disposing an end tells the other end's reads
/// that the peer has closed.
/// </summary>
public sealed class InMemoryDuplexStream : Stream
{
    private readonly CancelSafeChunkQueue _incoming;
    private readonly CancelSafeChunkQueue _outgoing;
    private ReadOnlyMemory<byte> _unread;

    private InMemoryDuplexStream(CancelSafeChunkQueue incoming, CancelSafeChunkQueue outgoing)
    {
        _incoming = incoming;
        _outgoing = outgoing;
    }

    /// <summary>Gets a value indicating whether this end has been disposed.</summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>Creates the two connected ends of a pipe.</summary>
    /// <returns>The client end and the server end.</returns>
    public static (InMemoryDuplexStream Client, InMemoryDuplexStream Server) CreatePair()
    {
        var clientToServer = new CancelSafeChunkQueue();
        var serverToClient = new CancelSafeChunkQueue();

        return (new InMemoryDuplexStream(serverToClient, clientToServer),
                new InMemoryDuplexStream(clientToServer, serverToClient));
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (_unread.IsEmpty)
        {
            if (await _incoming.DequeueAsync(cancellationToken).ConfigureAwait(false) is not { } chunk)
            {
                return 0;
            }

            _unread = chunk;
        }

        var count = Math.Min(buffer.Length, _unread.Length);
        _unread[..count].CopyTo(buffer);
        _unread = _unread[count..];
        return count;
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        _outgoing.TryEnqueue(buffer.ToArray());
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        _outgoing.Complete();
        base.Dispose(disposing);
    }
}
