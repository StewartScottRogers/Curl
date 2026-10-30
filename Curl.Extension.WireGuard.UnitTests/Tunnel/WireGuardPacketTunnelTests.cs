using System.Security.Cryptography;
using Curl.Extension.WireGuard.Configuration;
using Curl.Extension.WireGuard.Noise;
using Curl.Extension.WireGuard.Tunnel;
using Curl.Extension.WireGuard.UnitTests.IpStack;

namespace Curl.Extension.WireGuard.UnitTests.Tunnel;

/// <summary>Checks the IP packet channel against whitepaper section 5.4.6's transport data messages, 6.2's rekeying and 6.5's keepalives.</summary>
[TestClass]
public sealed class WireGuardPacketTunnelTests
{
    private const byte InitiationType = 1;

    private const byte TransportDataType = 4;

    [TestMethod]
    public async Task SendAsync_Packet_SealsItPaddedToSixteenBytesForThePeer()
    {
        var (peer, tunnel, _) = await OpenTunnelAsync();
        using (tunnel)
        {
            await tunnel.SendAsync(new byte[] { 0x45, 1, 2 }, CancellationToken.None);

            CollectionAssert.AreEqual(new byte[] { 0x45, 1, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, peer.Responder.OpenTransport(await peer.NextSentAsync()));
        }
    }

    [TestMethod]
    public async Task ReceiveAsync_JunkAndKeepaliveBeforeAPacket_GivesOnlyThePacket()
    {
        var (peer, tunnel, _) = await OpenTunnelAsync();
        using (tunnel)
        {
            peer.Deliver([9, 9, 9]);
            peer.Deliver(peer.SealToInitiator([]));
            peer.Deliver(peer.SealToInitiator([0x45, 7]));

            ReadOnlyMemory<byte> packet = await tunnel.ReceiveAsync(CancellationToken.None);

            CollectionAssert.AreEqual(new byte[] { 0x45, 7, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, packet.ToArray());
        }
    }

    [TestMethod]
    public async Task Section62_SendAfterRekeyAfterTime_SealsUnderTheOldSessionAndSendsANewInitiation()
    {
        var (peer, tunnel, clock) = await OpenTunnelAsync();
        using (tunnel)
        {
            clock.Advance(WireGuardTransportSession.RekeyAfterTime);

            await tunnel.SendAsync(new byte[] { 0x45, 1 }, CancellationToken.None);

            Assert.IsNotNull(peer.Responder.OpenTransport(await peer.NextSentAsync()));
            byte[] initiation = await peer.NextSentAsync();
            Assert.AreEqual(InitiationType, initiation[0]);
            Assert.IsTrue(peer.Responder.TryConsumeInitiation(initiation));
        }
    }

    [TestMethod]
    public async Task Section62_NoResponseWithinRekeyTimeout_OnlyTheNextSendAfterItInitiatesAgain()
    {
        var (peer, tunnel, clock) = await OpenTunnelAsync();
        using (tunnel)
        {
            clock.Advance(WireGuardTransportSession.RekeyAfterTime);

            await tunnel.SendAsync(new byte[] { 0x45, 1 }, CancellationToken.None);
            await tunnel.SendAsync(new byte[] { 0x45, 2 }, CancellationToken.None);
            clock.Advance(WireGuardTunnelHandshake.RekeyTimeout);
            await tunnel.SendAsync(new byte[] { 0x45, 3 }, CancellationToken.None);

            List<byte> types = [];
            for (int sent = 0; sent < 5; sent++)
            {
                types.Add((await peer.NextSentAsync())[0]);
            }

            CollectionAssert.AreEqual(new[] { TransportDataType, InitiationType, TransportDataType, TransportDataType, InitiationType }, types.ToArray());
        }
    }

    [TestMethod]
    public async Task Section62_RekeyResponse_MovesSendingToTheNewSessionAndTheOldSessionStillOpens()
    {
        var (peer, tunnel, clock) = await OpenTunnelAsync();
        clock.Advance(WireGuardTransportSession.RekeyAfterTime);
        await tunnel.SendAsync(new byte[] { 0x45, 1 }, CancellationToken.None);
        Assert.IsNotNull(peer.Responder.OpenTransport(await peer.NextSentAsync()));
        byte[] sealedUnderOldLate = peer.SealToInitiator([0x45, 3]);
        peer.Deliver(peer.SealToInitiator([0x45, 2]));
        Assert.AreEqual(0x02, (await tunnel.ReceiveAsync(CancellationToken.None)).Span[1]);

        await peer.AcceptHandshakeAsync();
        peer.Deliver(sealedUnderOldLate);
        peer.Deliver(peer.SealToInitiator([0x45, 4]));

        Assert.AreEqual(0x03, (await tunnel.ReceiveAsync(CancellationToken.None)).Span[1]);
        Assert.AreEqual(0x04, (await tunnel.ReceiveAsync(CancellationToken.None)).Span[1]);
        await tunnel.SendAsync(new byte[] { 0x45, 5 }, CancellationToken.None);
        Assert.AreEqual(0x05, peer.Responder.OpenTransport(await peer.NextSentAsync())![1]);

        clock.Advance(WireGuardTransportSession.RekeyAfterTime);
        await tunnel.SendAsync(new byte[] { 0x45, 6 }, CancellationToken.None);
        await peer.AcceptHandshakeAsync();
        peer.Deliver(peer.SealToInitiator([0x45, 7]));
        Assert.AreEqual(0x07, (await tunnel.ReceiveAsync(CancellationToken.None)).Span[1]);
        tunnel.Dispose();
    }

    [TestMethod]
    public async Task Section65_PacketsReceivedAndNothingSent_OneKeepaliveAfterKeepaliveTimeout()
    {
        var (peer, tunnel, clock) = await OpenTunnelAsync();
        using (tunnel)
        {
            peer.Deliver(peer.SealToInitiator([0x45, 1]));
            peer.Deliver(peer.SealToInitiator([0x45, 2]));
            await tunnel.ReceiveAsync(CancellationToken.None);
            await tunnel.ReceiveAsync(CancellationToken.None);

            clock.Advance(WireGuardPacketTunnel.KeepaliveTimeout);
            await tunnel.SendAsync(new byte[] { 0x45, 3 }, CancellationToken.None);

            Assert.IsEmpty(peer.Responder.OpenTransport(await peer.NextSentAsync())!);
            Assert.HasCount(16, peer.Responder.OpenTransport(await peer.NextSentAsync())!);
        }
    }

    [TestMethod]
    public async Task Section65_PacketSentAfterOneReceived_NoKeepalive()
    {
        var (peer, tunnel, clock) = await OpenTunnelAsync();
        using (tunnel)
        {
            peer.Deliver(peer.SealToInitiator([0x45, 1]));
            await tunnel.ReceiveAsync(CancellationToken.None);
            await tunnel.SendAsync(new byte[] { 0x45, 2 }, CancellationToken.None);

            clock.Advance(WireGuardPacketTunnel.KeepaliveTimeout);
            await tunnel.SendAsync(new byte[] { 0x45, 3 }, CancellationToken.None);

            Assert.AreEqual(0x02, peer.Responder.OpenTransport(await peer.NextSentAsync())![1]);
            Assert.AreEqual(0x03, peer.Responder.OpenTransport(await peer.NextSentAsync())![1]);
        }
    }

    [TestMethod]
    public async Task Section65_PersistentKeepalive_SentEveryIntervalWithoutTraffic()
    {
        var (peer, tunnel, clock) = await OpenTunnelAsync(persistentKeepalive: 25);
        using (tunnel)
        {
            clock.Advance(TimeSpan.FromSeconds(25));
            Assert.IsEmpty(peer.Responder.OpenTransport(await peer.NextSentAsync())!);
            clock.Advance(TimeSpan.FromSeconds(25));
            Assert.IsEmpty(peer.Responder.OpenTransport(await peer.NextSentAsync())!);
        }
    }

    [TestMethod]
    public async Task Section65_PersistentKeepalivePastRejectAfterTime_IsDroppedWithoutThrowing()
    {
        var (_, tunnel, clock) = await OpenTunnelAsync(persistentKeepalive: 200);
        using (tunnel)
        {
            clock.Advance(TimeSpan.FromSeconds(200));

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await tunnel.SendAsync(new byte[] { 0x45 }, CancellationToken.None));
        }
    }

    [TestMethod]
    [DataRow(25, 25.0)]
    [DataRow(null, -1.0 / 1000)]
    public void PersistentKeepaliveIntervalOf_Peer_GivesSecondsOrInfinite(int? seconds, double expectedSeconds)
    {
        WireGuardPeer peer = TunnelTestPeer.Configuration().Peers[0] with { PersistentKeepalive = seconds };

        Assert.AreEqual(TimeSpan.FromSeconds(expectedSeconds), WireGuardPacketTunnel.PersistentKeepaliveIntervalOf(peer));
    }

    internal static async Task<(TunnelTestPeer Peer, WireGuardPacketTunnel Tunnel, ManualTimeProvider Clock)> OpenTunnelAsync(int? persistentKeepalive = null)
    {
        TunnelTestPeer peer = new();
        WireGuardConfiguration configuration = TunnelTestPeer.Configuration();
        ManualTimeProvider clock = new();
        var opening = WireGuardPacketTunnel.OpenAsync(configuration.Interface, configuration.Peers[0] with { PersistentKeepalive = persistentKeepalive }, peer, clock, RandomNumberGenerator.Create(), CancellationToken.None);
        await peer.AcceptHandshakeAsync();
        return (peer, await opening, clock);
    }
}
