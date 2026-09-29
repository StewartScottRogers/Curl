namespace Curl.Tls;

/// <summary>
/// A connected TLS 1.3 client, read and written like the stream <c>SslStream</c> gives:
/// writes go out as protected application data records of at most 2^14 bytes, reads
/// return application data. Post-handshake messages are handled as they arrive: a
/// NewSessionTicket goes to the handshake (<see cref="Handshake" />), a CertificateRequest
/// (once <c>post_handshake_auth</c> was offered) is answered with the handshake's
/// Certificate, CertificateVerify and Finished, and a KeyUpdate moves the read keys on and,
/// when the server asks, is answered before its own keys move on.
/// <c>close_notify</c> ends the stream, and so does the transport ending without one, as
/// with <c>SslStream</c>: <see cref="CloseNotifyReceived" /> tells the two apart, so the
/// caller can fail an unfinished transfer with curl's exit 56 (ADR-0157). Any
/// other alert, or a record that fails its checks, fails the stream with a
/// <see cref="TlsAlertException" />, the alert sent to the server first when the client
/// raised it. The stream is asynchronous only: the synchronous <c>Read</c> and
/// <c>Write</c> throw <see cref="NotSupportedException" />.
/// </summary>
public sealed class Tls13ClientStream : Stream
{
    private readonly Stream transport;
    private readonly Tls13RecordLayer layer;
    private readonly List<byte> postHandshakeBytes = [];
    private byte[] pending = [];
    private int pendingOffset;
    private bool endOfStream;
    private bool shutdownSent;
    private bool disposed;
    private TlsAlertException? failure;

    internal Tls13ClientStream(Stream transport, Tls13RecordLayer layer, Tls13ClientHandshake handshake)
    {
        this.transport = transport;
        this.layer = layer;
        Handshake = handshake;
    }

    /// <summary>Gets the completed handshake: the negotiated suite, group, ALPN protocol, server certificates and the tickets received so far.</summary>
    public Tls13ClientHandshake Handshake { get; }

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
            await layer.SendAsync(TlsEncryptionLevel.Application, TlsContentType.ApplicationData, buffer, Tls13RecordProtection.LegacyRecordVersion, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <summary>Sends a KeyUpdate and moves the client's write keys on (RFC 8446 section 4.6.3).</summary>
    /// <param name="requestServerUpdate">Whether to ask the server to move its keys on too.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the KeyUpdate is sent.</returns>
    public Task UpdateKeysAsync(bool requestServerUpdate, CancellationToken cancellationToken = default)
    {
        ThrowIfUnusable();
        return layer.SendKeyUpdateAsync(requestServerUpdate, cancellationToken);
    }

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
            Handshake.Dispose();
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
        byte[]? record = await layer.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            // The transport ended without close_notify: the stream ends as SslStream's does, and
            // CloseNotifyReceived stays false for the caller to answer as curl does (ADR-0157).
            endOfStream = true;
            return;
        }

        Tls13RecordContent content = (TlsContentType)record[0] == TlsContentType.ApplicationData ? layer.Open(record) : throw Unexpected();
        switch (content.Type)
        {
            case TlsContentType.ApplicationData:
                pending = content.Content;
                pendingOffset = 0;
                break;
            case TlsContentType.Alert:
                ReceiveAlert(content.Content);
                break;
            case TlsContentType.Handshake:
                await ReceivePostHandshakeAsync(content.Content, cancellationToken).ConfigureAwait(false);
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

    private async Task ReceivePostHandshakeAsync(byte[] content, CancellationToken cancellationToken)
    {
        if (content.Length == 0)
        {
            throw Unexpected();
        }

        postHandshakeBytes.AddRange(content);
        while (true)
        {
            HandshakeMessageReadResult read = HandshakeMessageReader.Read(postHandshakeBytes.ToArray());
            if (read.Alert is { } alert)
            {
                throw new TlsAlertException(alert, false);
            }

            if (read.Message is null)
            {
                return;
            }

            byte[] encoded = [.. postHandshakeBytes.Take(read.BytesConsumed)];
            postHandshakeBytes.RemoveRange(0, read.BytesConsumed);
            await ReceivePostHandshakeMessageAsync(read.Message, encoded, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ReceivePostHandshakeMessageAsync(HandshakeMessage message, byte[] encoded, CancellationToken cancellationToken)
    {
        if (message.Type == HandshakeType.KeyUpdate)
        {
            await ReceiveKeyUpdateAsync(message.Body, cancellationToken).ConfigureAwait(false);
            return;
        }

        // Only a CertificateRequest writes an answer, so only it waits for the write lock; a
        // NewSessionTicket must not stall behind an application write the transport is holding up.
        TlsAlertDescription? rejected = message.Type == HandshakeType.CertificateRequest
            ? await layer.AnswerCertificateRequestAsync(encoded, cancellationToken).ConfigureAwait(false)
            : Handshake.Receive(TlsEncryptionLevel.Application, encoded).Failure?.Alert;
        if (rejected is { } alert)
        {
            throw new TlsAlertException(alert, false);
        }
    }

    /// <summary>Takes a KeyUpdate (RFC 8446 section 4.6.3), which must end its record because the keys change after it.</summary>
    private async Task ReceiveKeyUpdateAsync(byte[] body, CancellationToken cancellationToken)
    {
        if (postHandshakeBytes.Count > 0)
        {
            throw Unexpected();
        }

        if (body.Length != 1)
        {
            throw new TlsAlertException(TlsAlertDescription.DecodeError, false);
        }

        if (body[0] > 1)
        {
            throw new TlsAlertException(TlsAlertDescription.IllegalParameter, false);
        }

        layer.UpdateReadKeys();
        if (body[0] == 1)
        {
            await layer.SendKeyUpdateAsync(false, cancellationToken).ConfigureAwait(false);
        }
    }
}
