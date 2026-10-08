using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Quic;

[TestClass]
public sealed class QuicClientConnectorTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task RunHandshakeAsync_InMemoryServer_Completes()
    {
        Diagnostics.Arrange("server", "in-memory QUIC server, no connect timeout");
        using QuicTestServer server = new();
        QuicTestChannel channel = new(server);
        using QuicClientConnectionState handshake = QuicClientConnectionStateTest.Client();

        QuicHandshakeFailure? failure;
        using (Diagnostics.Phase("handshake"))
        {
            failure = await new QuicClientConnector(new ManualTimerTimeProvider()).RunHandshakeAsync(handshake, channel, null, CancellationToken.None);
        }

        Diagnostics.Act("failure", failure?.Message ?? "null");
        Diagnostics.Act("application protocol", handshake.Tls.ApplicationProtocol);
        Diagnostics.Assert("failure", "null", failure?.Message ?? "null");
        Assert.IsNull(failure);
        Diagnostics.Assert("handshake complete", true, handshake.IsComplete);
        Assert.IsTrue(handshake.IsComplete);
        Diagnostics.Assert("application protocol", "h3", handshake.Tls.ApplicationProtocol);
        Assert.AreEqual("h3", handshake.Tls.ApplicationProtocol);
        Diagnostics.Assert("server received the client Finished", true, server.ClientFinishedReceived);
        Assert.IsTrue(server.ClientFinishedReceived);
        Diagnostics.Assert("Initial keys discarded", true, handshake.InitialKeysDiscarded);
        Assert.IsTrue(handshake.InitialKeysDiscarded);
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task RunHandshakeAsync_SilentPeerAndNoConnectTimeout_ClosesWithInternalErrorAndExit55After10Seconds()
    {
        Diagnostics.Arrange("peer", "silent, no connect timeout; clock advances 9999 ms then 1 ms");
        ManualTimerTimeProvider clock = new();
        QuicTestChannel channel = new(null);
        using QuicClientConnectionState handshake = QuicClientConnectionStateTest.Client(clock: clock);

        Task<QuicHandshakeFailure?> run = new QuicClientConnector(clock).RunHandshakeAsync(handshake, channel, null, CancellationToken.None);
        clock.Advance(9999);
        Diagnostics.Assert("run completed after 9999 ms", false, run.IsCompleted);
        Assert.IsFalse(run.IsCompleted);
        clock.Advance(1);
        QuicHandshakeFailure? failure = await run;

        Diagnostics.Act("failure", FormattableString.Invariant($"{failure?.ExitCode}: {failure?.Message}"));
        Diagnostics.Assert("exit code", CurlExitCode.SendError, failure!.ExitCode);
        Assert.AreEqual(CurlExitCode.SendError, failure!.ExitCode);
        Diagnostics.Assert("message", "ngtcp2_conn_handle_expiry returned error: ERR_HANDSHAKE_TIMEOUT", failure.Message);
        Assert.AreEqual("ngtcp2_conn_handle_expiry returned error: ERR_HANDSHAKE_TIMEOUT", failure.Message);
        Diagnostics.Bytes("last datagram sent (CONNECTION_CLOSE in an Initial)", channel.Sent[^1]);
        Diagnostics.Assert("last datagram length", 1200, channel.Sent[^1].Length);
        Assert.AreEqual(1200, channel.Sent[^1].Length);
        ulong closeCode = ClientClose(channel.Sent[^1]).ErrorCode;
        Diagnostics.Act("CONNECTION_CLOSE error code", closeCode);
        Diagnostics.Assert("CONNECTION_CLOSE error code", QuicTransportErrorCode.InternalError, (QuicTransportErrorCode)closeCode);
        Assert.AreEqual((ulong)QuicTransportErrorCode.InternalError, closeCode);
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task RunHandshakeAsync_SilentPeer_ProbesTheInitialWithExponentialBackoff()
    {
        Diagnostics.Arrange("peer", "silent, no connect timeout; probe timeouts at 999, 1998 and 3996 ms after each wait");
        ManualTimerTimeProvider clock = new();
        QuicTestChannel channel = new(null);
        using QuicClientConnectionState handshake = QuicClientConnectionStateTest.Client(clock: clock);
        List<int> sentBeforeEachProbe = [];

        Task<QuicHandshakeFailure?> run = new QuicClientConnector(clock).RunHandshakeAsync(handshake, channel, null, CancellationToken.None);

        // PTO = 333 + 4 x 166.5 = 999 ms before any RTT sample, doubling each time it fires (RFC 9002 section 6.2.1): at 999, 2997 and 6993 ms.
        using (Diagnostics.Phase("probes"))
        {
            foreach (long wait in new long[] { 999, 1998, 3996 })
            {
                bool waiting = await channel.WaitingToReceive.WaitAsync(QuicTestLiveChannel.HangGuard);
                Diagnostics.Assert("connector is waiting to receive before the next probe", true, waiting);
                Assert.IsTrue(waiting);
                clock.Advance(wait - 1);
                sentBeforeEachProbe.Add(channel.Sent.Count);
                clock.Advance(1);
            }
        }

        bool waitingForLast = await channel.WaitingToReceive.WaitAsync(QuicTestLiveChannel.HangGuard);
        Diagnostics.Assert("connector is waiting to receive before the final advance", true, waitingForLast);
        Assert.IsTrue(waitingForLast);
        clock.Advance(3007);
        await run;

        Diagnostics.Act("datagrams sent before each probe", string.Join(", ", sentBeforeEachProbe));
        Diagnostics.Assert("datagrams sent before each probe", "1, 2, 3", string.Join(", ", sentBeforeEachProbe));
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, sentBeforeEachProbe);
        Diagnostics.Assert("datagrams sent in all", 5, channel.Sent.Count);
        Assert.HasCount(5, channel.Sent);
        Diagnostics.Assert("probe timeout count", 3, handshake.Recovery.ProbeTimeoutCount);
        Assert.AreEqual(3, handshake.Recovery.ProbeTimeoutCount);
        foreach (byte[] probe in channel.Sent.Skip(1).Take(3))
        {
            Diagnostics.Assert("probe datagram length", 1200, probe.Length);
            Assert.AreEqual(1200, probe.Length);
        }
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task RunHandshakeAsync_SilentPeerAndConnectTimeout_ClosesWithNoErrorAndExit28()
    {
        Diagnostics.Arrange("peer", "silent; connect timeout 1 s; clock advances 1008 ms");
        ManualTimerTimeProvider clock = new();
        QuicTestChannel channel = new(null);
        using QuicClientConnectionState handshake = QuicClientConnectionStateTest.Client();

        Task<QuicHandshakeFailure?> run = new QuicClientConnector(clock).RunHandshakeAsync(handshake, channel, TimeSpan.FromSeconds(1), CancellationToken.None);
        clock.Advance(1008);
        QuicHandshakeFailure? failure = await run;

        Diagnostics.Act("failure", FormattableString.Invariant($"{failure?.ExitCode}: {failure?.Message}"));
        Diagnostics.Assert("exit code", CurlExitCode.OperationTimedOut, failure!.ExitCode);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, failure!.ExitCode);
        Diagnostics.Assert("message", "Connection timed out after 1008 milliseconds", failure.Message);
        Assert.AreEqual("Connection timed out after 1008 milliseconds", failure.Message);
        Diagnostics.Bytes("second datagram sent (CONNECTION_CLOSE in an Initial)", channel.Sent[1]);
        ulong closeCode = ClientClose(channel.Sent[1]).ErrorCode;
        Diagnostics.Act("CONNECTION_CLOSE error code", closeCode);
        Diagnostics.Assert("CONNECTION_CLOSE error code", QuicTransportErrorCode.NoError, (QuicTransportErrorCode)closeCode);
        Assert.AreEqual((ulong)QuicTransportErrorCode.NoError, closeCode);
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task RunHandshakeAsync_VersionNegotiationWithoutVersion1_FailsWithExit7()
    {
        Diagnostics.Arrange("server", "answers with Version Negotiation listing only 0xff00001d");
        using QuicTestServer server = new() { VersionNegotiation = [0xff00001d] };
        QuicTestChannel channel = new(server);
        using QuicClientConnectionState handshake = QuicClientConnectionStateTest.Client();

        QuicHandshakeFailure? failure;
        using (Diagnostics.Phase("handshake"))
        {
            failure = await new QuicClientConnector(new ManualTimerTimeProvider()).RunHandshakeAsync(handshake, channel, null, CancellationToken.None);
        }

        Diagnostics.Act("failure", FormattableString.Invariant($"{failure?.ExitCode}: {failure?.Message}"));
        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, failure!.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, failure!.ExitCode);
        Diagnostics.Assert("datagrams sent", 1, channel.Sent.Count);
        Assert.HasCount(1, channel.Sent);
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task RunHandshakeAsync_DatagramFromAnotherEndpoint_IsIgnored()
    {
        Diagnostics.Arrange("stray datagram", "1200 zero bytes from 127.0.0.1:9, queued before the handshake");
        using QuicTestServer server = new();
        QuicTestChannel channel = new(server);
        channel.Enqueue(new byte[1200], new IPEndPoint(IPAddress.Loopback, 9));
        using QuicClientConnectionState handshake = QuicClientConnectionStateTest.Client();

        QuicHandshakeFailure? failure;
        using (Diagnostics.Phase("handshake"))
        {
            failure = await new QuicClientConnector(new ManualTimerTimeProvider()).RunHandshakeAsync(handshake, channel, null, CancellationToken.None);
        }

        Diagnostics.Act("failure", failure?.Message ?? "null");
        Diagnostics.Assert("failure", "null", failure?.Message ?? "null");
        Assert.IsNull(failure);
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task RunHandshakeAsync_CallerCancels_Throws()
    {
        Diagnostics.Arrange("cancellation token", "already cancelled");
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        using QuicClientConnectionState handshake = QuicClientConnectionStateTest.Client();
        QuicClientConnector connector = new(new ManualTimerTimeProvider());

        TaskCanceledException cancelled = await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => connector.RunHandshakeAsync(handshake, new QuicTestChannel(null), null, cancellation.Token));
        Diagnostics.Act("cancelled run exception", cancelled.GetType().Name);
        Diagnostics.Assert("cancelled run exception", nameof(TaskCanceledException), cancelled.GetType().Name);
        ArgumentNullException noHandshake = await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => connector.RunHandshakeAsync(null!, new QuicTestChannel(null), null, CancellationToken.None));
        Diagnostics.Act("null handshake exception", FormattableString.Invariant($"{noHandshake.GetType().Name}: {noHandshake.ParamName}"));
        Diagnostics.Assert("null handshake exception", nameof(ArgumentNullException), noHandshake.GetType().Name);
        ArgumentNullException noChannel = await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => connector.RunHandshakeAsync(handshake, null!, null, CancellationToken.None));
        Diagnostics.Act("null channel exception", FormattableString.Invariant($"{noChannel.GetType().Name}: {noChannel.ParamName}"));
        Diagnostics.Assert("null channel exception", nameof(ArgumentNullException), noChannel.GetType().Name);
    }

    private static QuicConnectionCloseFrame ClientClose(byte[] datagram)
    {
        QuicLongHeaderPacket header = (QuicLongHeaderPacket)QuicPacketCodec.Decode(datagram, QuicClientSettings.CurlConnectionIdLength).Packet;
        using QuicPacketProtection keys = QuicPacketProtection.CreateClientInitial(header.DestinationConnectionId.Span);
        QuicLongHeaderPacket packet = (QuicLongHeaderPacket)keys.Unprotect(datagram, 0, null).Packet!;
        return QuicFrameCodec.Decode(packet.Payload, QuicPacketType.Initial).OfType<QuicConnectionCloseFrame>().Single();
    }
}
