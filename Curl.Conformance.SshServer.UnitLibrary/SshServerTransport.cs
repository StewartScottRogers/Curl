using System.Security.Cryptography;
using System.Text;
using Curl.Cryptography;
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

    /// <summary>The finite-field key exchange the server offers, for clients that offer no Curve25519.</summary>
    private const string FiniteFieldKeyExchange = "diffie-hellman-group14-sha256";

    /// <summary>
    /// The key-exchange methods the server offers: RFC 8731's name and libssh's older one, then
    /// RFC 8268's <c>diffie-hellman-group14-sha256</c>, the first curl's WinCNG build shares.
    /// </summary>
    private static readonly string[] KeyExchangeMethods = ["curve25519-sha256", "curve25519-sha256@libssh.org", FiniteFieldKeyExchange];

    /// <summary>The host-key algorithms the server offers: Ed25519 first, then the RSA key's three signature forms.</summary>
    private static readonly string[] HostKeyAlgorithms = [SshServerHostKey.Algorithm, .. SshServerRsaHostKey.Algorithms];

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
    /// sends a <c>KEXINIT</c> offering <c>curve25519-sha256</c> and
    /// <c>diffie-hellman-group14-sha256</c>, the <see cref="SshServerHostKey" /> and the
    /// <see cref="SshServerRsaHostKey" />, and every cipher and MAC
    /// <see cref="SshPacketProtections" /> implements, so the client's first implemented
    /// choice wins; answers the client's X25519 share or e with its own and a signature over
    /// the exchange hash by the agreed host key; then exchanges <c>NEWKEYS</c>, protecting each direction from its own
    /// <c>NEWKEYS</c> on.
    /// </summary>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The algorithms agreed.</returns>
    /// <exception cref="InvalidDataException">
    /// The client offers nothing the server implements, or sends another message where the
    /// exchange expects its <c>KEXINIT</c>, its share or its <c>NEWKEYS</c>, or sends a group 14
    /// e outside 1 &lt; e &lt; p - 1.
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
        byte[] hostKey = algorithms.ServerHostKey == SshServerHostKey.Algorithm ? SshServerHostKey.Blob : SshServerRsaHostKey.Blob;
        SshExchangeHashInput hashInput = new(handshake, hostKey);
        SshWireWriter reply = new();
        reply.WriteByte(SshMessageNumber.KeyExchangeDiffieHellmanReply);
        reply.WriteString(hostKey);
        byte[] sharedSecret = algorithms.KeyExchange == FiniteFieldKeyExchange
            ? AgreeFiniteField(clientInit, hashInput.Fields, reply)
            : AgreeX25519(clientInit, hashInput.Fields, reply);
        byte[] encodedSharedSecret = SshExchangeHashInput.EncodeMpint(sharedSecret);
        byte[] exchangeHash = hashInput.ComputeHash(encodedSharedSecret, HashAlgorithmName.SHA256);
        reply.WriteString(SignExchangeHash(algorithms.ServerHostKey, exchangeHash));
        await WritePacketAsync(reply.ToArray(), cancellationToken).ConfigureAwait(false);
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
            HostKeyAlgorithms,
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

    /// <summary>
    /// Answers the client's X25519 share (RFC 8731): both shares go into the exchange hash as
    /// strings, and the server's into the reply.
    /// </summary>
    private static byte[] AgreeX25519(SshWireReader clientInit, SshWireWriter hashFields, SshWireWriter reply)
    {
        byte[] clientShare = clientInit.ReadString().ToArray();
        using X25519SshKeyShare serverShare = new(new SystemSshEphemeralKeySource());
        byte[] sharedSecret = serverShare.ComputeSharedSecret(clientShare);
        hashFields.WriteString(clientShare);
        hashFields.WriteString(serverShare.ClientShare);
        reply.WriteString(serverShare.ClientShare);
        return sharedSecret;
    }

    /// <summary>
    /// Answers the client's e in MODP group 14 (RFC 4253 section 8, RFC 8268): e and f go into
    /// the exchange hash as mpints, and f into the reply.
    /// </summary>
    private static byte[] AgreeFiniteField(SshWireReader clientInit, SshWireWriter hashFields, SshWireWriter reply)
    {
        byte[] clientPublicValue = clientInit.ReadMpint().ToArray();
        using FiniteFieldDiffieHellman serverKey = new SystemSshEphemeralKeySource().CreateFiniteFieldKey(FiniteFieldDiffieHellmanGroup.Group14);
        byte[] serverPublicValue = new byte[serverKey.Group.PrimeLength];
        serverKey.ComputePublicValue(serverPublicValue);
        byte[] sharedSecret = new byte[serverKey.Group.PrimeLength];
        if (!serverKey.TryComputeSharedSecret(clientPublicValue, sharedSecret))
        {
            throw new InvalidDataException("The SSH client's Diffie-Hellman public value e is not in 1 < e < p - 1.");
        }

        hashFields.WriteMpint(clientPublicValue);
        hashFields.WriteMpint(serverPublicValue);
        reply.WriteMpint(serverPublicValue);
        return sharedSecret;
    }

    private static byte[] SignExchangeHash(string hostKeyAlgorithm, byte[] exchangeHash) =>
        hostKeyAlgorithm == SshServerHostKey.Algorithm
            ? SshServerHostKey.Sign(exchangeHash)
            : SshServerRsaHostKey.Sign(hostKeyAlgorithm, exchangeHash);

    private async ValueTask<byte[]> ReadMessageAsync(byte expectedMessageNumber, CancellationToken cancellationToken)
    {
        byte[] payload = await ReadPacketAsync(cancellationToken).ConfigureAwait(false);
        return payload[0] == expectedMessageNumber
            ? payload
            : throw new InvalidDataException($"The SSH client sent message {payload[0]} where the server expects {expectedMessageNumber}.");
    }
}
