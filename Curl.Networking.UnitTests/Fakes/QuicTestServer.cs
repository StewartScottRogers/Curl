using System.Security.Cryptography;

using Curl.Quic;
using Curl.Tls;

namespace Curl.Networking.Fakes;

/// <summary>
/// A copy of <c>Curl.Quic.UnitTests</c>' in-memory QUIC server, so <see cref="QuicDialer" /> is tested
/// against a real handshake (BL-728): a server built from the library's own pieces - packet numbers spaces,
/// packet protection, the datagram assembler, the CRYPTO reassembler and the transport
/// parameters - over <see cref="QuicTestTlsServer" />. It answers the client's first
/// Initial with a Version Negotiation packet, a Retry, a CONNECTION_CLOSE or the handshake,
/// checks the client's Finished, then sends <see cref="FramesAfterHandshake" /> in 1-RTT.
/// Every packet the client sends is decrypted and kept in <see cref="ClientPackets" />.
/// </summary>
internal sealed class QuicTestServer : IDisposable
{
    public const int ConnectionIdLength = 8;

    private readonly QuicPacketNumberSpace initial = new(QuicPacketType.Initial);

    private readonly QuicPacketNumberSpace handshake = new(QuicPacketType.Handshake);

    private readonly QuicPacketNumberSpace application = new(QuicPacketType.OneRtt);

    private readonly List<byte> clientHello = [];

    private QuicTestTlsServer? tls;

    private byte[]? clientConnectionId;

    public QuicTestServer() => ServerConnectionId = RandomNumberGenerator.GetBytes(ConnectionIdLength);

    /// <summary>Gets the cipher suite the server chooses.</summary>
    public ushort CipherSuite { get; init; } = 0x1301;

    /// <summary>Gets the versions a Version Negotiation packet lists in answer to the first Initial, or <see langword="null" /> to answer it normally.</summary>
    public IReadOnlyList<uint>? VersionNegotiation { get; init; }

    /// <summary>Gets a value indicating whether the server answers the first Initial without a token with a Retry.</summary>
    public bool SendRetry { get; init; }

    /// <summary>Gets the token a Retry carries.</summary>
    public byte[] RetryToken { get; init; } = "retry-token"u8.ToArray();

    /// <summary>Gets the transport error a CONNECTION_CLOSE carries in answer to the ClientHello, or <see langword="null" /> to answer it with the handshake.</summary>
    public ulong? CloseAfterClientHello { get; init; }

    /// <summary>Gets the ALPN protocol the server chooses, or <see langword="null" /> for none.</summary>
    public string? ApplicationProtocol { get; init; } = "h3";

    /// <summary>Gets how the server's transport parameters are encoded; <see langword="null" /> sends no extension.</summary>
    public Func<QuicTransportParameters, byte[]?> EncodeTransportParameters { get; init; } = parameters => parameters.Encode();

    /// <summary>Gets what changes the server's transport parameters before they are encoded, such as its stream and flow control limits.</summary>
    public Func<QuicTransportParameters, QuicTransportParameters> ConfigureTransportParameters { get; init; } = parameters => parameters;

    /// <summary>Gets the frames the server sends in 1-RTT once the client's Finished checks out.</summary>
    public Func<QuicTestServer, IEnumerable<QuicFrame>> FramesAfterHandshake { get; init; } = _ => [new QuicHandshakeDoneFrame()];

    /// <summary>Gets a value indicating whether the server asks for a client certificate.</summary>
    public bool RequestClientCertificate { get; init; }

    /// <summary>Gets a value indicating whether a NewSessionTicket follows the handshake in a 1-RTT CRYPTO frame.</summary>
    public bool SendSessionTicket { get; init; }

    public byte[] ServerConnectionId { get; }

    public byte[]? OriginalDestinationConnectionId { get; private set; }

    public byte[]? RetrySourceConnectionId { get; private set; }

    public byte[] StatelessResetToken { get; } = RandomNumberGenerator.GetBytes(16);

    public QuicTestTlsServer? Tls => tls;

    /// <summary>Gets each packet the client sent, decrypted, with its datagram's index.</summary>
    public List<(int Datagram, QuicPacketType Type, IReadOnlyList<QuicFrame> Frames)> ClientPackets { get; } = [];

    public int DatagramsReceived { get; private set; }

    public bool ClientFinishedReceived { get; private set; }

    public QuicPacketAddress Address => new(clientConnectionId!, clientConnectionId!, ServerConnectionId, ReadOnlyMemory<byte>.Empty);

    /// <summary>Takes one datagram from the client and returns the datagrams to send back.</summary>
    public IReadOnlyList<byte[]> Receive(byte[] datagram)
    {
        DatagramsReceived++;
        QuicPacket first = QuicPacketCodec.Decode(datagram, ConnectionIdLength).Packet;
        if (clientConnectionId is null && first is QuicLongHeaderPacket firstInitial && Accept(firstInitial) is { } answer)
        {
            return [answer];
        }

        ReceivePackets(datagram);
        return QuicDatagramAssembler.Assemble([initial, handshake, application], Address, TimeSpan.Zero, long.MaxValue).Datagrams;
    }

    /// <summary>Protects frames in one packet of <paramref name="type" />, for a test to hand the client directly.</summary>
    public byte[] Protect(QuicPacketType type, params QuicFrame[] frames)
    {
        QuicPacketNumberSpace space = SpaceOf(type);
        foreach (QuicFrame frame in frames)
        {
            space.QueueFrame(frame);
        }

        return QuicDatagramAssembler.Assemble([space], Address, TimeSpan.Zero, long.MaxValue).Datagrams.Single();
    }

    public void Dispose()
    {
        tls?.Dispose();
        initial.Dispose();
        handshake.Dispose();
        application.Dispose();
    }

    private static bool IsAckEliciting(QuicFrame frame) => frame is not (QuicAckFrame or QuicPaddingFrame or QuicConnectionCloseFrame);

    private QuicPacketNumberSpace SpaceOf(QuicPacketType type) => type switch
    {
        QuicPacketType.Initial => initial,
        QuicPacketType.Handshake => handshake,
        _ => application,
    };

    private byte[]? Accept(QuicLongHeaderPacket first)
    {
        if (VersionNegotiation is { } versions)
        {
            return QuicPacketCodec.Encode(new QuicVersionNegotiationPacket(first.SourceConnectionId, first.DestinationConnectionId, versions));
        }

        OriginalDestinationConnectionId ??= first.DestinationConnectionId.ToArray();
        if (SendRetry && first.Token.IsEmpty)
        {
            RetrySourceConnectionId = RandomNumberGenerator.GetBytes(ConnectionIdLength);
            QuicRetryPacket retry = new(QuicPacketCodec.Version1, first.SourceConnectionId, RetrySourceConnectionId, RetryToken, new byte[QuicRetryPacket.RetryIntegrityTagLength]);
            return QuicPacketCodec.Encode(retry with { RetryIntegrityTag = QuicRetryIntegrity.ComputeTag(OriginalDestinationConnectionId, retry) });
        }

        clientConnectionId = first.SourceConnectionId.ToArray();
        initial.ReceiveProtection = QuicPacketProtection.CreateClientInitial(first.DestinationConnectionId.Span);
        initial.SendProtection = QuicPacketProtection.CreateServerInitial(first.DestinationConnectionId.Span);
        return null;
    }

    private void ReceivePackets(byte[] datagram)
    {
        ReadOnlyMemory<byte> remaining = datagram;
        while (!remaining.IsEmpty)
        {
            QuicPacket header = QuicPacketCodec.Decode(remaining, ConnectionIdLength).Packet;
            QuicPacketNumberSpace space = SpaceOf(header is QuicLongHeaderPacket longHeader ? longHeader.Type : QuicPacketType.OneRtt);
            QuicUnprotectResult result = space.ReceiveProtection!.Unprotect(remaining, ConnectionIdLength, space.LargestReceived);
            Assert.AreEqual(QuicUnprotectStatus.Unprotected, result.Status);
            space.RecordReceived(result.PacketNumber, TimeSpan.Zero);
            ReadOnlyMemory<byte> payload = result.Packet is QuicLongHeaderPacket protectedLong ? protectedLong.Payload : ((QuicShortHeaderPacket)result.Packet!).Payload;
            IReadOnlyList<QuicFrame> frames = QuicFrameCodec.Decode(payload, space.PacketType);
            ClientPackets.Add((DatagramsReceived, space.PacketType, frames));
            if (frames.Any(IsAckEliciting))
            {
                space.RequireAcknowledgement();
            }

            foreach (QuicCryptoFrame crypto in frames.OfType<QuicCryptoFrame>())
            {
                ReceiveCrypto(space, space.CryptoReceived.Receive(crypto));
            }

            remaining = remaining[result.Length..];
        }
    }

    // A repeated ClientHello (a retransmission) brings no new bytes and is not answered again.
    private void ReceiveCrypto(QuicPacketNumberSpace space, byte[] bytes)
    {
        if (space == initial && bytes.Length > 0)
        {
            clientHello.AddRange(bytes);
            if (HandshakeMessageReader.Read(clientHello.ToArray()).Message is not null)
            {
                Answer();
            }
        }
        else if (space == handshake && bytes.Length > 0)
        {
            if (!tls!.ReceiveClientFlight(bytes))
            {
                return;
            }

            ClientFinishedReceived = true;
            AfterHandshake();
        }
    }

    private void Answer()
    {
        if (CloseAfterClientHello is { } errorCode)
        {
            initial.QueueFrame(new QuicConnectionCloseFrame(errorCode, 0x06, "refused"u8.ToArray()));
            return;
        }

        tls = new QuicTestTlsServer(CipherSuite, RequestClientCertificate);
        QuicTransportParameters parameters = new()
        {
            OriginalDestinationConnectionId = OriginalDestinationConnectionId,
            InitialSourceConnectionId = ServerConnectionId,
            RetrySourceConnectionId = RetrySourceConnectionId,
            StatelessResetToken = StatelessResetToken,
            MaxIdleTimeout = 30000,
            ActiveConnectionIdLimit = 4,
            InitialMaxData = 1 << 20,
        };
        (byte[] serverHello, byte[] flight) = tls.Answer([.. clientHello], ApplicationProtocol, EncodeTransportParameters(ConfigureTransportParameters(parameters)));
        initial.QueueCrypto(serverHello);
        handshake.SendProtection = QuicPacketProtection.Create(tls.Suite, tls.ServerHandshakeSecret);
        handshake.ReceiveProtection = QuicPacketProtection.Create(tls.Suite, tls.ClientHandshakeSecret);
        handshake.QueueCrypto(flight);
        application.SendProtection = QuicPacketProtection.Create(tls.Suite, tls.ServerApplicationSecret);
        application.ReceiveProtection = QuicPacketProtection.Create(tls.Suite, tls.ClientApplicationSecret);
    }

    private void AfterHandshake()
    {
        foreach (QuicFrame frame in FramesAfterHandshake(this))
        {
            application.QueueFrame(frame);
        }

        if (SendSessionTicket)
        {
            application.QueueCrypto(new NewSessionTicket(7200, 1, [0], [1, 2, 3], []).Encode());
        }
    }
}
