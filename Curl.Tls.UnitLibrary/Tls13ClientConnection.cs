namespace Curl.Tls;

/// <summary>
/// Runs the TLS 1.3 client handshake over a caller's byte stream (RFC 8446 section 5):
/// the ClientHello in a plaintext record, the server's records read one at a time and
/// their handshake content handed to <see cref="Tls13ClientHandshake" />, the traffic
/// secrets it installs put in force, and the client's flight protected at its level. In
/// middlebox compatibility mode (<see cref="Tls13ClientSettings.SendLegacySessionId" />)
/// the client sends one <c>change_cipher_spec</c> record before its second flight
/// (appendix D.4); a server's <c>change_cipher_spec</c> of the single byte 1 is ignored
/// until the handshake completes.
/// </summary>
public sealed class Tls13ClientConnection
{
    private readonly Tls13ClientSettings settings;
    private readonly Tls13ClientHandshake handshake;
    private readonly Tls13RecordLayer layer;
    private bool clientHelloSent;
    private bool changeCipherSpecSent;

    private Tls13ClientConnection(Tls13ClientSettings settings, Tls13ClientHandshake handshake, Stream transport)
    {
        this.settings = settings;
        this.handshake = handshake;
        layer = new Tls13RecordLayer(transport, handshake);
    }

    /// <summary>
    /// Runs the handshake over <paramref name="transport" />. On success the returned
    /// stream owns the transport; on failure the alert has been sent (unless the server
    /// sent one or closed the transport) and the transport is left to the caller. Only
    /// cancellation and the transport's own failures are thrown.
    /// </summary>
    /// <param name="transport">The byte stream to the server, such as a TCP connection's.</param>
    /// <param name="settings">What the ClientHello offers.</param>
    /// <param name="random">The source of the random, the legacy session ID and the key shares.</param>
    /// <param name="verifier">Judges the server's certificate chain.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The connected stream, or the failure.</returns>
    /// <exception cref="ArgumentException">The settings offer a suite whose records cannot be protected yet (<see cref="Tls13RecordProtection.CanProtect" />).</exception>
    public static async Task<Tls13ConnectResult> ConnectAsync(
        Stream transport,
        Tls13ClientSettings settings,
        ITlsRandomSource random,
        IServerCertificateVerifier verifier,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.CipherSuites.All(Tls13RecordProtection.CanProtect))
        {
            throw new ArgumentException("A TLS 1.3 connection over a byte stream offers only the GCM and ChaCha20-Poly1305 suites until AES-CCM is built.", nameof(settings));
        }

        Tls13ClientConnection connection = new(settings, new Tls13ClientHandshake(settings, random, verifier), transport);
        TlsHandshakeFailure? failure = await connection.HandshakeAsync(cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            connection.layer.Dispose();
            connection.handshake.Dispose();
            return new Tls13ConnectResult(null, failure);
        }

        return new Tls13ConnectResult(new Tls13ClientStream(transport, connection.layer, connection.handshake), null);
    }

    private static void CheckCompatibilityChangeCipherSpec(ReadOnlySpan<byte> fragment)
    {
        if (fragment is not [1])
        {
            throw new TlsAlertException(TlsAlertDescription.UnexpectedMessage, false);
        }
    }

    private static void CheckHandshakeFragment(ReadOnlySpan<byte> fragment)
    {
        if (fragment.IsEmpty)
        {
            throw new TlsAlertException(TlsAlertDescription.UnexpectedMessage, false);
        }
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
        await ApplyAsync(handshake.Start(), cancellationToken).ConfigureAwait(false);
        while (!handshake.IsComplete)
        {
            byte[]? record = await layer.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (record is null)
            {
                return new TlsHandshakeFailure(TlsAlertDescription.CloseNotify, null) { Origin = TlsHandshakeFailureOrigin.TransportClosed };
            }

            Tls13HandshakeOutput? output = Receive(record);
            if (output is not null)
            {
                await ApplyAsync(output, cancellationToken).ConfigureAwait(false);
            }

            if (output?.Failure is { } failure)
            {
                await layer.SendAlertAsync(failure.Alert, cancellationToken).ConfigureAwait(false);
                return failure;
            }
        }

        return null;
    }

    /// <summary>Takes one record from the server; returns what the handshake asks for, or <see langword="null" /> for an ignored <c>change_cipher_spec</c>.</summary>
    private Tls13HandshakeOutput? Receive(byte[] record)
    {
        TlsContentType type = (TlsContentType)record[0];
        ReadOnlySpan<byte> fragment = record.AsSpan(Tls13RecordProtection.RecordHeaderLength);
        if (type == TlsContentType.ChangeCipherSpec)
        {
            CheckCompatibilityChangeCipherSpec(fragment);
            return null;
        }

        if (layer.IsReadProtected)
        {
            return type == TlsContentType.ApplicationData
                ? ReceiveContent(layer.Open(record))
                : throw new TlsAlertException(TlsAlertDescription.UnexpectedMessage, false);
        }

        return fragment.Length > Tls13RecordProtection.MaximumPlaintextLength
            ? throw new TlsAlertException(TlsAlertDescription.RecordOverflow, false)
            : ReceiveContent(new Tls13RecordContent(type, fragment.ToArray()));
    }

    private Tls13HandshakeOutput ReceiveContent(Tls13RecordContent content)
    {
        switch (content.Type)
        {
            case TlsContentType.Handshake:
                CheckHandshakeFragment(content.Content);
                return handshake.Receive(layer.ReadLevel, content.Content);
            case TlsContentType.Alert:
                throw new TlsAlertException(TlsAlertRecord.Decode(content.Content), true);
            default:
                throw new TlsAlertException(TlsAlertDescription.UnexpectedMessage, false);
        }
    }

    private async Task ApplyAsync(Tls13HandshakeOutput output, CancellationToken cancellationToken)
    {
        foreach (Tls13TrafficSecret secret in output.SecretsInstalled)
        {
            layer.Install(secret);
        }

        foreach (TlsHandshakeBytes bytes in output.BytesToSend)
        {
            await SendAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendAsync(TlsHandshakeBytes bytes, CancellationToken cancellationToken)
    {
        bool isFirstClientHello = !clientHelloSent;
        if (!isFirstClientHello && settings.SendLegacySessionId && !changeCipherSpecSent)
        {
            changeCipherSpecSent = true;
            await layer.SendAsync(TlsEncryptionLevel.Initial, TlsContentType.ChangeCipherSpec, new byte[] { 1 }, Tls13RecordProtection.LegacyRecordVersion, cancellationToken).ConfigureAwait(false);
        }

        ushort recordVersion = isFirstClientHello ? settings.ClientHelloRecordVersion : Tls13RecordProtection.LegacyRecordVersion;
        await layer.SendAsync(bytes.Level, TlsContentType.Handshake, bytes.Bytes, recordVersion, cancellationToken).ConfigureAwait(false);
        clientHelloSent = true;
    }
}
