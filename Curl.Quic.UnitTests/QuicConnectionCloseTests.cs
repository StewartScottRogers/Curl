using Curl.Protocol.Abstractions;
using static Curl.Quic.QuicStreamTest;

namespace Curl.Quic;

/// <summary>How a QUIC connection ends once its handshake is complete: the close it sends, the server's close, the idle timeout and a stateless reset (RFC 9000 section 10, ADR-0177).</summary>
[TestClass]
public sealed class QuicConnectionCloseTests
{
    [TestMethod]
    public void CloseWithApplicationError_EndOfANormalConnection_SendsOneApplicationCloseWithH3NoError()
    {
        using QuicTestServer server = new();
        using QuicClientHandshake client = Connect(server);

        QuicClientHandshakeTests.Exchange(client, server, client.CloseWithApplicationError(0x100));

        QuicConnectionCloseFrame close = Sent<QuicConnectionCloseFrame>(server).Single();
        Assert.AreEqual(0x100UL, close.ErrorCode);
        Assert.IsNull(close.FrameType);
        Assert.IsTrue(close.ReasonPhrase.IsEmpty);
        Assert.IsNull(client.Failure);
    }

    [TestMethod]
    [DataRow(0x0aUL, DisplayName = "A transport error")]
    [DataRow(0x150UL, DisplayName = "A TLS alert (CRYPTO_ERROR)")]
    public void Receive_ServerCloseAfterTheHandshake_DrainsWithExit56(ulong errorCode)
    {
        using QuicTestServer server = new();
        using QuicClientHandshake client = Connect(server);
        QuicConnectionCloseFrame close = new(errorCode, 0, "bye"u8.ToArray());

        IReadOnlyList<byte[]> answer = client.Receive(server.Protect(QuicPacketType.OneRtt, close));

        Assert.IsEmpty(answer);
        Assert.AreEqual(CurlExitCode.RecvError, client.Failure!.ExitCode);
        Assert.AreEqual("Failure when receiving data from the peer", client.Failure.Message);
        Assert.AreEqual(errorCode, client.Failure.ServerClose!.ErrorCode);
        Assert.IsEmpty(client.TakeDatagramsToSend());
        Assert.AreEqual(Timeout.InfiniteTimeSpan, client.TimeUntilIdleTimer);
    }

    [TestMethod]
    public void Receive_StatelessResetAfterTheHandshake_DrainsWithExit56()
    {
        using QuicTestServer server = new();
        using QuicClientHandshake client = Connect(server);

        IReadOnlyList<byte[]> answer = client.Receive(StatelessReset(server.StatelessResetToken, 30));

        Assert.IsEmpty(answer);
        Assert.AreEqual(CurlExitCode.RecvError, client.Failure!.ExitCode);
        Assert.AreEqual("Failure when receiving data from the peer", client.Failure.Message);
        Assert.IsNull(client.Failure.ServerClose);
        Assert.IsEmpty(client.TakeDatagramsToSend());
    }

    [TestMethod]
    public void Receive_DatagramsThatAreNotAStatelessReset_AreDropped()
    {
        using QuicTestServer server = new();
        using QuicClientHandshake client = Connect(server);
        byte[] token = server.StatelessResetToken;
        byte[] unusedConnectionIdToken = [.. Enumerable.Repeat((byte)7, 16)];
        byte[] longHeader = StatelessReset(token, 30);
        longHeader[0] = 0xc0;
        byte[] otherToken = StatelessReset(token, 30);
        otherToken[^1] ^= 0xff;

        client.Receive(StatelessReset(token, 20));
        client.Receive(longHeader);
        client.Receive(otherToken);
        Deliver(client, server, new QuicNewConnectionIdFrame(1, 0, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, unusedConnectionIdToken));
        client.Receive(StatelessReset(unusedConnectionIdToken, 30));

        Assert.IsNull(client.Failure);
    }

    [TestMethod]
    public void OnIdleTimer_ServerGoesSilent_SendsAKeepAlivePingThenTimesOutWithExit55()
    {
        ManualTimerTimeProvider clock = new();
        using QuicTestServer server = new();
        using QuicClientHandshake client = Connect(server, clock: clock);
        Assert.AreEqual(TimeSpan.FromSeconds(15), client.TimeUntilIdleTimer);
        Assert.IsEmpty(client.OnIdleTimer());

        // The first keep-alive is the first ack-eliciting packet since the server was heard
        // from, so the 30-second timeout now runs from 15 s; the second one does not move it.
        clock.Advance(15_000);
        server.Receive(client.OnIdleTimer().Single());
        Assert.HasCount(1, Sent<QuicPingFrame>(server));
        Assert.AreEqual(TimeSpan.FromSeconds(15), client.TimeUntilIdleTimer);
        clock.Advance(15_000);
        server.Receive(client.OnIdleTimer().Single());
        Assert.HasCount(2, Sent<QuicPingFrame>(server));
        Assert.AreEqual(TimeSpan.FromSeconds(15), client.TimeUntilIdleTimer);
        clock.Advance(14_999);
        Assert.IsEmpty(client.OnIdleTimer());
        Assert.IsNull(client.Failure);
        clock.Advance(1);

        Assert.IsEmpty(client.OnIdleTimer());
        Assert.AreEqual(CurlExitCode.SendError, client.Failure!.ExitCode);
        Assert.AreEqual("ngtcp2_conn_handle_expiry returned error: ERR_IDLE_CLOSE", client.Failure.Message);
        Assert.AreEqual(Timeout.InfiniteTimeSpan, client.TimeUntilIdleTimer);
        Assert.IsEmpty(client.OnIdleTimer());
    }

    [TestMethod]
    public void TimeUntilIdleTimer_PacketFromTheServer_RestartsTheIdleTimer()
    {
        ManualTimerTimeProvider clock = new();
        using QuicTestServer server = new();
        using QuicClientHandshake client = Connect(server, clock: clock);

        clock.Advance(10_000);
        client.Receive(server.Protect(QuicPacketType.OneRtt, new QuicPingFrame()));

        Assert.AreEqual(TimeSpan.FromSeconds(15), client.TimeUntilIdleTimer);
    }

    [TestMethod]
    [DataRow(0UL, 0UL, -1L, DisplayName = "Neither side declares one: no idle timer")]
    [DataRow(10_000UL, 30_000UL, 5_000L, DisplayName = "The smaller is the client's")]
    [DataRow(10_000UL, 0UL, 5_000L, DisplayName = "Only the client declares one")]
    [DataRow(0UL, 4_000UL, 2_000L, DisplayName = "Only the server declares one")]
    public void TimeUntilIdleTimer_BothSidesTimeouts_KeepAliveAtHalfTheSmallerNonZero(ulong clientTimeout, ulong serverTimeout, long expectedMilliseconds)
    {
        using QuicTestServer server = new() { ConfigureTransportParameters = parameters => parameters with { MaxIdleTimeout = serverTimeout } };
        using QuicClientHandshake client = Connect(server, SmallClientLimits with { MaxIdleTimeout = clientTimeout });

        Assert.AreEqual(TimeSpan.FromMilliseconds(expectedMilliseconds), client.TimeUntilIdleTimer);
    }

    [TestMethod]
    public void OnIdleTimer_TimeoutBelowThreeProbeTimeouts_WaitsThreeProbeTimeouts()
    {
        ManualTimerTimeProvider clock = new();
        using QuicTestServer server = new() { ConfigureTransportParameters = parameters => parameters with { MaxIdleTimeout = 10 } };
        using QuicClientHandshake client = Connect(server, clock: clock);
        long floor = (long)(3 * (client.Recovery.Rtt.ProbeTimeout + client.Recovery.MaxAckDelay)).TotalMilliseconds;
        clock.Advance(5);
        Assert.IsNotEmpty(client.OnIdleTimer());

        while (client.Failure is null)
        {
            clock.Advance((long)client.TimeUntilIdleTimer.TotalMilliseconds);
            client.OnIdleTimer();
        }

        Assert.IsGreaterThan(10L, floor);
        Assert.AreEqual(5 + floor, clock.GetTimestamp());
        Assert.AreEqual(CurlExitCode.SendError, client.Failure.ExitCode);
    }

    [TestMethod]
    public void TimeUntilIdleTimer_BeforeTheHandshakeCompletes_IsInfinite()
    {
        using QuicClientHandshake client = QuicHandshakeTest.Client();
        client.Start();

        Assert.AreEqual(Timeout.InfiniteTimeSpan, client.TimeUntilIdleTimer);
        Assert.IsEmpty(client.OnIdleTimer());
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
}
