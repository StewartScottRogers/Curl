namespace Curl.Tls;

/// <summary>
/// Runs the TLS 1.2, 1.1 and 1.0 client handshake over a caller's byte stream (RFC 5246
/// sections 6 and 7): the ClientHello in a plaintext record, the server's records read
/// one at a time with their handshake content handed to <see cref="Tls12ClientHandshake" />
/// (which frames messages that span records) and its ChangeCipherSpec switching the read
/// state, and the client's messages written in order, the write state switched after its
/// own ChangeCipherSpec. A received alert ends the handshake.
/// </summary>
public sealed class Tls12ClientConnection
{
    private readonly Tls12ClientSettings settings;
    private readonly ITlsRandomSource random;
    private readonly Tls12ClientHandshake handshake;
    private readonly Tls12RecordLayer layer;

    private Tls12ClientConnection(Tls12ClientSettings settings, ITlsRandomSource random, Tls12ClientHandshake handshake, Stream transport)
    {
        this.settings = settings;
        this.random = random;
        this.handshake = handshake;
        layer = new Tls12RecordLayer(transport);
    }

    /// <summary>
    /// Runs the handshake over <paramref name="transport" />. On success the returned
    /// stream owns the transport; on failure the alert has been sent (unless the server
    /// sent one or closed the transport) and the transport is left to the caller. Only
    /// cancellation and the transport's own failures are thrown.
    /// </summary>
    /// <param name="transport">The byte stream to the server, such as a TCP connection's.</param>
    /// <param name="settings">What the ClientHello offers.</param>
    /// <param name="random">The source of the random, the session ID, the key shares and CBC records' explicit IVs.</param>
    /// <param name="verifier">Judges the server's certificate chain.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The connected stream, or the failure.</returns>
    /// <exception cref="ArgumentException">The settings cannot drive a handshake.</exception>
    public static async Task<Tls12ConnectResult> ConnectAsync(
        Stream transport,
        Tls12ClientSettings settings,
        ITlsRandomSource random,
        IServerCertificateVerifier verifier,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transport);
        Tls12ClientConnection connection = new(settings, random, new Tls12ClientHandshake(settings, random, verifier), transport);
        TlsHandshakeFailure? failure = await connection.HandshakeAsync(cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            connection.layer.Dispose();
            return new Tls12ConnectResult(null, failure);
        }

        return new Tls12ConnectResult(new Tls12ClientStream(transport, connection.layer, connection.handshake), null);
    }

    private async Task<TlsHandshakeFailure?> HandshakeAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await ExchangeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (TlsAlertException alert) when (alert.IsFromServer)
        {
            return new TlsHandshakeFailure(alert.Alert, null) { Origin = TlsHandshakeFailureOrigin.AlertReceived };
        }
        catch (TlsAlertException alert)
        {
            await layer.SendAlertAsync(alert.Alert, cancellationToken).ConfigureAwait(false);
            return new TlsHandshakeFailure(alert.Alert, null);
        }
    }

    private async Task<TlsHandshakeFailure?> ExchangeAsync(CancellationToken cancellationToken)
    {
        await SendAsync(handshake.Start(), cancellationToken).ConfigureAwait(false);
        while (!handshake.IsComplete)
        {
            Tls12RecordContent? record = await layer.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (record is null)
            {
                return new TlsHandshakeFailure(TlsAlertDescription.CloseNotify, null) { Origin = TlsHandshakeFailureOrigin.TransportClosed };
            }

            Tls12HandshakeOutput output = Receive(record);
            if (handshake.Version is { } version)
            {
                layer.FixVersion(version);
            }

            if (output.Failure is { } failure)
            {
                await layer.SendAlertAsync(failure.Alert, cancellationToken).ConfigureAwait(false);
                return failure;
            }

            await SendAsync(output, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private Tls12HandshakeOutput Receive(Tls12RecordContent record)
    {
        switch (record.Type)
        {
            case TlsContentType.Handshake:
                return handshake.ReceiveHandshake(record.Content);
            case TlsContentType.ChangeCipherSpec:
                return ReceiveChangeCipherSpec(record.Content);
            case TlsContentType.Alert:
                throw new TlsAlertException(TlsAlertRecord.Decode(record.Content), true);
            default:
                throw new TlsAlertException(TlsAlertDescription.UnexpectedMessage, false);
        }
    }

    private Tls12HandshakeOutput ReceiveChangeCipherSpec(byte[] content)
    {
        Tls12HandshakeOutput output = handshake.ReceiveChangeCipherSpec(content);
        if (output.Failure is null)
        {
            layer.SwitchReadState(Tls12RecordReadState.Create(handshake.RecordProtection!, handshake.KeyBlock!.ServerWrite));
        }

        return output;
    }

    private async Task SendAsync(Tls12HandshakeOutput output, CancellationToken cancellationToken)
    {
        foreach (Tls12OutgoingMessage message in output.MessagesToSend)
        {
            await layer.SendAsync(message.ContentType, message.Bytes, cancellationToken).ConfigureAwait(false);
            if (message.ContentType == TlsContentType.ChangeCipherSpec)
            {
                layer.SwitchWriteState(Tls12RecordWriteState.Create(handshake.RecordProtection!, handshake.KeyBlock!.ClientWrite, random, settings.InsertEmptyFragment));
            }
        }
    }
}
