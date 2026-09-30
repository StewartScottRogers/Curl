using System.Security.Cryptography;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Compression;
using Curl.Protocol.Ssh.HostKeys;
using Curl.Protocol.Ssh.KeyExchange;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Protocol.Ssh.PacketProtection;

namespace Curl.Protocol.Ssh.Transport;

/// <summary>
/// The client side of the SSH transport layer (RFC 4253) up to the new keys: the
/// identification exchange, the unencrypted binary packets, the two <c>KEXINIT</c>
/// messages, the key exchange, the host key's signature and <c>NEWKEYS</c>; a key
/// re-exchange the server starts later; and the compression the first exchange agreed,
/// which lasts the session as OpenSSH keeps it across re-exchanges.
/// </summary>
internal sealed class SshTransport
{
    private readonly IConnection connection;

    private readonly SshAlgorithmPreferences preferences;

    private readonly SshAlgorithmCatalogue catalogue;

    private readonly ISshRandomSource randomSource;

    private readonly ISshEphemeralKeySource ephemeralKeySource;

    private readonly SshConnectionReader connectionReader;

    private byte[]? sessionIdentifier;

    private string? serverIdentification;

    private bool isStrictKeyExchange;

    private SshNegotiatedAlgorithms? sessionCompression;

    /// <summary>
    /// Initializes a new instance of the <see cref="SshTransport" /> class.
    /// </summary>
    /// <param name="connection">The connection to the server.</param>
    /// <param name="preferences">The platform preset, after compression and known-hosts narrowing.</param>
    /// <param name="catalogue">The algorithms this build implements.</param>
    /// <param name="randomSource">Where the cookie and padding bytes come from.</param>
    /// <param name="ephemeralKeySource">Where the key exchange's ephemeral key pairs come from.</param>
    internal SshTransport(
        IConnection connection,
        SshAlgorithmPreferences preferences,
        SshAlgorithmCatalogue catalogue,
        ISshRandomSource randomSource,
        ISshEphemeralKeySource ephemeralKeySource)
    {
        this.connection = connection;
        this.preferences = preferences;
        this.catalogue = catalogue;
        this.randomSource = randomSource;
        this.ephemeralKeySource = ephemeralKeySource;
        connectionReader = new SshConnectionReader(connection);
        PacketReader = new SshPacketReader(connectionReader);
        PacketWriter = new SshPacketWriter(connection, randomSource);
    }

    /// <summary>
    /// Gets the reader of the server's packets.
    /// </summary>
    internal SshPacketReader PacketReader { get; }

    /// <summary>
    /// Gets the writer of the client's packets.
    /// </summary>
    internal SshPacketWriter PacketWriter { get; }

    /// <summary>
    /// Gets the session identifier: the exchange hash of the first key exchange, which a
    /// <c>publickey</c> signature covers (RFC 4252 section 7).
    /// </summary>
    /// <exception cref="InvalidOperationException">No key exchange has completed yet.</exception>
    internal byte[] SessionIdentifier => sessionIdentifier ?? throw new InvalidOperationException("The session identifier exists only after the first key exchange.");

    /// <summary>
    /// Exchanges identification strings, sends the client's <c>KEXINIT</c> at once, reads
    /// the server's and agrees the algorithms, as libssh2 1.11.1 does.
    /// </summary>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>What the key exchange that follows needs.</returns>
    /// <exception cref="SshTransferException">
    /// Exit 2 with curl's <c>Failure establishing ssh session</c> message: <c>-13, Failed
    /// getting banner</c> when no identification string arrives; <c>-5, Unable to exchange
    /// encryption keys</c> when the lists share no algorithm; <c>-1, Unable to exchange
    /// encryption keys</c> when the peer closes, breaks the packet framing, sends a
    /// malformed <c>KEXINIT</c>, sends any other message first but <c>IGNORE</c>,
    /// <c>DEBUG</c> or <c>UNIMPLEMENTED</c>, or sends even those first under strict key
    /// exchange.
    /// </exception>
    internal async ValueTask<SshNegotiatedHandshake> NegotiateAlgorithmsAsync(CancellationToken cancellationToken)
    {
        string serverIdentificationLine = await SshIdentificationExchange.ExchangeAsync(connection, connectionReader, cancellationToken).ConfigureAwait(false);
        byte[] clientPayload = await SendClientKexInitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            (byte[] serverPayload, int packetsBefore) = await ReadServerKexInitAsync(cancellationToken).ConfigureAwait(false);
            SshNegotiatedHandshake handshake = Negotiate(serverIdentificationLine, clientPayload, serverPayload);
            if (handshake.Algorithms.IsStrictKeyExchange && packetsBefore > 0)
            {
                throw new InvalidDataException("Strict key exchange allows no packet before the server's KEXINIT.");
            }

            return handshake;
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException)
        {
            throw SshTransferException.SessionEstablishmentFailed(Libssh2ErrorCode.SocketNone, Libssh2ErrorCode.UnableToExchangeEncryptionKeys);
        }
    }

    /// <summary>
    /// Runs the agreed key-exchange method, checks the server's signature over the exchange
    /// hash with the agreed host-key algorithm, and exchanges <c>NEWKEYS</c>. Under strict
    /// key exchange each direction's sequence number restarts at 0 after its
    /// <c>NEWKEYS</c>. The first exchange's hash becomes the session identifier. Each
    /// direction takes the new keys into use at its <c>NEWKEYS</c>: the client's packets
    /// once its own is sent, the server's once the server's arrives. The first exchange's
    /// <c>zlib</c> starts compressing a direction at the same point; its
    /// <c>zlib@openssh.com</c> waits for <see cref="StartDelayedCompression" />.
    /// </summary>
    /// <param name="handshake">What <see cref="NegotiateAlgorithmsAsync" /> agreed.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The exchange hash, the session identifier and the keys' derivation.</returns>
    /// <exception cref="SshTransferException">
    /// Exit 2 with <c>Failure establishing ssh session: -8, Unable to exchange encryption
    /// keys</c>, as measured, when the peer closes, sends an unexpected or malformed
    /// message, a public value outside its group, a host key or signature of another type,
    /// or a signature that does not verify; with <c>-4</c> (a MAC) or <c>-12</c> (an AES-GCM
    /// tag) in place of <c>-8</c> when a re-exchange reads a packet that fails its check
    /// (ADR-0212).
    /// </exception>
    /// <exception cref="NotSupportedException">The agreed method, host key, cipher or MAC is not implemented.</exception>
    internal async ValueTask<SshKeyExchangeResult> ExchangeKeysAsync(SshNegotiatedHandshake handshake, CancellationToken cancellationToken)
    {
        bool isFirstExchange = sessionIdentifier is null;
        isStrictKeyExchange = isFirstExchange ? handshake.Algorithms.IsStrictKeyExchange : isStrictKeyExchange;
        SshKeyExchangeMessages messages = new(PacketReader, PacketWriter, isStrictKeyExchange && isFirstExchange);
        ISshKeyExchange method = SshKeyExchangeMethods.Create(handshake.Algorithms.KeyExchange, ephemeralKeySource);
        ISshSignatureVerifier verifier = SshSignatureVerifiers.For(handshake.Algorithms.ServerHostKey);
        try
        {
            if (handshake.Algorithms.DiscardServerGuess)
            {
                await PacketReader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }

            SshKeyExchangeOutcome outcome = await method.ExchangeAsync(messages, handshake, cancellationToken).ConfigureAwait(false);
            if (!verifier.Verify(outcome.HostKey, outcome.Signature, outcome.ExchangeHash))
            {
                throw new InvalidDataException("The SSH server's signature over the exchange hash does not verify.");
            }

            byte[] session = sessionIdentifier ?? outcome.ExchangeHash;
            SshKeyDerivation keys = new(outcome.HashAlgorithm, outcome.SharedSecret, outcome.ExchangeHash, session);
            await SwitchKeysAsync(messages, handshake.Algorithms, keys, cancellationToken).ConfigureAwait(false);
            sessionIdentifier = session;
            serverIdentification = handshake.ServerIdentification;
            return new SshKeyExchangeResult(handshake.Algorithms, outcome.HostKey, outcome.ExchangeHash, session, keys);
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException or CryptographicException)
        {
            throw KeyExchangeMethodFailed();
        }
        catch (SshPacketAuthenticationException exception)
        {
            throw SshTransferException.SessionEstablishmentFailed(exception.Libssh2ErrorCode, Libssh2ErrorCode.UnableToExchangeEncryptionKeys);
        }
    }

    /// <summary>
    /// Answers a <c>KEXINIT</c> the server sends after the first exchange (RFC 4253 section
    /// 9), the server-initiated re-exchange ADR-0122 gives the transport: sends the client's
    /// own <c>KEXINIT</c>, agrees the algorithms again and runs a new exchange that keeps
    /// the session identifier.
    /// </summary>
    /// <param name="serverKexInitPayload">The server's <c>KEXINIT</c>, as received.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The new keys.</returns>
    /// <exception cref="InvalidOperationException">No key exchange has finished yet.</exception>
    /// <exception cref="SshTransferException">
    /// Exit 2 with <c>-5</c> when the lists share no algorithm, and <c>-8</c> for a malformed
    /// <c>KEXINIT</c> or any failure <see cref="ExchangeKeysAsync" /> reports.
    /// </exception>
    internal async ValueTask<SshKeyExchangeResult> ReExchangeKeysAsync(byte[] serverKexInitPayload, CancellationToken cancellationToken)
    {
        string identification = serverIdentification
            ?? throw new InvalidOperationException("A key re-exchange needs a finished first key exchange.");
        byte[] clientPayload = await SendClientKexInitAsync(cancellationToken).ConfigureAwait(false);
        SshNegotiatedHandshake handshake;
        try
        {
            handshake = Negotiate(identification, clientPayload, serverKexInitPayload);
        }
        catch (InvalidDataException)
        {
            throw KeyExchangeMethodFailed();
        }

        return await ExchangeKeysAsync(handshake, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts each direction's <c>zlib@openssh.com</c> the first exchange agreed, as the
    /// client must once <c>SSH_MSG_USERAUTH_SUCCESS</c> arrives: the packets after it are
    /// compressed both ways (OpenSSH's <c>PROTOCOL</c>, "delayed compression"). A direction with
    /// another method, and a transport before its first exchange, is left as it is.
    /// </summary>
    internal void StartDelayedCompression()
    {
        if (sessionCompression?.CompressionClientToServer == SshCompressionMethods.DelayedZlib)
        {
            PacketWriter.StartCompression();
        }

        if (sessionCompression?.CompressionServerToClient == SshCompressionMethods.DelayedZlib)
        {
            PacketReader.StartDecompression();
        }
    }

    private static SshTransferException KeyExchangeMethodFailed() =>
        SshTransferException.SessionEstablishmentFailed(Libssh2ErrorCode.KeyExchangeMethodFailure, Libssh2ErrorCode.UnableToExchangeEncryptionKeys);

    private static SshNegotiatedHandshake Negotiate(string serverIdentificationLine, byte[] clientPayload, byte[] serverPayload)
    {
        SshNegotiatedAlgorithms algorithms = SshAlgorithmNegotiator.Negotiate(SshKexInit.Parse(clientPayload), SshKexInit.Parse(serverPayload))
            ?? throw SshTransferException.SessionEstablishmentFailed(Libssh2ErrorCode.KeyExchangeFailure, Libssh2ErrorCode.UnableToExchangeEncryptionKeys);
        return new SshNegotiatedHandshake(
            SshIdentificationExchange.ClientIdentification,
            serverIdentificationLine,
            clientPayload,
            serverPayload,
            algorithms);
    }

    private async ValueTask<byte[]> SendClientKexInitAsync(CancellationToken cancellationToken)
    {
        byte[] clientPayload = SshKexInit.ForClient(preferences, catalogue, randomSource).ToPayload();
        await PacketWriter.WriteAsync(clientPayload, cancellationToken).ConfigureAwait(false);
        return clientPayload;
    }

    private async ValueTask SwitchKeysAsync(SshKeyExchangeMessages messages, SshNegotiatedAlgorithms algorithms, SshKeyDerivation keys, CancellationToken cancellationToken)
    {
        ISshPacketProtection clientToServer = SshPacketProtections.ForClientToServer(algorithms, keys);
        ISshPacketProtection serverToClient = SshPacketProtections.ForServerToClient(algorithms, keys);
        bool startsCompression = sessionCompression is null;
        sessionCompression ??= algorithms;
        await messages.SendAsync([SshMessageNumber.NewKeys], cancellationToken).ConfigureAwait(false);
        TakeClientKeysIntoUse(clientToServer, startsCompression && algorithms.CompressionClientToServer == SshCompressionMethods.Zlib);
        await messages.ReadAsync(SshMessageNumber.NewKeys, cancellationToken).ConfigureAwait(false);
        TakeServerKeysIntoUse(serverToClient, startsCompression && algorithms.CompressionServerToClient == SshCompressionMethods.Zlib);
    }

    // After the client's NEWKEYS is sent.
    private void TakeClientKeysIntoUse(ISshPacketProtection clientToServer, bool startsCompression)
    {
        PacketWriter.ChangeProtection(clientToServer);
        if (isStrictKeyExchange)
        {
            PacketWriter.ResetSequenceNumber();
        }

        if (startsCompression)
        {
            PacketWriter.StartCompression();
        }
    }

    // After the server's NEWKEYS arrives.
    private void TakeServerKeysIntoUse(ISshPacketProtection serverToClient, bool startsDecompression)
    {
        PacketReader.ChangeProtection(serverToClient);
        if (isStrictKeyExchange)
        {
            PacketReader.ResetSequenceNumber();
        }

        if (startsDecompression)
        {
            PacketReader.StartDecompression();
        }
    }

    private async ValueTask<(byte[] Payload, int PacketsBefore)> ReadServerKexInitAsync(CancellationToken cancellationToken)
    {
        int packetsBefore = 0;
        while (true)
        {
            byte[] payload = await PacketReader.ReadAsync(cancellationToken).ConfigureAwait(false);
            switch (payload[0])
            {
                case SshMessageNumber.KeyExchangeInit:
                    return (payload, packetsBefore);
                case SshMessageNumber.Ignore or SshMessageNumber.Debug or SshMessageNumber.Unimplemented:
                    packetsBefore++;
                    break;
                default:
                    throw new InvalidDataException($"The SSH server sent message {payload[0]} before its KEXINIT.");
            }
        }
    }
}
