namespace Curl.Tls;

/// <summary>
/// A connected TLS 1.2, 1.1 or 1.0 client, read and written like the stream
/// <c>SslStream</c> gives: writes go out as protected application data records of at most
/// 2^14 bytes (after an empty one in TLS 1.0 CBC unless
/// <see cref="Tls12ClientSettings.InsertEmptyFragment" /> is off), reads return application
/// data. A server's HelloRequest is ignored, as Curl never renegotiates. <c>close_notify</c>
/// ends the stream, and so does the transport ending without one, as with
/// <c>SslStream</c>: <see cref="CloseNotifyReceived" /> tells the two apart, so the caller
/// can fail an unfinished transfer with curl's exit 56 (ADR-0157). Any other alert, or a
/// record that fails its checks, fails the stream with a <see cref="TlsAlertException" />,
/// the alert sent to the server first when the client raised it. The stream is
/// asynchronous only: the synchronous <c>Read</c> and <c>Write</c> throw
/// <see cref="NotSupportedException" />.
/// </summary>
public sealed class Tls12ClientStream : Stream
{
    private readonly Stream transport;
    private readonly Tls12RecordLayer layer;
    private readonly List<byte> postHandshakeBytes = [];
    private byte[] pending = [];
    private int pendingOffset;
    private bool endOfStream;
    private bool shutdownSent;
    private bool disposed;
    private TlsAlertException? failure;

    internal Tls12ClientStream(Stream transport, Tls12RecordLayer layer, Tls12ClientHandshake handshake)
    {
        this.transport = transport;
        this.layer = layer;
        Handshake = handshake;
    }

    /// <summary>Gets the completed handshake: the negotiated version, suite, group, ALPN protocol, server certificates and the session to resume.</summary>
    public Tls12ClientHandshake Handshake { get; }

    /// <summary>Gets a value indicating whether the stream ended with the server's <c>close_notify</c>, rather than with the transport ending.</summary>
    public bool CloseNotifyReceived { get; private set; }

    /// <inheritdoc />
    public override bool CanRead => !disposed;

    /// <inheritdoc />
    public override bool CanWrite => !disposed;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException("A TLS stream has no length.");

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException("A TLS stream has no position.");
        set => throw new NotSupportedException("A TLS stream has no position.");
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfUnusable();
        if (buffer.IsEmpty)
        {
            return 0;
        }

        while (pendingOffset == pending.Length && !endOfStream)
        {
            await ReceiveRecordAsync(cancellationToken).ConfigureAwait(false);
        }

        int count = Math.Min(buffer.Length, pending.Length - pendingOffset);
        pending.AsMemory(pendingOffset, count).CopyTo(buffer);
        pendingOffset += count;
        return count;
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfUnusable();
        if (shutdownSent)
        {
            throw new InvalidOperationException("The stream has sent close_notify and can write no more.");
        }

        if (!buffer.IsEmpty)
        {
            await layer.SendAsync(TlsContentType.ApplicationData, buffer, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <summary>Sends <c>close_notify</c> once; the stream can still read what the server sends after it.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the alert is sent.</returns>
    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfUnusable();
        if (!shutdownSent)
        {
            shutdownSent = true;
            await layer.SendAlertAsync(TlsAlertDescription.CloseNotify, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => layer.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public override void Flush() => transport.Flush();

    /// <summary>Not supported: the stream is asynchronous only.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Read a TLS stream with ReadAsync.");

    /// <summary>Not supported: the stream is asynchronous only.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("Write a TLS stream with WriteAsync.");

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException("A TLS stream cannot seek.");

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException("A TLS stream has no length.");

    /// <summary>Zeroes the keys and disposes the transport, without sending <c>close_notify</c> (<see cref="ShutdownAsync" /> sends it).</summary>
    /// <param name="disposing">Whether the call comes from <see cref="Stream.Dispose()" />; always, as the stream has no finalizer.</param>
    protected override void Dispose(bool disposing)
    {
        if (!disposed)
        {
            disposed = true;
            layer.Dispose();
            transport.Dispose();
        }

        base.Dispose(disposing);
    }

    private static TlsAlertException Unexpected() => new(TlsAlertDescription.UnexpectedMessage, false);

    private void ThrowIfUnusable()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (failure is not null)
        {
            throw new TlsAlertException(failure.Alert, failure.IsFromServer);
        }
    }

    private async Task ReceiveRecordAsync(CancellationToken cancellationToken)
    {
        TlsAlertException? alert = await TryReceiveRecordContentAsync(cancellationToken).ConfigureAwait(false);
        if (alert is null)
        {
            return;
        }

        failure = alert;
        if (!alert.IsFromServer)
        {
            await layer.SendAlertAsync(alert.Alert, cancellationToken).ConfigureAwait(false);
        }

        throw alert;
    }

    private async Task<TlsAlertException?> TryReceiveRecordContentAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ReceiveRecordContentAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (TlsAlertException alert)
        {
            return alert;
        }
    }

    private async Task ReceiveRecordContentAsync(CancellationToken cancellationToken)
    {
        Tls12RecordContent? record = await layer.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            // The transport ended without close_notify: the stream ends as SslStream's does, and
            // CloseNotifyReceived stays false for the caller to answer as curl does (ADR-0157).
            endOfStream = true;
            return;
        }

        switch (record.Type)
        {
            case TlsContentType.ApplicationData:
                pending = record.Content;
                pendingOffset = 0;
                break;
            case TlsContentType.Alert:
                ReceiveAlert(record.Content);
                break;
            case TlsContentType.Handshake:
                ReceivePostHandshake(record.Content);
                break;
            default:
                throw Unexpected();
        }
    }

    private void ReceiveAlert(byte[] content)
    {
        TlsAlertDescription alert = TlsAlertRecord.Decode(content);
        if (alert != TlsAlertDescription.CloseNotify)
        {
            throw new TlsAlertException(alert, true);
        }

        CloseNotifyReceived = true;
        endOfStream = true;
    }

    /// <summary>Takes handshake content after completion: HelloRequests, whole or split across records, are ignored; any other message is unexpected.</summary>
    private void ReceivePostHandshake(byte[] content)
    {
        postHandshakeBytes.AddRange(content);
        while (true)
        {
            HandshakeMessageReadResult read = HandshakeMessageReader.Read(postHandshakeBytes.ToArray());
            if (read.Message is null)
            {
                ThrowIfFailed(read);
                return;
            }

            postHandshakeBytes.RemoveRange(0, read.BytesConsumed);
            CheckHelloRequest(read.Message);
        }
    }

    private static void ThrowIfFailed(HandshakeMessageReadResult read)
    {
        if (read.Alert is { } alert)
        {
            throw new TlsAlertException(alert, false);
        }
    }

    private static void CheckHelloRequest(HandshakeMessage message)
    {
        if (message.Type != HandshakeType.HelloRequest)
        {
            throw Unexpected();
        }

        if (message.Body.Length != 0)
        {
            throw new TlsAlertException(TlsAlertDescription.DecodeError, false);
        }
    }
}
