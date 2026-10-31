using System.Net;
using Curl.Extension.WireGuard.IpStack;

namespace Curl.Extension.WireGuard.UnitTests.IpStack;

/// <summary>The far end of a <see cref="TcpConnection" /> under test: it sends scripted segments and reads the client's.</summary>
internal sealed class ScriptedTcpPeer(IPEndPoint client, IPEndPoint server)
{
    public const uint ClientInitialSequenceNumber = 100;
    public const uint ServerInitialSequenceNumber = 5000;
    public const ushort ClientMaximumSegmentSize = 1360;

    public ScriptedTcpPeer()
        : this(new IPEndPoint(IPAddress.Parse("10.0.0.2"), 50000), new IPEndPoint(IPAddress.Parse("10.0.0.1"), 80))
    {
    }

    public IPEndPoint Client { get; } = client;

    public IPEndPoint Server { get; } = server;

    public ScriptedPacketChannel Channel { get; } = new();

    public ManualTimeProvider Clock { get; } = new();

    public TcpConnectionParameters Parameters => new(Client, Server, ClientInitialSequenceNumber, ClientMaximumSegmentSize);

    /// <summary>The server's segment, addressed to the client.</summary>
    public TcpSegment Segment(TcpFlags flags, uint sequence, uint acknowledgement, byte[]? payload = null, ushort window = 65535, ushort? maximumSegmentSize = null) =>
        new((ushort)Server.Port, (ushort)Client.Port, sequence, acknowledgement, flags, window, maximumSegmentSize, payload ?? []);

    /// <summary>The IP packet carrying <paramref name="segment" /> from the server to the client.</summary>
    public byte[] Packet(TcpSegment segment) =>
        IpPacketCodec.Build(new IpPacket(Server.Address, Client.Address, IpPacketCodec.TcpProtocol, TcpSegmentCodec.Build(segment, Server.Address, Client.Address)));

    /// <summary>Delivers the server's segment to the client.</summary>
    public void Send(TcpFlags flags, uint sequence, uint acknowledgement, byte[]? payload = null, ushort window = 65535, ushort? maximumSegmentSize = null) =>
        Channel.Deliver(Packet(Segment(flags, sequence, acknowledgement, payload, window, maximumSegmentSize)));

    /// <summary>Waits for the client's next segment and checks that its IP and TCP checksums hold.</summary>
    public async Task<TcpSegment> ReceiveAsync()
    {
        var bytes = await Channel.NextSentAsync();
        Assert.IsTrue(IpPacketCodec.TryParse(bytes, out var packet));
        Assert.AreEqual(Client.Address, packet!.Source);
        Assert.AreEqual(Server.Address, packet.Destination);
        Assert.IsTrue(TcpSegmentCodec.TryParse(packet.Payload, packet.Source, packet.Destination, out var segment));
        return segment!;
    }

    /// <summary>Runs the handshake: takes the SYN, answers SYN-ACK and takes the ACK.</summary>
    public async Task<TcpConnection> ConnectAsync(ushort window = 65535, ushort? maximumSegmentSize = 1400)
    {
        var connecting = TcpConnection.ConnectAsync(Channel, Parameters, Clock, CancellationToken.None);
        await ReceiveAsync();
        Send(TcpFlags.Syn | TcpFlags.Ack, ServerInitialSequenceNumber, ClientInitialSequenceNumber + 1, window: window, maximumSegmentSize: maximumSegmentSize);
        await ReceiveAsync();
        return await connecting;
    }
}
