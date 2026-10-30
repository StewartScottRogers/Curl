using System.Security.Cryptography;
using Curl.Extension.WireGuard.Configuration;
using Curl.Extension.WireGuard.Noise;
using Curl.Extension.WireGuard.Tunnel;
using Curl.Extension.WireGuard.UnitTests.IpStack;

namespace Curl.Extension.WireGuard.UnitTests.Tunnel;

/// <summary>Checks the IP packet channel against whitepaper section 5.4.6's transport data messages.</summary>
[TestClass]
public sealed class WireGuardPacketTunnelTests
{
    [TestMethod]
    public async Task SendAsync_Packet_SealsItPaddedToSixteenBytesForThePeer()
    {
        var (peer, session) = await HandshakeAsync();
        using (session)
        {
            WireGuardPacketTunnel tunnel = new(peer, session);

            await tunnel.SendAsync(new byte[] { 0x45, 1, 2 }, CancellationToken.None);

            CollectionAssert.AreEqual(new byte[] { 0x45, 1, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, peer.Responder.OpenTransport(await peer.NextSentAsync()));
        }
    }

    [TestMethod]
    public async Task ReceiveAsync_JunkAndKeepaliveBeforeAPacket_GivesOnlyThePacket()
    {
        var (peer, session) = await HandshakeAsync();
        using (session)
        {
            WireGuardPacketTunnel tunnel = new(peer, session);
            peer.Deliver([9, 9, 9]);
            peer.Deliver(peer.SealToInitiator([]));
            peer.Deliver(peer.SealToInitiator([0x45, 7]));

            ReadOnlyMemory<byte> packet = await tunnel.ReceiveAsync(CancellationToken.None);

            CollectionAssert.AreEqual(new byte[] { 0x45, 7, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, packet.ToArray());
        }
    }

    internal static async Task<(TunnelTestPeer Peer, WireGuardTransportSession Session)> HandshakeAsync()
    {
        TunnelTestPeer peer = new();
        WireGuardConfiguration configuration = TunnelTestPeer.Configuration();
        ManualTimeProvider clock = new();
        WireGuardHandshakeInitiator initiator = WireGuardHandshakeInitiator.ForPeer(configuration.Interface, configuration.Peers[0], clock, RandomNumberGenerator.Create());
        var connecting = WireGuardTunnelHandshake.ConnectAsync(initiator, peer, clock, CancellationToken.None);
        await peer.AcceptHandshakeAsync();
        return (peer, await connecting);
    }
}
