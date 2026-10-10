using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh;
using Curl.Protocol.Ssh.KeyExchange;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Protocol.Ssh.PacketProtection;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Conformance.SshServer;

/// <summary>
/// The server's side of one SSH connection's transport layer (RFC 4253): the identification
/// exchange, then packets framed by the SSH client's own <see cref="SshPacketReader" /> and
/// <see cref="SshPacketWriter" />, unencrypted until <see cref="ExchangeKeysAsync" /> switches each direction to its new keys.
/// </summary>
internal sealed class SshServerTransport
{
    /// <summary>
    /// The identification string the server sends: OpenSSH's, as the SSH client's tests
    /// already answer with.
    /// </summary>
    internal const string ServerIdentification = "SSH-2.0-OpenSSH_9.7";

    /// <summary>The only service the server offers (RFC 4252).</summary>
    internal const string UserAuthService = "ssh-userauth";

    private const string NoCompression = "none";

    /// <summary>The key-exchange methods the server offers: RFC 8731's name and libssh's older one.</summary>
    private static readonly string[] KeyExchangeMethods = ["curve25519-sha256", "curve25519-sha256@libssh.org"];

    private readonly IConnection connection;

    private readonly SshConnectionReader connectionReader;

    private readonly SshPacketReader packetReader;

    private readonly SshPacketWriter packetWriter;

    /// <summary>
    /// Wraps the server's end of a connection.
    /// </summary>
    /// <param name="connection">The server's end.</param>
    /// <param name="randomSource">Where packet padding comes from.</param>
    internal SshServerTransport(IConnection connection, ISshRandomSource randomSource)
    {
        this.connection = connection;
        connectionReader = new SshConnectionReader(connection);
        packetReader = new SshPacketReader(connectionReader);
        packetWriter = new SshPacketWriter(connection, randomSource);
    }

    /// <summary>
    /// Gets the client's identification string, once <see cref="ExchangeIdentificationAsync" /> has read it.
    /// </summary>
    internal string? ClientIdentification { get; private set; }

    /// <summary>
    /// Gets the session identifier, the first exchange hash, once <see cref="ExchangeKeysAsync" /> has finished.
    /// </summary>
    internal byte[]? SessionIdentifier { get; private set; }

    /// <summary>
    /// Sends <see cref="ServerIdentification" /> and reads the client's identification line,
    /// skipping any line before it that does not start with <c>SSH-</c>.
    /// </summary>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The client's identification string.</returns>
    /// <exception cref="IOException">The client closed the connection before identifying itself.</exception>
    internal async ValueTask<string> ExchangeIdentificationAsync(CancellationToken cancellationToken)
    {
        await connection.WriteAsync(Encoding.ASCII.GetBytes(ServerIdentification + "\r\n"), cancellationToken).ConfigureAwait(false);
        await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
        string line;
        do
        {
            line = await connectionReader.ReadLineAsync(SshIdentificationExchange.MaximumLineLength, cancellationToken).ConfigureAwait(false)
                ?? throw new IOException("The SSH client closed the connection before sending its identification.");
        }
        while (!line.StartsWith("SSH-", StringComparison.Ordinal));

        ClientIdentification = line;
        return line;
    }

    /// <summary>
    /// Reads the next packet's payload, message number first.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The payload.</returns>
    internal ValueTask<byte[]> ReadPacketAsync(CancellationToken cancellationToken) => packetReader.ReadAsync(cancellationToken);

    /// <summary>
    /// Frames and sends one payload, message number first.
    /// </summary>
    /// <param name="payload">The payload.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the packet is written.</returns>
    internal ValueTask WritePacketAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) => packetWriter.WriteAsync(payload, cancellationToken);

    /// <summary>
    /// Runs the first key exchange as the server (RFC 4253 sections 7 and 8, RFC 8731):
    /// sends a <c>KEXINIT</c> offering <c>curve25519-sha256</c>, the
    /// <see cref="SshServerHostKey" /> and every cipher and MAC
    /// <see cref="SshPacketProtections" /> implements, so the client's first implemented
    /// choice wins; answers the client's X25519 share with its own and a signature over the
    /// exchange hash; then exchanges <c>NEWKEYS</c>, protecting each direction from its own
    /// <c>NEWKEYS</c> on.
    /// </summary>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The algorithms agreed.</returns>
    /// <exception cref="InvalidDataException">
    /// The client offers nothing the server implements, or sends another message where the
    /// exchange expects its <c>KEXINIT</c>, its share or its <c>NEWKEYS</c>.
    /// </exception>
    internal async ValueTask<SshNegotiatedAlgorithms> ExchangeKeysAsync(CancellationToken cancellationToken)
    {
        byte[] serverKexInit = CreateServerKexInit().ToPayload();
        await WritePacketAsync(serverKexInit, cancellationToken).ConfigureAwait(false);
        byte[] clientKexInit = await ReadMessageAsync(SshMessageNumber.KeyExchangeInit, cancellationToken).ConfigureAwait(false);
        SshNegotiatedAlgorithms algorithms = SshAlgorithmNegotiator.Negotiate(SshKexInit.Parse(clientKexInit), SshKexInit.Parse(serverKexInit), [])
            ?? throw new InvalidDataException("The SSH client offers no algorithm list the server shares.");
        SshNegotiatedHandshake handshake = new(ClientIdentification!, ServerIdentification, clientKexInit, serverKexInit, algorithms);
        SshWireReader clientInit = new(await ReadMessageAsync(SshMessageNumber.KeyExchangeDiffieHellmanInit, cancellationToken).ConfigureAwait(false));
        clientInit.ReadByte();
        byte[] clientShare = clientInit.ReadString().ToArray();
        using X25519SshKeyShare serverShare = new(new SystemSshEphemeralKeySource());
        byte[] encodedSharedSecret = SshExchangeHashInput.EncodeMpint(serverShare.ComputeSharedSecret(clientShare));
        SshExchangeHashInput hashInput = new(handshake, SshServerHostKey.Blob);
        hashInput.Fields.WriteString(clientShare);
        hashInput.Fields.WriteString(serverShare.ClientShare);
        byte[] exchangeHash = hashInput.ComputeHash(encodedSharedSecret, HashAlgorithmName.SHA256);
        await WritePacketAsync(CreateReply(serverShare.ClientShare, exchangeHash), cancellationToken).ConfigureAwait(false);
        SshKeyDerivation keys = new(HashAlgorithmName.SHA256, encodedSharedSecret, exchangeHash, exchangeHash);
        await WritePacketAsync(new[] { SshMessageNumber.NewKeys }, cancellationToken).ConfigureAwait(false);
        packetWriter.ChangeProtection(SshPacketProtections.ForServerToClient(algorithms, keys));
        await ReadMessageAsync(SshMessageNumber.NewKeys, cancellationToken).ConfigureAwait(false);
        packetReader.ChangeProtection(SshPacketProtections.ForClientToServer(algorithms, keys));
        SessionIdentifier = exchangeHash;
        return algorithms;
    }

    /// <summary>
    /// Reads the client's <c>SERVICE_REQUEST</c> and accepts it with <c>SERVICE_ACCEPT</c>
    /// when it names <c>ssh-userauth</c>, the only service the server offers.
    /// </summary>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>A task that completes once the acceptance is sent.</returns>
    /// <exception cref="InvalidDataException">The client sends another message, or asks for another service.</exception>
    internal async ValueTask AcceptServiceRequestAsync(CancellationToken cancellationToken)
    {
        SshWireReader request = new(await ReadMessageAsync(SshMessageNumber.ServiceRequest, cancellationToken).ConfigureAwait(false));
        request.ReadByte();
        string service = request.ReadName();
        if (service != UserAuthService)
        {
            throw new InvalidDataException($"The SSH client asks for the service '{service}', not '{UserAuthService}'.");
        }

        SshWireWriter accept = new();
        accept.WriteByte(SshMessageNumber.ServiceAccept);
        accept.WriteString(Encoding.ASCII.GetBytes(UserAuthService));
        await WritePacketAsync(accept.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    private static SshKexInit CreateServerKexInit()
    {
        string[] protections = [.. SshPacketProtections.Names];
        return new SshKexInit(
            RandomNumberGenerator.GetBytes(SshKexInit.CookieLength),
            KeyExchangeMethods,
            [SshServerHostKey.Algorithm],
            protections,
            protections,
            protections,
            protections,
            [NoCompression],
            [NoCompression],
            [],
            [],
            FirstKexPacketFollows: false);
    }

    private static byte[] CreateReply(byte[] serverShare, byte[] exchangeHash)
    {
        SshWireWriter reply = new();
        reply.WriteByte(SshMessageNumber.KeyExchangeDiffieHellmanReply);
        reply.WriteString(SshServerHostKey.Blob);
        reply.WriteString(serverShare);
        reply.WriteString(SshServerHostKey.Sign(exchangeHash));
        return reply.ToArray();
    }

    private async ValueTask<byte[]> ReadMessageAsync(byte expectedMessageNumber, CancellationToken cancellationToken)
    {
        byte[] payload = await ReadPacketAsync(cancellationToken).ConfigureAwait(false);
        return payload[0] == expectedMessageNumber
            ? payload
            : throw new InvalidDataException($"The SSH client sent message {payload[0]} where the server expects {expectedMessageNumber}.");
    }
}
