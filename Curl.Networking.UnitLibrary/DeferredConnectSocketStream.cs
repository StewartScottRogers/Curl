using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// A <see cref="Stream" /> over a TCP <see cref="Socket" /> whose connect the kernel finishes on the first
/// write: the socket <see cref="DarwinFastOpenConnect" /> answers after <c>connectx</c> with
/// <c>CONNECT_RESUME_ON_READ_WRITE</c>, which has sent no SYN yet and so reports itself not connected
/// (BL-1158). <see cref="NetworkStream" /> refuses such a socket; this stream reads and writes it as it is,
/// and owns and disposes it.
/// </summary>
/// <param name="socket">The stream socket, connected or with its connect deferred to the first write.</param>
internal sealed class DeferredConnectSocketStream(Socket socket) : Stream
{
    private readonly Socket _socket = socket;

    /// <summary>Gets <see langword="true" />.</summary>
    public override bool CanRead => true;

    /// <summary>Gets <see langword="false" />: a socket has no position.</summary>
    public override bool CanSeek => false;

    /// <summary>Gets <see langword="true" />.</summary>
    public override bool CanWrite => true;

    /// <summary>Throws <see cref="NotSupportedException" />: a socket has no length.</summary>
    public override long Length => throw new NotSupportedException();

    /// <summary>Throws <see cref="NotSupportedException" />: a socket has no position.</summary>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>Does nothing: every write goes straight to the socket.</summary>
    public override void Flush()
    {
    }

    /// <summary>Completes at once: every write goes straight to the socket.</summary>
    /// <param name="cancellationToken">Not used.</param>
    /// <returns>A completed task.</returns>
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) =>
        _socket.Receive(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        _socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        var remaining = buffer.AsSpan(offset, count);
        while (!remaining.IsEmpty)
        {
            remaining = remaining[_socket.Send(remaining)..];
        }
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var remaining = buffer;
        while (!remaining.IsEmpty)
        {
            remaining = remaining[await _socket.SendAsync(remaining, SocketFlags.None, cancellationToken).ConfigureAwait(false)..];
        }
    }

    /// <summary>Throws <see cref="NotSupportedException" />: a socket cannot seek.</summary>
    /// <param name="offset">Not used.</param>
    /// <param name="origin">Not used.</param>
    /// <returns>Never returns.</returns>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <summary>Throws <see cref="NotSupportedException" />: a socket has no length.</summary>
    /// <param name="value">Not used.</param>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <summary>Disposes the socket.</summary>
    /// <remarks>The stream has no finalizer, so <paramref name="disposing" /> is always <see langword="true" />.</remarks>
    /// <param name="disposing"><see langword="true" /> when called from <see cref="Stream.Dispose()" />.</param>
    protected override void Dispose(bool disposing)
    {
        _socket.Dispose();
        base.Dispose(disposing);
    }
}
