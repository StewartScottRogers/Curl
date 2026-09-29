using System.Buffers.Binary;

namespace Curl.Tls;

/// <summary>
/// Runs a client handshake that offers TLS 1.3 and TLS 1.2 (and below) in one ClientHello
/// over a caller's byte stream, and continues on the version the ServerHello picks
/// (ADR-0140, "Class structure"). The hello is the TLS 1.3 one with the TLS 1.2 offer added
/// (<see cref="TlsClientSettings" />). The server's first plaintext handshake records are
/// read until its first message is whole: a ServerHello without <c>supported_versions</c>
/// hands the sent hello to <see cref="Tls12ClientHandshake" />, which refuses either RFC
/// 8446 section 4.1.3 downgrade sentinel; anything else, a HelloRetryRequest, an alert or a
/// malformed record included, goes to <see cref="Tls13ClientHandshake" />, which judges it
/// as it would after a TLS 1.3-only hello. Either way the chosen record layer reads the
/// server's records from the first byte.
/// </summary>
public static class TlsClientConnection
{
    /// <summary>
    /// Runs the handshake over <paramref name="transport" />. On success the returned
    /// stream owns the transport; on failure the alert has been sent (unless the server
    /// sent one or closed the transport) and the transport is left to the caller. Only
    /// cancellation and the transport's own failures are thrown.
    /// </summary>
    /// <param name="transport">The byte stream to the server, such as a TCP connection's.</param>
    /// <param name="settings">What the ClientHello offers for each version.</param>
    /// <param name="random">The source of the random, the session ID, the key shares and CBC records' explicit IVs.</param>
    /// <param name="verifier">Judges the server's certificate chain.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The connected stream of the chosen version, or the failure.</returns>
    /// <exception cref="ArgumentException">The settings cannot drive a handshake.</exception>
    public static async Task<TlsConnectResult> ConnectAsync(
        Stream transport,
        TlsClientSettings settings,
        ITlsRandomSource random,
        IServerCertificateVerifier verifier,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        ServerHelloReplayStream replay = new(transport);
        Tls13ClientConnection tls13 = Tls13ClientConnection.Create(replay, settings.Tls13 with { LowerVersions = settings.Tls12 }, random, verifier);
        await tls13.SendClientHelloAsync(cancellationToken).ConfigureAwait(false);
        bool serverChoseTls12 = await ReadsTls12ServerHelloAsync(replay, cancellationToken).ConfigureAwait(false);
        replay.Replay();
        if (!serverChoseTls12)
        {
            Tls13ConnectResult result = await tls13.CompleteAsync(replay, cancellationToken).ConfigureAwait(false);
            return new TlsConnectResult(result.Stream, null, result.Failure);
        }

        tls13.Abandon();
        Tls12ConnectResult tls12 = await Tls12ClientConnection.ContinueAsync(replay, settings.Tls12, random, verifier, tls13.SentClientHello, cancellationToken).ConfigureAwait(false);
        return new TlsConnectResult(null, tls12.Stream, tls12.Failure);
    }

    // Reads plaintext handshake records until the first message is whole; false as soon as
    // what arrives is not a TLS 1.2-or-below ServerHello, so the TLS 1.3 client judges it.
    private static async Task<bool> ReadsTls12ServerHelloAsync(Stream transport, CancellationToken cancellationToken)
    {
        List<byte> handshakeBytes = [];
        while (await ReadHandshakeFragmentAsync(transport, cancellationToken).ConfigureAwait(false) is { } fragment)
        {
            handshakeBytes.AddRange(fragment);
            HandshakeMessageReadResult read = HandshakeMessageReader.Read(handshakeBytes.ToArray());
            if (read.Message is { } message)
            {
                return IsTls12ServerHello(message);
            }

            if (read.Alert is not null)
            {
                return false;
            }
        }

        return false;
    }

    // One plaintext handshake record's fragment, or null for any other record, one too long,
    // or the transport's end.
    private static async Task<byte[]?> ReadHandshakeFragmentAsync(Stream transport, CancellationToken cancellationToken)
    {
        byte[] header = new byte[Tls13RecordProtection.RecordHeaderLength];
        int headerRead = await transport.ReadAtLeastAsync(header, header.Length, false, cancellationToken).ConfigureAwait(false);
        int length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(3));
        if (headerRead < header.Length || header[0] != (byte)TlsContentType.Handshake || length > Tls13RecordProtection.MaximumPlaintextLength)
        {
            return null;
        }

        byte[] fragment = new byte[length];
        int read = await transport.ReadAtLeastAsync(fragment, length, false, cancellationToken).ConfigureAwait(false);
        return read < length ? null : fragment;
    }

    private static bool IsTls12ServerHello(HandshakeMessage message) =>
        message.Type == HandshakeType.ServerHello
        && ServerHello.Decode(message.Body) is { Succeeded: true } hello
        && hello.Value.Extensions.All(extension => extension.Type != TlsExtensionType.SupportedVersions);
}
