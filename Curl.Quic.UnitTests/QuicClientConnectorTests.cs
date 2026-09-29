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
        using QuicClientHandshake handshake = QuicHandshakeTest.Client();

        Task<QuicHandshakeFailure?> run = new QuicClientConnector(clock).RunHandshakeAsync(handshake, channel, null, CancellationToken.None);
        clock.Advance(9999);
        Assert.IsFalse(run.IsCompleted);
        clock.Advance(1);
        QuicHandshakeFailure? failure = await run;

        Assert.AreEqual(CurlExitCode.SendError, failure!.ExitCode);
        Assert.AreEqual("ngtcp2_conn_handle_expiry returned error: ERR_HANDSHAKE_TIMEOUT", failure.Message);
        Assert.HasCount(2, channel.Sent);
        Assert.AreEqual(1200, channel.Sent[1].Length);
        Assert.AreEqual((ulong)QuicTransportErrorCode.InternalError, ClientClose(channel.Sent[1]).ErrorCode);
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
