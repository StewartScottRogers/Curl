using System.Security.Cryptography;
using Curl.Extension.WireGuard.Configuration;
using Curl.Extension.WireGuard.Noise;
using Curl.Extension.WireGuard.Tunnel;
using Curl.Extension.WireGuard.UnitTests.IpStack;

namespace Curl.Extension.WireGuard.UnitTests.Tunnel;

/// <summary>Checks the handshake's timing against whitepaper section 6.4 and 6.1's Rekey-Timeout and Rekey-Attempt-Time.</summary>
[TestClass]
public sealed class WireGuardTunnelHandshakeTests
{
    [TestMethod]
    public void Constants_Section61_RekeyTimeoutFiveSecondsAndNinetySecondsOfAttempts()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(5), WireGuardTunnelHandshake.RekeyTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(90), WireGuardTunnelHandshake.RekeyTimeout * WireGuardTunnelHandshake.MaximumAttempts);
    }

    [TestMethod]
    public async Task ConnectAsync_FirstInitiationLost_SendsANewOneAfterRekeyTimeoutAndCompletes()
    {
        TunnelTestPeer peer = new();
        ManualTimeProvider clock = new();
        var connecting = WireGuardTunnelHandshake.ConnectAsync(InitiatorFor(clock), peer, clock, CancellationToken.None);

        byte[] lost = await peer.NextSentAsync();
        clock.Advance(WireGuardTunnelHandshake.RekeyTimeout);
        await peer.AcceptHandshakeAsync();
        using WireGuardTransportSession session = await connecting;

        Assert.AreEqual(Noise.WireGuardTestResponder.ResponderIndex, session.RemoteIndex);
        Assert.HasCount(148, lost);
    }

    [TestMethod]
    public async Task ConnectAsync_CallerCancels_ThrowsWithoutRetrying()
    {
        TunnelTestPeer peer = new();
        ManualTimeProvider clock = new();
        using CancellationTokenSource cancellation = new();
        var connecting = WireGuardTunnelHandshake.ConnectAsync(InitiatorFor(clock), peer, clock, cancellation.Token);

        await peer.NextSentAsync();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => connecting);
    }

    private static WireGuardHandshakeInitiator InitiatorFor(TimeProvider clock)
    {
        WireGuardConfiguration configuration = TunnelTestPeer.Configuration();
        return WireGuardHandshakeInitiator.ForPeer(configuration.Interface, configuration.Peers[0], clock, RandomNumberGenerator.Create());
    }
}
