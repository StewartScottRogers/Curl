using Curl.Protocol.Abstractions;
using Curl.Testing;
using static Curl.Quic.QuicStreamTest;

namespace Curl.Quic;

/// <summary>How a QUIC connection ends once its handshake is complete: the close it sends, the server's close, the idle timeout and a stateless reset (RFC 9000 section 10, ADR-0177).</summary>
[TestClass]
public sealed class QuicConnectionCloseTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void CloseWithApplicationError_EndOfANormalConnection_SendsOneApplicationCloseWithH3NoError()
    {
        Diagnostics.Arrange("application error code", 0x100);
        using QuicTestServer server = new();
        using QuicClientConnectionState client = ConnectClient(server, null, null);

        QuicClientConnectionStateTests.Exchange(client, server, client.CloseWithApplicationError(0x100));

        QuicConnectionCloseFrame close = Sent<QuicConnectionCloseFrame>(server).Single();
        Diagnostics.Act("CONNECTION_CLOSE", close);
        Diagnostics.Assert("error code", 0x100UL, close.ErrorCode);
        Assert.AreEqual(0x100UL, close.ErrorCode);
        Diagnostics.Assert("frame type is null", true, close.FrameType is null);
        Assert.IsNull(close.FrameType);
        Diagnostics.Assert("reason phrase is empty", true, close.ReasonPhrase.IsEmpty);
        Assert.IsTrue(close.ReasonPhrase.IsEmpty);
        Diagnostics.Assert("client failure", "none", client.Failure?.ToString() ?? "none");
        Assert.IsNull(client.Failure);
    }

    [TestMethod]
    [DataRow(0x0aUL, DisplayName = "A transport error")]
    [DataRow(0x150UL, DisplayName = "A TLS alert (CRYPTO_ERROR)")]
    public void Receive_ServerCloseAfterTheHandshake_DrainsWithExit56(ulong errorCode)
    {
        Diagnostics.Arrange("server CONNECTION_CLOSE error code", errorCode);
        Diagnostics.Arrange("reason phrase", "bye");
        using QuicTestServer server = new();
        using QuicClientConnectionState client = ConnectClient(server, null, null);
        QuicConnectionCloseFrame close = new(errorCode, 0, "bye"u8.ToArray());

        IReadOnlyList<byte[]> answer = client.Receive(server.Protect(QuicPacketType.OneRtt, close));

        Diagnostics.Act("datagrams answered", answer.Count);
        Diagnostics.Assert("datagrams answered", 0, answer.Count);
        Assert.IsEmpty(answer);
        Diagnostics.Act("failure exit code", client.Failure!.ExitCode);
        Diagnostics.Assert("failure exit code", CurlExitCode.RecvError, client.Failure!.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, client.Failure!.ExitCode);
        Diagnostics.Assert("failure message", "Failure when receiving data from the peer", client.Failure.Message);
        Assert.AreEqual("Failure when receiving data from the peer", client.Failure.Message);
        Diagnostics.Assert("server close error code", errorCode, client.Failure.ServerClose!.ErrorCode);
        Assert.AreEqual(errorCode, client.Failure.ServerClose!.ErrorCode);
        IReadOnlyList<byte[]> pending = client.TakeDatagramsToSend();
        Diagnostics.Assert("datagrams to send", 0, pending.Count);
        Assert.IsEmpty(pending);
        Diagnostics.Assert("time until idle timer", Timeout.InfiniteTimeSpan, client.TimeUntilIdleTimer);
        Assert.AreEqual(Timeout.InfiniteTimeSpan, client.TimeUntilIdleTimer);
    }

    [TestMethod]
    public void Receive_StatelessResetAfterTheHandshake_DrainsWithExit56()
    {
        Diagnostics.Arrange("stateless reset datagram length", 30);
        using QuicTestServer server = new();
        using QuicClientConnectionState client = ConnectClient(server, null, null);

        IReadOnlyList<byte[]> answer = client.Receive(StatelessReset(server.StatelessResetToken, 30));

        Diagnostics.Act("datagrams answered", answer.Count);
        Diagnostics.Assert("datagrams answered", 0, answer.Count);
        Assert.IsEmpty(answer);
        Diagnostics.Act("failure exit code", client.Failure!.ExitCode);
        Diagnostics.Assert("failure exit code", CurlExitCode.RecvError, client.Failure!.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, client.Failure!.ExitCode);
        Diagnostics.Assert("failure message", "Failure when receiving data from the peer", client.Failure.Message);
        Assert.AreEqual("Failure when receiving data from the peer", client.Failure.Message);
        Diagnostics.Assert("server close is null", true, client.Failure.ServerClose is null);
        Assert.IsNull(client.Failure.ServerClose);
        IReadOnlyList<byte[]> pending = client.TakeDatagramsToSend();
        Diagnostics.Assert("datagrams to send", 0, pending.Count);
        Assert.IsEmpty(pending);
    }

    [TestMethod]
    public void Receive_DatagramsThatAreNotAStatelessReset_AreDropped()
    {
        Diagnostics.Arrange("datagrams", "too short (20 bytes), long header, wrong token, token of an unused connection id");
        using QuicTestServer server = new();
        using QuicClientConnectionState client = ConnectClient(server, null, null);
        byte[] token = server.StatelessResetToken;
        byte[] unusedConnectionIdToken = [.. Enumerable.Repeat((byte)7, 16)];
        byte[] longHeader = StatelessReset(token, 30);
        longHeader[0] = 0xc0;
        byte[] otherToken = StatelessReset(token, 30);
        otherToken[^1] ^= 0xff;
        Diagnostics.Bytes("server stateless reset token", token);

        client.Receive(StatelessReset(token, 20));
        client.Receive(longHeader);
        client.Receive(otherToken);
        Deliver(client, server, new QuicNewConnectionIdFrame(1, 0, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, unusedConnectionIdToken));
        client.Receive(StatelessReset(unusedConnectionIdToken, 30));

        Diagnostics.Act("client failure", client.Failure?.ToString() ?? "none");
        Diagnostics.Assert("client failure", "none", client.Failure?.ToString() ?? "none");
        Assert.IsNull(client.Failure);
    }

    [TestMethod]
    public void OnIdleTimer_ServerGoesSilent_SendsAKeepAlivePingThenTimesOutWithExit55()
    {
        Diagnostics.Arrange("idle timeout", "30 s (keep-alive at 15 s)");
        ManualTimerTimeProvider clock = new();
        using QuicTestServer server = new();
        using QuicClientConnectionState client = ConnectClient(server, null, clock);
        Diagnostics.Act("time until idle timer", client.TimeUntilIdleTimer);
        Diagnostics.Assert("time until idle timer", TimeSpan.FromSeconds(15), client.TimeUntilIdleTimer);
        Assert.AreEqual(TimeSpan.FromSeconds(15), client.TimeUntilIdleTimer);
        IReadOnlyList<byte[]> early = client.OnIdleTimer();
        Diagnostics.Assert("datagrams before the timer is due", 0, early.Count);
        Assert.IsEmpty(early);

        // The first keep-alive is the first ack-eliciting packet since the server was heard
        // from, so the 30-second timeout now runs from 15 s; the second one does not move it.
        clock.Advance(15_000);
        server.Receive(client.OnIdleTimer().Single());
        Diagnostics.Assert("PING frames after the first keep-alive", 1, Sent<QuicPingFrame>(server).Count);
        Assert.HasCount(1, Sent<QuicPingFrame>(server));
        Diagnostics.Assert("time until idle timer", TimeSpan.FromSeconds(15), client.TimeUntilIdleTimer);
        Assert.AreEqual(TimeSpan.FromSeconds(15), client.TimeUntilIdleTimer);
        clock.Advance(15_000);
        server.Receive(client.OnIdleTimer().Single());
        Diagnostics.Assert("PING frames after the second keep-alive", 2, Sent<QuicPingFrame>(server).Count);
        Assert.HasCount(2, Sent<QuicPingFrame>(server));
        Diagnostics.Assert("time until idle timer", TimeSpan.FromSeconds(15), client.TimeUntilIdleTimer);
        Assert.AreEqual(TimeSpan.FromSeconds(15), client.TimeUntilIdleTimer);
        clock.Advance(14_999);
        IReadOnlyList<byte[]> justBefore = client.OnIdleTimer();
        Diagnostics.Assert("datagrams 1 ms before the timeout", 0, justBefore.Count);
        Assert.IsEmpty(justBefore);
        Diagnostics.Assert("client failure 1 ms before the timeout", "none", client.Failure?.ToString() ?? "none");
        Assert.IsNull(client.Failure);
        clock.Advance(1);

        IReadOnlyList<byte[]> atTimeout = client.OnIdleTimer();
        Diagnostics.Act("failure", client.Failure?.Message ?? "none");
        Diagnostics.Assert("datagrams at the timeout", 0, atTimeout.Count);
        Assert.IsEmpty(atTimeout);
        Diagnostics.Assert("failure exit code", CurlExitCode.SendError, client.Failure!.ExitCode);
        Assert.AreEqual(CurlExitCode.SendError, client.Failure!.ExitCode);
        Diagnostics.Assert("failure message", "ngtcp2_conn_handle_expiry returned error: ERR_IDLE_CLOSE", client.Failure.Message);
        Assert.AreEqual("ngtcp2_conn_handle_expiry returned error: ERR_IDLE_CLOSE", client.Failure.Message);
        Diagnostics.Assert("time until idle timer", Timeout.InfiniteTimeSpan, client.TimeUntilIdleTimer);
        Assert.AreEqual(Timeout.InfiniteTimeSpan, client.TimeUntilIdleTimer);
        IReadOnlyList<byte[]> afterFailure = client.OnIdleTimer();
        Diagnostics.Assert("datagrams after the failure", 0, afterFailure.Count);
        Assert.IsEmpty(afterFailure);
    }

    [TestMethod]
    public void TimeUntilIdleTimer_PacketFromTheServer_RestartsTheIdleTimer()
    {
        Diagnostics.Arrange("time advanced before the server packet (ms)", 10_000);
        ManualTimerTimeProvider clock = new();
        using QuicTestServer server = new();
        using QuicClientConnectionState client = ConnectClient(server, null, clock);

        clock.Advance(10_000);
        client.Receive(server.Protect(QuicPacketType.OneRtt, new QuicPingFrame()));

        Diagnostics.Act("time until idle timer", client.TimeUntilIdleTimer);
        Diagnostics.Assert("time until idle timer", TimeSpan.FromSeconds(15), client.TimeUntilIdleTimer);
        Assert.AreEqual(TimeSpan.FromSeconds(15), client.TimeUntilIdleTimer);
    }

    [TestMethod]
    [DataRow(0UL, 0UL, -1L, DisplayName = "Neither side declares one: no idle timer")]
    [DataRow(10_000UL, 30_000UL, 5_000L, DisplayName = "The smaller is the client's")]
    [DataRow(10_000UL, 0UL, 5_000L, DisplayName = "Only the client declares one")]
    [DataRow(0UL, 4_000UL, 2_000L, DisplayName = "Only the server declares one")]
    public void TimeUntilIdleTimer_BothSidesTimeouts_KeepAliveAtHalfTheSmallerNonZero(ulong clientTimeout, ulong serverTimeout, long expectedMilliseconds)
    {
        Diagnostics.Arrange("client MaxIdleTimeout (ms)", clientTimeout);
        Diagnostics.Arrange("server MaxIdleTimeout (ms)", serverTimeout);
        Diagnostics.Arrange("expected keep-alive (ms, -1 is none)", expectedMilliseconds);
        using QuicTestServer server = new() { ConfigureTransportParameters = parameters => parameters with { MaxIdleTimeout = serverTimeout } };
        using QuicClientConnectionState client = ConnectClient(server, SmallClientLimits with { MaxIdleTimeout = clientTimeout }, null);

        Diagnostics.Act("time until idle timer", client.TimeUntilIdleTimer);
        Diagnostics.Assert("time until idle timer", TimeSpan.FromMilliseconds(expectedMilliseconds), client.TimeUntilIdleTimer);
        Assert.AreEqual(TimeSpan.FromMilliseconds(expectedMilliseconds), client.TimeUntilIdleTimer);
    }

    [TestMethod]
    public void OnIdleTimer_TimeoutBelowThreeProbeTimeouts_WaitsThreeProbeTimeouts()
    {
        Diagnostics.Arrange("server MaxIdleTimeout (ms)", 10);
        ManualTimerTimeProvider clock = new();
        using QuicTestServer server = new() { ConfigureTransportParameters = parameters => parameters with { MaxIdleTimeout = 10 } };
        using QuicClientConnectionState client = ConnectClient(server, null, clock);
        long floor = (long)(3 * (client.Recovery.Rtt.ProbeTimeout + client.Recovery.MaxAckDelay)).TotalMilliseconds;
        Diagnostics.Act("three probe timeouts (ms)", floor);
        clock.Advance(5);
        IReadOnlyList<byte[]> keepAlive = client.OnIdleTimer();
        Diagnostics.Act("datagrams at 5 ms", keepAlive.Count);
        Diagnostics.Assert("datagrams at 5 ms", "> 0", keepAlive.Count);
        Assert.IsNotEmpty(keepAlive);

        int steps = 0;
        while (client.Failure is null)
        {
            clock.Advance((long)client.TimeUntilIdleTimer.TotalMilliseconds);
            client.OnIdleTimer();
            steps++;
        }

        Diagnostics.Act("timer steps until failure", steps);
        Diagnostics.Act("clock timestamp at failure", clock.GetTimestamp());
        Diagnostics.Assert("floor above the declared timeout", "> 10", floor);
        Assert.IsGreaterThan(10L, floor);
        Diagnostics.Assert("clock timestamp at failure", 5 + floor, clock.GetTimestamp());
        Assert.AreEqual(5 + floor, clock.GetTimestamp());
        Diagnostics.Assert("failure exit code", CurlExitCode.SendError, client.Failure.ExitCode);
        Assert.AreEqual(CurlExitCode.SendError, client.Failure.ExitCode);
    }

    [TestMethod]
    public void TimeUntilIdleTimer_BeforeTheHandshakeCompletes_IsInfinite()
    {
        Diagnostics.Arrange("handshake", "started, not complete");
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client();
        client.Start();

        Diagnostics.Act("time until idle timer", client.TimeUntilIdleTimer);
        Diagnostics.Assert("time until idle timer", Timeout.InfiniteTimeSpan, client.TimeUntilIdleTimer);
        Assert.AreEqual(Timeout.InfiniteTimeSpan, client.TimeUntilIdleTimer);
        IReadOnlyList<byte[]> datagrams = client.OnIdleTimer();
        Diagnostics.Assert("datagrams from the idle timer", 0, datagrams.Count);
        Assert.IsEmpty(datagrams);
    }

    // A datagram shaped as a stateless reset: a short header's first byte, unpredictable bits, then the token.
    private static byte[] StatelessReset(byte[] token, int length)
    {
        byte[] datagram = new byte[length];
        Random.Shared.NextBytes(datagram);
        datagram[0] = (byte)(0x40 | (datagram[0] & 0x3f));
        token.CopyTo(datagram, length - token.Length);
        return datagram;
    }

    private QuicClientConnectionState ConnectClient(QuicTestServer server, QuicTransportParameters? clientParameters, ManualTimerTimeProvider? clock)
    {
        using (Diagnostics.Phase("handshake"))
        {
            return Connect(server, clientParameters, clock);
        }
    }
}
