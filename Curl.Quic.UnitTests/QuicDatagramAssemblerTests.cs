using Curl.Tls;

namespace Curl.Quic;

[TestClass]
public sealed class QuicDatagramAssemblerTests
{
    private static readonly byte[] HandshakeDestination = [1, 1, 1, 1, 1, 1, 1, 1];

    private static readonly byte[] OneRttDestination = [2, 2, 2, 2, 2, 2, 2, 2];

    private static readonly QuicPacketAddress Address = new(HandshakeDestination, OneRttDestination, [3, 3, 3, 3, 3, 3, 3, 3], ReadOnlyMemory<byte>.Empty);

    [TestMethod]
    public void Assemble_HandshakeAndOneRttToDifferentConnectionIds_SendsThemInSeparateDatagrams()
    {
        using QuicPacketNumberSpace handshake = Space(QuicPacketType.Handshake);
        using QuicPacketNumberSpace application = Space(QuicPacketType.OneRtt);
        handshake.QueueFrame(new QuicPingFrame());
        application.QueueFrame(new QuicPingFrame());

        QuicAssembly assembly = QuicDatagramAssembler.Assemble([handshake, application], Address, TimeSpan.FromMilliseconds(7), long.MaxValue);

        Assert.HasCount(2, assembly.Datagrams);
        Assert.AreEqual(0x80, QuicHeaderForm(assembly.Datagrams[0]));
        Assert.AreEqual(0x00, QuicHeaderForm(assembly.Datagrams[1]));
        Assert.AreEqual(TimeSpan.FromMilliseconds(7), assembly.Packets[0].Packet.TimeSent);
    }

    [TestMethod]
    public void Assemble_CongestionAllowanceUsed_LeavesTheRestQueuedButLetsAnAcknowledgementGo()
    {
        using QuicPacketNumberSpace handshake = Space(QuicPacketType.Handshake);
        using QuicPacketNumberSpace application = Space(QuicPacketType.OneRtt);
        handshake.QueueCrypto(new byte[3000]);
        application.RecordReceived(0, TimeSpan.Zero);
        application.RequireAcknowledgement();

        QuicAssembly assembly = QuicDatagramAssembler.Assemble([handshake, application], Address, TimeSpan.Zero, 1);

        Assert.HasCount(2, assembly.Packets);
        Assert.AreEqual(handshake, assembly.Packets[0].Space);
        Assert.AreEqual(application, assembly.Packets[1].Space);
        Assert.IsTrue(handshake.HasFramesToSend);
        Assert.IsTrue(assembly.Packets[0].Packet.IsAckEliciting);
        Assert.IsTrue(assembly.Packets[0].Packet.IsInFlight);
        Assert.IsFalse(assembly.Packets[1].Packet.IsAckEliciting);
        Assert.IsFalse(assembly.Packets[1].Packet.IsInFlight);
        Assert.AreEqual(assembly.Datagrams[1].Length, assembly.Packets[1].Packet.SentBytes);
    }

    [TestMethod]
    public void Assemble_AckOnlyInitial_IsPaddedAndSoInFlight()
    {
        using QuicPacketNumberSpace initial = new(QuicPacketType.Initial)
        {
            SendProtection = QuicPacketProtection.CreateClientInitial(HandshakeDestination),
        };
        initial.RecordReceived(0, TimeSpan.Zero);
        initial.RequireAcknowledgement();

        QuicAssembly assembly = QuicDatagramAssembler.Assemble([initial], Address, TimeSpan.Zero, 0);

        Assert.AreEqual(1200, assembly.Datagrams.Single().Length);
        (_, QuicSentPacket packet) = assembly.Packets.Single();
        Assert.IsFalse(packet.IsAckEliciting);
        Assert.IsTrue(packet.IsInFlight);
        Assert.AreEqual(1200, packet.SentBytes);
    }

    private static int QuicHeaderForm(byte[] datagram) => datagram[0] & 0x80;

    private static QuicPacketNumberSpace Space(QuicPacketType type) =>
        new(type) { SendProtection = QuicPacketProtection.Create(Tls13CipherSuite.ChaCha20Poly1305Sha256, new byte[32]) };
}
