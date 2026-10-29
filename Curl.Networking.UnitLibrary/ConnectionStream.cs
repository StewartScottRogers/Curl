using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Presents an <see cref="IConnection" /> as the <see cref="Stream" /> an
/// <see cref="System.Net.Security.SslStream" /> runs over. Only the asynchronous members
/// carry bytes, because <see cref="IConnection" /> has no synchronous ones. Disposing the
/// stream leaves the connection open, its owner disposing it, unless the stream was made
/// its owner: then <see cref="DisposeAsync" /> disposes it too, as the KDC transport's
/// caller disposes only the stream it was given.
/// </summary>
/// <param name="connection">The plaintext connection to read from and write to.</param>
/// <param name="ownsConnection">Whether <see cref="DisposeAsync" /> disposes <paramref name="connection" />.</param>
internal sealed class ConnectionStream(IConnection connection, bool ownsConnection = false) : Stream
{
    /// <summary>Disposes the stream, and the connection when the stream owns it.</summary>
    /// <returns>A task that completes when both are disposed.</returns>
    public override async ValueTask DisposeAsync()
    {
        if (ownsConnection)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }

        await base.DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <summary>Not supported: a connection has no length.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override long Length => throw new NotSupportedException();

    /// <summary>Not supported: a connection has no position.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// Gets a value indicating whether a read into a non-empty buffer has returned 0: the
    /// connection underneath has ended. <see cref="SslStreamConnection" /> reads it to tell a
    /// bare end from the server's <c>close_notify</c>, which <c>SslStream</c> does not say.
    /// </summary>
    public bool TransportEnded { get; private set; }

    /// <summary>
    /// Gets or sets the detector every byte read is shown to, so the Schannel build can tell a
    /// TLS 1.3 session ticket arrived (BL-1089); <see langword="null" /> shows them to none.
    /// </summary>
    public SessionTicketRecordDetector? TicketRecords { get; set; }

    /// <summary>
    /// Gets or sets whether writes after the first read are held back until the next read, the
    /// first write once this is cleared, or <see cref="ReleaseHeldWritesAsync" />. The Schannel
    /// build uses it to keep a TLS 1.3 client's final flight off the wire until the server
    /// certificate was judged, so a refused certificate ends the handshake before it completes
    /// for the server, as curl's Schannel build does (CGR-228).
    /// </summary>
    public bool HoldsWritesAfterFirstRead { get; set; }

    private MemoryStream? _heldWrites;

    private bool _hasRead;

    /// <summary>Sends the writes held back, if any, and stops holding.</summary>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes when the held bytes are written.</returns>
    public async ValueTask ReleaseHeldWritesAsync(CancellationToken cancellationToken)
    {
        HoldsWritesAfterFirstRead = false;
        await SendHeldWritesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask SendHeldWritesAsync(CancellationToken cancellationToken)
    {
        if (_heldWrites is not { } held)
        {
            return;
        }

        _heldWrites = null;
        await connection.WriteAsync(held.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await SendHeldWritesAsync(cancellationToken).ConfigureAwait(false);
        _hasRead = true;
        var read = await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        TransportEnded |= read == 0 && !buffer.IsEmpty;
        TicketRecords?.ObserveReceived(buffer.Span[..read]);
        return read;
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (HoldsWritesAfterFirstRead && _hasRead)
        {
            (_heldWrites ??= new MemoryStream()).Write(buffer.Span);
            return;
        }

        await SendHeldWritesAsync(cancellationToken).ConfigureAwait(false);
        await connection.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) =>
        connection.FlushAsync(cancellationToken).AsTask();

    /// <summary>Not supported: the connection has no synchronous read.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>Not supported: the connection has no synchronous write.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>Not supported: the connection has no synchronous flush.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override void Flush() => throw new NotSupportedException();

    /// <summary>Not supported: a connection cannot seek.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <summary>Not supported: a connection has no length.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override void SetLength(long value) => throw new NotSupportedException();
}
