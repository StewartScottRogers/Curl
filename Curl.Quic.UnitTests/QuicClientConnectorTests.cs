using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Quic;

[TestClass]
public sealed class QuicClientConnectorTests
{
    [TestMethod]
    public async Task RunHandshakeAsync_InMemoryServer_Completes()
    {
        using QuicTestServer server = new();
        QuicTestChannel channel = new(server);
        using QuicClientHandshake handshake = QuicHandshakeTest.Client();

        QuicHandshakeFailure? failure = await new QuicClientConnector(new ManualTimerTimeProvider()).RunHandshakeAsync(handshake, channel, null, CancellationToken.None);

        Assert.IsNull(failure);
        Assert.IsTrue(handshake.IsComplete);
        Assert.AreEqual("h3", handshake.Tls.ApplicationProtocol);
        Assert.IsTrue(server.ClientFinishedReceived);
        Assert.IsTrue(handshake.InitialKeysDiscarded);
    }

    [TestMethod]
    public async Task RunHandshakeAsync_SilentPeerAndNoConnectTimeout_ClosesWithInternalErrorAndExit55After10Seconds()
    {
        ManualTimerTimeProvider clock = new();
        QuicTestChannel channel = new(null);
        using QuicClientHandshake handshake = QuicHandshakeTest.Client(clock: clock);

        Task<QuicHandshakeFailure?> run = new QuicClientConnector(clock).RunHandshakeAsync(handshake, channel, null, CancellationToken.None);
        clock.Advance(9999);
        Assert.IsFalse(run.IsCompleted);
        clock.Advance(1);
        QuicHandshakeFailure? failure = await run;

        Assert.AreEqual(CurlExitCode.SendError, failure!.ExitCode);
        Assert.AreEqual("ngtcp2_conn_handle_expiry returned error: ERR_HANDSHAKE_TIMEOUT", failure.Message);
        Assert.AreEqual(1200, channel.Sent[^1].Length);
        Assert.AreEqual((ulong)QuicTransportErrorCode.InternalError, ClientClose(channel.Sent[^1]).ErrorCode);
    }

    [TestMethod]
    public async Task RunHandshakeAsync_SilentPeer_ProbesTheInitialWithExponentialBackoff()
    {
        ManualTimerTimeProvider clock = new();
        QuicTestChannel channel = new(null);
        using QuicClientHandshake handshake = QuicHandshakeTest.Client(clock: clock);
        List<int> sentBeforeEachProbe = [];

        Task<QuicHandshakeFailure?> run = new QuicClientConnector(clock).RunHandshakeAsync(handshake, channel, null, CancellationToken.None);

        // PTO = 333 + 4 x 166.5 = 999 ms before any RTT sample, doubling each time it fires (RFC 9002 section 6.2.1): at 999, 2997 and 6993 ms.
        foreach (long wait in new long[] { 999, 1998, 3996 })
        {
            await channel.WaitingToReceive.WaitAsync();
            clock.Advance(wait - 1);
            sentBeforeEachProbe.Add(channel.Sent.Count);
            clock.Advance(1);
        }

        await channel.WaitingToReceive.WaitAsync();
        clock.Advance(3007);
        await run;

        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, sentBeforeEachProbe);
        Assert.HasCount(5, channel.Sent);
        Assert.AreEqual(3, handshake.Recovery.ProbeTimeoutCount);
        foreach (byte[] probe in channel.Sent.Skip(1).Take(3))
        {
            Assert.AreEqual(1200, probe.Length);
        }
    }

    [TestMethod]
    public async Task RunHandshakeAsync_SilentPeerAndConnectTimeout_ClosesWithNoErrorAndExit28()
    {
        ManualTimerTimeProvider clock = new();
        QuicTestChannel channel = new(null);
        using QuicClientHandshake handshake = QuicHandshakeTest.Client();

        Task<QuicHandshakeFailure?> run = new QuicClientConnector(clock).RunHandshakeAsync(handshake, channel, TimeSpan.FromSeconds(1), CancellationToken.None);
        clock.Advance(1008);
        QuicHandshakeFailure? failure = await run;

        Assert.AreEqual(CurlExitCode.OperationTimedOut, failure!.ExitCode);
        Assert.AreEqual("Connection timed out after 1008 milliseconds", failure.Message);
        Assert.AreEqual((ulong)QuicTransportErrorCode.NoError, ClientClose(channel.Sent[1]).ErrorCode);
    }

    [TestMethod]
    public async Task RunHandshakeAsync_VersionNegotiationWithoutVersion1_FailsWithExit7()
    {
        using QuicTestServer server = new() { VersionNegotiation = [0xff00001d] };
        QuicTestChannel channel = new(server);
        using QuicClientHandshake handshake = QuicHandshakeTest.Client();

        QuicHandshakeFailure? failure = await new QuicClientConnector(new ManualTimerTimeProvider()).RunHandshakeAsync(handshake, channel, null, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, failure!.ExitCode);
        Assert.HasCount(1, channel.Sent);
    }

    [TestMethod]
    public async Task RunHandshakeAsync_DatagramFromAnotherEndpoint_IsIgnored()
    {
        using QuicTestServer server = new();
        QuicTestChannel channel = new(server);
        channel.Enqueue(new byte[1200], new IPEndPoint(IPAddress.Loopback, 9));
        using QuicClientHandshake handshake = QuicHandshakeTest.Client();

        QuicHandshakeFailure? failure = await new QuicClientConnector(new ManualTimerTimeProvider()).RunHandshakeAsync(handshake, channel, null, CancellationToken.None);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public async Task RunHandshakeAsync_CallerCancels_Throws()
    {
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        using QuicClientHandshake handshake = QuicHandshakeTest.Client();
        QuicClientConnector connector = new(new ManualTimerTimeProvider());

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => connector.RunHandshakeAsync(handshake, new QuicTestChannel(null), null, cancellation.Token));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => connector.RunHandshakeAsync(null!, new QuicTestChannel(null), null, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => connector.RunHandshakeAsync(handshake, null!, null, CancellationToken.None));
    }

    private static QuicConnectionCloseFrame ClientClose(byte[] datagram)
    {
        QuicLongHeaderPacket header = (QuicLongHeaderPacket)QuicPacketCodec.Decode(datagram, QuicClientSettings.CurlConnectionIdLength).Packet;
        using QuicPacketProtection keys = QuicPacketProtection.CreateClientInitial(header.DestinationConnectionId.Span);
        QuicLongHeaderPacket packet = (QuicLongHeaderPacket)keys.Unprotect(datagram, 0, null).Packet!;
        return QuicFrameCodec.Decode(packet.Payload, QuicPacketType.Initial).OfType<QuicConnectionCloseFrame>().Single();
    }
}
