using System.Net.Sockets;
using System.Threading.Channels;
using Curl.Cryptography;
using Curl.Extension.WireGuard.Configuration;
using Curl.Extension.WireGuard.IpStack;
using Curl.Extension.WireGuard.Messages;
using Curl.Extension.WireGuard.Tunnel;
using Curl.Extension.WireGuard.UnitTests.Noise;

namespace Curl.Extension.WireGuard.UnitTests.Tunnel;

/// <summary>
/// The far end of a tunnel, written from the whitepaper: the UDP socket the connector opens, the responder of the
/// handshake behind it, and a TCP server inside the tunnel, each step driven by the test.
/// </summary>
internal sealed class TunnelTestPeer : IWireGuardUdpOpener, IWireGuardUdpChannel
{
    public const uint ServerInitialSequenceNumber = 7000;

    public static readonly byte[] InitiatorPrivateKey = WireGuardTestResponder.NewKey(1);

    public static readonly byte[] ResponderPrivateKey = WireGuardTestResponder.NewKey(2);

    private static readonly byte[] ResponderEphemeralKey = WireGuardTestResponder.NewKey(3);

    private readonly Channel<byte[]> sent = Channel.CreateUnbounded<byte[]>();

    private readonly Channel<byte[]> inbound = Channel.CreateUnbounded<byte[]>();

    private uint initiatorIndex;

    private ulong counter;

    public WireGuardTestResponder Responder { get; } = new(ResponderPrivateKey, PublicKey(InitiatorPrivateKey), new byte[32]);

    public SocketException? OpenFailure { get; init; }

    public WireGuardEndpoint? OpenedEndpoint { get; private set; }

    public bool IsDisposed { get; private set; }

    public static string ConfigurationText(string addresses = "10.0.0.2/32, fd00::2/128", string allowedIps = "10.0.0.0/24, fd00::/64", string extraInterfaceLine = "") =>
        string.Join(
            '\n',
            "[Interface]",
            $"PrivateKey = {Convert.ToBase64String(InitiatorPrivateKey)}",
            $"Address = {addresses}",
            extraInterfaceLine,
            "[Peer]",
            $"PublicKey = {Convert.ToBase64String(PublicKey(ResponderPrivateKey))}",
            "Endpoint = 192.0.2.1:51820",
            $"AllowedIPs = {allowedIps}");

    public static WireGuardConfiguration Configuration(string addresses = "10.0.0.2/32, fd00::2/128", string allowedIps = "10.0.0.0/24, fd00::/64", string extraInterfaceLine = "") =>
        WireGuardConfigurationParser.Parse(ConfigurationText(addresses, allowedIps, extraInterfaceLine));

    public static byte[] PublicKey(byte[] privateKey)
    {
        var publicKey = new byte[32];
        X25519.ComputePublicKey(privateKey, publicKey);
        return publicKey;
    }

    public ValueTask<IWireGuardUdpChannel> OpenAsync(WireGuardEndpoint endpoint, CancellationToken cancellationToken)
    {
        OpenedEndpoint = endpoint;
        return OpenFailure is null ? ValueTask.FromResult<IWireGuardUdpChannel>(this) : ValueTask.FromException<IWireGuardUdpChannel>(OpenFailure);
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken)
    {
        sent.Writer.TryWrite(datagram.ToArray());
        return ValueTask.CompletedTask;
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken) =>
        await inbound.Reader.ReadAsync(cancellationToken);

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }

    public void Deliver(byte[] datagram) => inbound.Writer.TryWrite(datagram);

    public async Task<byte[]> NextSentAsync() => await sent.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

    /// <summary>Takes the next initiation (section 5.4.2), skipping what an earlier tunnel sent last, and answers it with a response (5.4.3).</summary>
    public async Task AcceptHandshakeAsync()
    {
        byte[] initiation;
        do
        {
            initiation = await NextSentAsync();
        }
        while (initiation[0] != 1);

        Assert.IsTrue(Responder.TryConsumeInitiation(initiation));
        Assert.IsTrue(HandshakeInitiationMessage.TryDecode(initiation, out var message));
        initiatorIndex = message.SenderIndex;
        Deliver(Responder.CreateResponse(ResponderEphemeralKey));
    }

    /// <summary>Opens the next transport data message (section 5.4.6) and reads the TCP segment inside it.</summary>
    public async Task<(IpPacket Packet, TcpSegment Segment)> ReceiveSegmentAsync()
    {
        var padded = Responder.OpenTransport(await NextSentAsync());
        Assert.IsNotNull(padded);
        Assert.IsTrue(IpPacketCodec.TryParse(padded, out var packet));
        Assert.IsTrue(TcpSegmentCodec.TryParse(packet!.Payload, packet.Source, packet.Destination, out var segment));
        return (packet, segment!);
    }

    /// <summary>Reads segments until one has every flag of <paramref name="flags" /> or carries data.</summary>
    public async Task<TcpSegment> ReceiveSegmentAsync(TcpFlags flags)
    {
        while (true)
        {
            var (_, segment) = await ReceiveSegmentAsync();
            if ((flags != TcpFlags.None && segment.Flags.HasFlag(flags)) || (flags == TcpFlags.None && segment.Payload.Length > 0))
            {
                return segment;
            }
        }
    }

    /// <summary>Sends a segment back to the sender of <paramref name="received" />, sealed into a transport data message.</summary>
    public void Reply(IpPacket received, TcpSegment segment) =>
        Deliver(SealToInitiator(IpPacketCodec.Build(new IpPacket(received.Destination, received.Source, IpPacketCodec.TcpProtocol, TcpSegmentCodec.Build(segment, received.Destination, received.Source)))));

    /// <summary>Pads <paramref name="packet" /> to 16 bytes and seals it under the next counter.</summary>
    public byte[] SealToInitiator(byte[] packet)
    {
        var padded = new byte[(packet.Length + 15) / 16 * 16];
        packet.CopyTo(padded, 0);
        return Responder.SealTransport(initiatorIndex, counter++, padded);
    }

    /// <summary>Answers the SYN with a SYN-ACK and takes the ACK.</summary>
    public async Task<(IpPacket Packet, TcpSegment Syn)> AcceptTcpAsync()
    {
        var (packet, syn) = await ReceiveSegmentAsync();
        Assert.IsTrue(syn.Flags.HasFlag(TcpFlags.Syn));
        Reply(packet, new TcpSegment(syn.DestinationPort, syn.SourcePort, ServerInitialSequenceNumber, syn.SequenceNumber + 1, TcpFlags.Syn | TcpFlags.Ack, 65535, 1200, []));
        await ReceiveSegmentAsync(TcpFlags.Ack);
        return (packet, syn);
    }
}
