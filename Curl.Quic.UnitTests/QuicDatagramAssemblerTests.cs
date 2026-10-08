using System.Globalization;
using Curl.Testing;
using Curl.Tls;

namespace Curl.Quic;

[TestClass]
public sealed class QuicDatagramAssemblerTests
{
    private static readonly byte[] HandshakeDestination = [1, 1, 1, 1, 1, 1, 1, 1];

    private static readonly byte[] OneRttDestination = [2, 2, 2, 2, 2, 2, 2, 2];

    private static readonly QuicPacketAddress Address = new(HandshakeDestination, OneRttDestination, [3, 3, 3, 3, 3, 3, 3, 3], ReadOnlyMemory<byte>.Empty);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Assemble_HandshakeAndOneRttToDifferentConnectionIds_SendsThemInSeparateDatagrams()
    {
        Diagnostics.Arrange("handshake destination", QuicTest.HexOf(HandshakeDestination));
        Diagnostics.Arrange("one-rtt destination", QuicTest.HexOf(OneRttDestination));
        Diagnostics.Arrange("queued", "one ping frame in each of a handshake and an application space");
        Diagnostics.Arrange("now ms", 7);
        using QuicPacketNumberSpace handshake = Space(QuicPacketType.Handshake);
        using QuicPacketNumberSpace application = Space(QuicPacketType.OneRtt);
        handshake.QueueFrame(new QuicPingFrame());
        application.QueueFrame(new QuicPingFrame());

        QuicAssembly assembly = QuicDatagramAssembler.Assemble([handshake, application], Address, TimeSpan.FromMilliseconds(7), long.MaxValue);

        Diagnostics.Act("datagram count", assembly.Datagrams.Count);
        Diagnostics.Act("first datagram header form bit", QuicHeaderForm(assembly.Datagrams[0]));
        Diagnostics.Act("second datagram header form bit", QuicHeaderForm(assembly.Datagrams[1]));
        Diagnostics.Act("first packet time sent ms", assembly.Packets[0].Packet.TimeSent.TotalMilliseconds);
        Diagnostics.Assert("datagram count", 2, assembly.Datagrams.Count);
        Assert.HasCount(2, assembly.Datagrams);
        Diagnostics.Assert("first datagram header form bit", 0x80, QuicHeaderForm(assembly.Datagrams[0]));
        Assert.AreEqual(0x80, QuicHeaderForm(assembly.Datagrams[0]));
        Diagnostics.Assert("second datagram header form bit", 0x00, QuicHeaderForm(assembly.Datagrams[1]));
        Assert.AreEqual(0x00, QuicHeaderForm(assembly.Datagrams[1]));
        Diagnostics.Assert("first packet time sent ms", 7.0, assembly.Packets[0].Packet.TimeSent.TotalMilliseconds);
        Assert.AreEqual(TimeSpan.FromMilliseconds(7), assembly.Packets[0].Packet.TimeSent);
    }

    [TestMethod]
    public void Assemble_CongestionAllowanceUsed_LeavesTheRestQueuedButLetsAnAcknowledgementGo()
    {
        Diagnostics.Arrange("queued", "3000 bytes of handshake crypto; application packet 0 received and acknowledgement required");
        Diagnostics.Arrange("congestion allowance bytes", 1);
        using QuicPacketNumberSpace handshake = Space(QuicPacketType.Handshake);
        using QuicPacketNumberSpace application = Space(QuicPacketType.OneRtt);
        handshake.QueueCrypto(new byte[3000]);
        application.RecordReceived(0, TimeSpan.Zero);
        application.RequireAcknowledgement();

        QuicAssembly assembly = QuicDatagramAssembler.Assemble([handshake, application], Address, TimeSpan.Zero, 1);

        Diagnostics.Act("packet count", assembly.Packets.Count);
        Diagnostics.Act("handshake still has frames to send", handshake.HasFramesToSend);
        Diagnostics.Act("packet 0 ack-eliciting, in flight", $"{assembly.Packets[0].Packet.IsAckEliciting}, {assembly.Packets[0].Packet.IsInFlight}");
        Diagnostics.Act("packet 1 ack-eliciting, in flight", $"{assembly.Packets[1].Packet.IsAckEliciting}, {assembly.Packets[1].Packet.IsInFlight}");
        Diagnostics.Act("packet 1 sent bytes", assembly.Packets[1].Packet.SentBytes);
        Diagnostics.Act("datagram 1 length", assembly.Datagrams[1].Length);
        Diagnostics.Assert("packet count", 2, assembly.Packets.Count);
        Assert.HasCount(2, assembly.Packets);
        Diagnostics.Assert("packet 0 is the handshake space", true, ReferenceEquals(handshake, assembly.Packets[0].Space));
        Assert.AreEqual(handshake, assembly.Packets[0].Space);
        Diagnostics.Assert("packet 1 is the application space", true, ReferenceEquals(application, assembly.Packets[1].Space));
        Assert.AreEqual(application, assembly.Packets[1].Space);
        Diagnostics.Assert("handshake still has frames to send", true, handshake.HasFramesToSend);
        Assert.IsTrue(handshake.HasFramesToSend);
        Diagnostics.Assert("packet 0 ack-eliciting", true, assembly.Packets[0].Packet.IsAckEliciting);
        Assert.IsTrue(assembly.Packets[0].Packet.IsAckEliciting);
        Diagnostics.Assert("packet 0 in flight", true, assembly.Packets[0].Packet.IsInFlight);
        Assert.IsTrue(assembly.Packets[0].Packet.IsInFlight);
        Diagnostics.Assert("packet 1 ack-eliciting", false, assembly.Packets[1].Packet.IsAckEliciting);
        Assert.IsFalse(assembly.Packets[1].Packet.IsAckEliciting);
        Diagnostics.Assert("packet 1 in flight", false, assembly.Packets[1].Packet.IsInFlight);
        Assert.IsFalse(assembly.Packets[1].Packet.IsInFlight);
        Diagnostics.Assert("packet 1 sent bytes", assembly.Datagrams[1].Length, assembly.Packets[1].Packet.SentBytes);
        Assert.AreEqual(assembly.Datagrams[1].Length, assembly.Packets[1].Packet.SentBytes);
    }

    [TestMethod]
    public void Assemble_AckOnlyInitial_IsPaddedAndSoInFlight()
    {
        Diagnostics.Arrange("destination", QuicTest.HexOf(HandshakeDestination));
        Diagnostics.Arrange("queued", "initial packet 0 received and acknowledgement required");
        Diagnostics.Arrange("congestion allowance bytes", 0);
        using QuicPacketNumberSpace initial = new(QuicPacketType.Initial)
        {
            SendProtection = QuicPacketProtection.CreateClientInitial(HandshakeDestination),
        };
        initial.RecordReceived(0, TimeSpan.Zero);
        initial.RequireAcknowledgement();

        QuicAssembly assembly = QuicDatagramAssembler.Assemble([initial], Address, TimeSpan.Zero, 0);

        Diagnostics.Act("datagram length", assembly.Datagrams.Single().Length);
        (_, QuicSentPacket packet) = assembly.Packets.Single();
        Diagnostics.Act("ack-eliciting, in flight, sent bytes", string.Create(CultureInfo.InvariantCulture, $"{packet.IsAckEliciting}, {packet.IsInFlight}, {packet.SentBytes}"));
        Diagnostics.Assert("datagram length", 1200, assembly.Datagrams.Single().Length);
        Assert.AreEqual(1200, assembly.Datagrams.Single().Length);
        Diagnostics.Assert("ack-eliciting", false, packet.IsAckEliciting);
        Assert.IsFalse(packet.IsAckEliciting);
        Diagnostics.Assert("in flight", true, packet.IsInFlight);
        Assert.IsTrue(packet.IsInFlight);
        Diagnostics.Assert("sent bytes", 1200, packet.SentBytes);
        Assert.AreEqual(1200, packet.SentBytes);
    }

    private static int QuicHeaderForm(byte[] datagram) => datagram[0] & 0x80;

    private static QuicPacketNumberSpace Space(QuicPacketType type) =>
        new(type) { SendProtection = QuicPacketProtection.Create(Tls13CipherSuite.ChaCha20Poly1305Sha256, new byte[32]) };
}
