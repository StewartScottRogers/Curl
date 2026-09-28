using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Negotiation;

namespace Curl.Protocol.Ssh.Transport;

/// <summary>
/// The client side of the SSH transport layer (RFC 4253) up to the choice of algorithms:
/// the identification exchange, the unencrypted binary packets, and the two
/// <c>KEXINIT</c> messages.
/// </summary>
internal sealed class SshTransport
{
    private readonly IConnection connection;

    private readonly SshAlgorithmPreferences preferences;

    private readonly SshAlgorithmCatalogue catalogue;

    private readonly ISshRandomSource randomSource;

    private readonly SshConnectionReader connectionReader;

    /// <summary>
    /// Initializes a new instance of the <see cref="SshTransport" /> class.
    /// </summary>
    /// <param name="connection">The connection to the server.</param>
    /// <param name="preferences">The platform preset, after compression and known-hosts narrowing.</param>
    /// <param name="catalogue">The algorithms this build implements.</param>
    /// <param name="randomSource">Where the cookie and padding bytes come from.</param>
    internal SshTransport(
        IConnection connection,
        SshAlgorithmPreferences preferences,
        SshAlgorithmCatalogue catalogue,
        ISshRandomSource randomSource)
    {
        this.connection = connection;
        this.preferences = preferences;
        this.catalogue = catalogue;
        this.randomSource = randomSource;
        connectionReader = new SshConnectionReader(connection);
        PacketReader = new SshPacketReader(connectionReader);
        PacketWriter = new SshPacketWriter(connection, randomSource);
    }

    /// <summary>
    /// Gets the reader of the server's packets, for the key exchange that follows.
    /// </summary>
    internal SshPacketReader PacketReader { get; }

    /// <summary>
    /// Gets the writer of the client's packets, for the key exchange that follows.
    /// </summary>
    internal SshPacketWriter PacketWriter { get; }

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
        string serverIdentification = await SshIdentificationExchange.ExchangeAsync(connection, connectionReader, cancellationToken).ConfigureAwait(false);
        SshKexInit clientKexInit = SshKexInit.ForClient(preferences, catalogue, randomSource);
        byte[] clientPayload = clientKexInit.ToPayload();
        await PacketWriter.WriteAsync(clientPayload, cancellationToken).ConfigureAwait(false);
        try
        {
            (byte[] serverPayload, int packetsBefore) = await ReadServerKexInitAsync(cancellationToken).ConfigureAwait(false);
            SshKexInit serverKexInit = SshKexInit.Parse(serverPayload);
            SshNegotiatedAlgorithms algorithms = SshAlgorithmNegotiator.Negotiate(clientKexInit, serverKexInit)
                ?? throw SshTransferException.SessionEstablishmentFailed(Libssh2ErrorCode.KeyExchangeFailure, Libssh2ErrorCode.UnableToExchangeEncryptionKeys);
            if (algorithms.IsStrictKeyExchange && packetsBefore > 0)
            {
                throw new InvalidDataException("Strict key exchange allows no packet before the server's KEXINIT.");
            }

            return new SshNegotiatedHandshake(
                SshIdentificationExchange.ClientIdentification,
                serverIdentification,
                clientPayload,
                serverPayload,
                algorithms);
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException)
        {
            throw SshTransferException.SessionEstablishmentFailed(Libssh2ErrorCode.SocketNone, Libssh2ErrorCode.UnableToExchangeEncryptionKeys);
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
