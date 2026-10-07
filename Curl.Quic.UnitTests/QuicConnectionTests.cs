using System.Net.Sockets;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using static Curl.Quic.QuicStreamTest;

namespace Curl.Quic;

/// <summary>The connection loop and the multiplexed-connection contract over the in-memory server.</summary>
[TestClass]
public sealed class QuicConnectionTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task OpenBidirectionalStreamAsync_RequestAndResponse_CarriesBothWays()
    {
        Diagnostics.Arrange("request", "GET");
        Diagnostics.Arrange("response chunks", "'ok'@2 with fin, then 'is'@0");
        (QuicConnection connection, QuicTestLiveChannel channel, _) = await ConnectAsync();
        await using (connection)
        {
            IMultiplexedStream stream = await connection.OpenBidirectionalStreamAsync(CancellationToken.None);
            using (Diagnostics.Phase("data transfer"))
            {
                await stream.WriteAsync("GET"u8.ToArray(), endStream: true, CancellationToken.None);
                await channel.WaitUntilSentAsync(() => channel.Sent<QuicStreamFrame>().Any(frame => frame.IsFin));
                Task<string> responseTask = ReadToEndAsync(stream);
                channel.FromServer(new QuicStreamFrame(0, 2, "ok"u8.ToArray(), true));
                channel.FromServer(new QuicStreamFrame(0, 0, "is"u8.ToArray(), false));

                string response = await responseTask;
                Diagnostics.Act("response", response);
                Diagnostics.Assert("response", "isok", response);
                Assert.AreEqual("isok", response);
            }

            string request = Encoding.ASCII.GetString(channel.Sent<QuicStreamFrame>().Single().Data.Span);
            Diagnostics.Act("request received by server", request);
            Diagnostics.Assert("request received by server", "GET", request);
            Assert.AreEqual("GET", request);
            Diagnostics.Assert("stream id", 0L, stream.StreamId);
            Assert.AreEqual(0L, stream.StreamId);
            Diagnostics.Assert("application protocol", "h3", connection.ApplicationProtocol);
            Assert.AreEqual("h3", connection.ApplicationProtocol);
            Diagnostics.Act("bidirectional stream limit", connection.BidirectionalStreamLimit);
            Diagnostics.Assert("bidirectional stream limit", "> 0", connection.BidirectionalStreamLimit);
            Assert.IsGreaterThan(0L, connection.BidirectionalStreamLimit!.Value, "the server's MAX_STREAMS for bidirectional streams");
            Diagnostics.Assert("remote end point", QuicTestChannel.ServerAddress, connection.RemoteEndPoint);
            Assert.AreEqual(QuicTestChannel.ServerAddress, connection.RemoteEndPoint);
            Diagnostics.Assert("local end point", QuicTestLiveChannel.ClientAddress, connection.LocalEndPoint);
            Assert.AreEqual(QuicTestLiveChannel.ClientAddress, connection.LocalEndPoint);
            int endRead = await stream.ReadAsync(Memory<byte>.Empty, CancellationToken.None);
            Diagnostics.Assert("read after end", 0, endRead);
            Assert.AreEqual(0, endRead);
            await stream.DisposeAsync();
        }
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task PeerIdleTimeout_ServerDeclaresOne_IsItsMaxIdleTimeout()
    {
        Diagnostics.Arrange("server MaxIdleTimeout (ms)", 180000);
        (QuicConnection connection, _, _) = await ConnectAsync(parameters => GenerousServerLimits(parameters) with { MaxIdleTimeout = 180000 });
        await using (connection)
        {
            Diagnostics.Act("peer idle timeout", connection.PeerIdleTimeout);
            Diagnostics.Assert("peer idle timeout", TimeSpan.FromMilliseconds(180000), connection.PeerIdleTimeout);
            Assert.AreEqual(TimeSpan.FromMilliseconds(180000), connection.PeerIdleTimeout);
        }
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task PeerIdleTimeout_ServerDeclaresNone_IsNull()
    {
        Diagnostics.Arrange("server MaxIdleTimeout (ms)", 0);
        (QuicConnection connection, _, _) = await ConnectAsync(parameters => GenerousServerLimits(parameters) with { MaxIdleTimeout = 0 });
        await using (connection)
        {
            Diagnostics.Act("peer idle timeout", connection.PeerIdleTimeout?.ToString() ?? "null");
            Diagnostics.Assert("peer idle timeout is null", true, connection.PeerIdleTimeout is null);
            Assert.IsNull(connection.PeerIdleTimeout);
        }
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task AcceptUnidirectionalStreamAsync_ServerStream_IsAcceptedAndRead()
    {
        Diagnostics.Arrange("server stream", "3 carrying 'control' with fin; a spoofed 'spoofed' frame from another address");
        (QuicConnection connection, QuicTestLiveChannel channel, _) = await ConnectAsync();
        await using (connection)
        {
            Task<IMultiplexedStream> accept = connection.AcceptUnidirectionalStreamAsync(CancellationToken.None).AsTask();
            IMultiplexedStream stream;
            using (Diagnostics.Phase("data transfer"))
            {
                channel.FromElsewhere(new QuicStreamFrame(3, 0, "spoofed"u8.ToArray(), true));
                channel.FromServer(new QuicStreamFrame(3, 0, "control"u8.ToArray(), true));
                stream = await accept;
            }

            Diagnostics.Act("accepted stream id", stream.StreamId);
            Diagnostics.Assert("accepted stream id", 3L, stream.StreamId);
            Assert.AreEqual(3L, stream.StreamId);
            string text = await ReadToEndAsync(stream);
            Diagnostics.Act("text read", text);
            Diagnostics.Assert("text read", "control", text);
            Assert.AreEqual("control", text);
        }
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task ReadAsync_ServerResetsTheStream_ThrowsWithItsCode()
    {
        Diagnostics.Arrange("RESET_STREAM error code", 0x10c);
        (QuicConnection connection, QuicTestLiveChannel channel, _) = await ConnectAsync();
        await using (connection)
        {
            IMultiplexedStream stream = await connection.OpenBidirectionalStreamAsync(CancellationToken.None);
            await stream.WriteAsync("x"u8.ToArray(), endStream: false, CancellationToken.None);
            Task<int> read = stream.ReadAsync(new byte[10], CancellationToken.None).AsTask();
            channel.FromServer(new QuicResetStreamFrame(0, 0x10c, 0));

            MultiplexedStreamResetException error = await Assert.ThrowsExactlyAsync<MultiplexedStreamResetException>(() => read);
            Diagnostics.Act("exception", error.GetType().Name + ": " + error.Message);
            Diagnostics.Assert("application error code", 0x10cL, error.ApplicationErrorCode);
            Assert.AreEqual(0x10cL, error.ApplicationErrorCode);
        }
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task WriteAsync_AfterStopSending_ThrowsAndTheStreamIsReset()
    {
        Diagnostics.Arrange("bytes written", 3);
        Diagnostics.Arrange("STOP_SENDING error code", 0x10b);
        (QuicConnection connection, QuicTestLiveChannel channel, _) = await ConnectAsync();
        await using (connection)
        {
            IMultiplexedStream stream = await connection.OpenBidirectionalStreamAsync(CancellationToken.None);
            await stream.WriteAsync("abc"u8.ToArray(), endStream: false, CancellationToken.None);
            await channel.WaitUntilSentAsync(() => channel.Sent<QuicStreamFrame>().Count == 1);
            channel.FromServer(new QuicStopSendingFrame(0, 0x10b));
            await channel.WaitUntilSentAsync(() => channel.Sent<QuicResetStreamFrame>().Count == 1);

            MultiplexedStreamResetException error = await Assert.ThrowsExactlyAsync<MultiplexedStreamResetException>(() => stream.WriteAsync("d"u8.ToArray(), false, CancellationToken.None).AsTask());
            QuicResetStreamFrame reset = channel.Sent<QuicResetStreamFrame>().Single();
            Diagnostics.Act("exception", error.GetType().Name + ": " + error.Message);
            Diagnostics.Act("RESET_STREAM sent", reset);
            Diagnostics.Assert("application error code", 0x10bL, error.ApplicationErrorCode);
            Assert.AreEqual(0x10bL, error.ApplicationErrorCode);
            Diagnostics.Assert("RESET_STREAM sent", new QuicResetStreamFrame(0, 0x10b, 3), reset);
            Assert.AreEqual(new QuicResetStreamFrame(0, 0x10b, 3), reset);
        }
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task Abort_SendsResetStreamAndStopSending()
    {
        Diagnostics.Arrange("abort error code", 0x10c);
        (QuicConnection connection, QuicTestLiveChannel channel, _) = await ConnectAsync();
        await using (connection)
        {
            IMultiplexedStream stream = await connection.OpenBidirectionalStreamAsync(CancellationToken.None);

            stream.Abort(0x10c);
            await channel.WaitUntilSentAsync(() => channel.Sent<QuicStopSendingFrame>().Count == 1);

            QuicResetStreamFrame reset = channel.Sent<QuicResetStreamFrame>().Single();
            QuicStopSendingFrame stopSending = channel.Sent<QuicStopSendingFrame>().Single();
            Diagnostics.Act("RESET_STREAM sent", reset);
            Diagnostics.Act("STOP_SENDING sent", stopSending);
            Diagnostics.Assert("RESET_STREAM sent", new QuicResetStreamFrame(0, 0x10c, 0), reset);
            Assert.AreEqual(new QuicResetStreamFrame(0, 0x10c, 0), reset);
            Diagnostics.Assert("STOP_SENDING sent", new QuicStopSendingFrame(0, 0x10c), stopSending);
            Assert.AreEqual(new QuicStopSendingFrame(0, 0x10c), stopSending);
        }
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task OpenUnidirectionalStreamAsync_NoStreamAllowed_WaitsForMaxStreamsAndSendsStreamsBlocked()
    {
        Diagnostics.Arrange("server InitialMaxStreamsUni", 0);
        Diagnostics.Arrange("MAX_STREAMS (uni) value", 1);
        (QuicConnection connection, QuicTestLiveChannel channel, _) = await ConnectAsync(parameters => GenerousServerLimits(parameters) with { InitialMaxStreamsUni = 0 });
        await using (connection)
        {
            Task<IMultiplexedStream> open = connection.OpenUnidirectionalStreamAsync(CancellationToken.None).AsTask();
            await channel.WaitUntilSentAsync(() => channel.Sent<QuicStreamsBlockedFrame>().Count == 1);
            Diagnostics.Act("open completed before MAX_STREAMS", open.IsCompleted);
            Diagnostics.Assert("open completed before MAX_STREAMS", false, open.IsCompleted);
            Assert.IsFalse(open.IsCompleted);
            OperationCanceledException cancelled = await Assert.ThrowsAsync<OperationCanceledException>(() => connection.OpenUnidirectionalStreamAsync(new CancellationToken(true)).AsTask());
            Diagnostics.Act("cancelled open exception", cancelled.GetType().Name);
            Diagnostics.Assert("cancelled open exception is an OperationCanceledException", true, cancelled.GetType().IsAssignableTo(typeof(OperationCanceledException)));

            channel.FromServer(new QuicMaxStreamsFrame(true, 1));

            long streamId = (await open).StreamId;
            Diagnostics.Act("opened stream id", streamId);
            Diagnostics.Assert("opened stream id", 2L, streamId);
            Assert.AreEqual(2L, streamId);
        }
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task ReadAsync_ServerBreaksFlowControl_FailsTheConnectionWithRecvError()
    {
        Diagnostics.Arrange("bytes delivered on stream 0", 101);
        (QuicConnection connection, QuicTestLiveChannel channel, _) = await ConnectAsync();
        await using (connection)
        {
            IMultiplexedStream stream = await connection.OpenBidirectionalStreamAsync(CancellationToken.None);
            Task<int> read = stream.ReadAsync(new byte[10], CancellationToken.None).AsTask();

            channel.FromServer(new QuicStreamFrame(0, 0, Bytes(101), false));

            MultiplexedConnectionFailedException error = await Assert.ThrowsExactlyAsync<MultiplexedConnectionFailedException>(() => read);
            Diagnostics.Act("exception", error.GetType().Name + ": " + error.Message);
            Diagnostics.Assert("exit code", CurlExitCode.RecvError, error.ExitCode);
            Assert.AreEqual(CurlExitCode.RecvError, error.ExitCode);
            await channel.WaitUntilSentAsync(() => channel.Sent<QuicConnectionCloseFrame>().Count == 1);
            ulong closeCode = channel.Sent<QuicConnectionCloseFrame>().Single().ErrorCode;
            Diagnostics.Act("CONNECTION_CLOSE error code", closeCode);
            Diagnostics.Assert("CONNECTION_CLOSE error code", (ulong)QuicTransportErrorCode.FlowControlError, closeCode);
            Assert.AreEqual((ulong)QuicTransportErrorCode.FlowControlError, closeCode);
            MultiplexedConnectionFailedException writeError = await Assert.ThrowsExactlyAsync<MultiplexedConnectionFailedException>(() => stream.WriteAsync("x"u8.ToArray(), false, CancellationToken.None).AsTask());
            Diagnostics.Act("write after failure exception", writeError.GetType().Name + ": " + writeError.Message);
            Diagnostics.Assert("write after failure exit code", CurlExitCode.RecvError, writeError.ExitCode);
        }

        Diagnostics.Assert("CONNECTION_CLOSE frames sent", 1, channel.Sent<QuicConnectionCloseFrame>().Count);
        Assert.HasCount(1, channel.Sent<QuicConnectionCloseFrame>());
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task AcceptUnidirectionalStreamAsync_ReceiveFails_FailsWithRecvError()
    {
        Diagnostics.Arrange("socket error code", 10054);
        (QuicConnection connection, QuicTestLiveChannel channel, _) = await ConnectAsync();
        await using (connection)
        {
            Task<IMultiplexedStream> accept = connection.AcceptUnidirectionalStreamAsync(CancellationToken.None).AsTask();

            channel.FailNextReceive(new SocketException(10054));

            MultiplexedConnectionFailedException error = await Assert.ThrowsExactlyAsync<MultiplexedConnectionFailedException>(() => accept);
            Diagnostics.Act("exception", error.GetType().Name + ": " + error.Message);
            Diagnostics.Assert("exit code", CurlExitCode.RecvError, error.ExitCode);
            Assert.AreEqual(CurlExitCode.RecvError, error.ExitCode);
            Diagnostics.Act("message", error.Message);
            Diagnostics.Assert("message starts with 'QUIC: the datagram channel failed: '", true, error.Message.StartsWith("QUIC: the datagram channel failed: ", StringComparison.Ordinal));
            StringAssert.StartsWith(error.Message, "QUIC: the datagram channel failed: ");
        }
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task AcceptUnidirectionalStreamAsync_ServerClosesTheConnection_FailsWithRecvErrorAndSendsNoClose()
    {
        Diagnostics.Arrange("server CONNECTION_CLOSE error code", (ulong)QuicTransportErrorCode.InternalError);
        (QuicConnection connection, QuicTestLiveChannel channel, _) = await ConnectAsync();
        await using (connection)
        {
            Task<IMultiplexedStream> accept = connection.AcceptUnidirectionalStreamAsync(CancellationToken.None).AsTask();

            channel.FromServer(new QuicConnectionCloseFrame((ulong)QuicTransportErrorCode.InternalError, 0, ReadOnlyMemory<byte>.Empty));

            MultiplexedConnectionFailedException error = await Assert.ThrowsExactlyAsync<MultiplexedConnectionFailedException>(() => accept);
            Diagnostics.Act("exception", error.GetType().Name + ": " + error.Message);
            Diagnostics.Assert("exit code", CurlExitCode.RecvError, error.ExitCode);
            Assert.AreEqual(CurlExitCode.RecvError, error.ExitCode);
            Diagnostics.Assert("message", "Failure when receiving data from the peer", error.Message);
            Assert.AreEqual("Failure when receiving data from the peer", error.Message);
        }

        Diagnostics.Assert("CONNECTION_CLOSE frames sent", 0, channel.Sent<QuicConnectionCloseFrame>().Count);
        Assert.IsEmpty(channel.Sent<QuicConnectionCloseFrame>());
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task Loop_ServerGoesSilent_SendsKeepAlivesThenFailsWithTheIdleTimeout()
    {
        Diagnostics.Arrange("server MaxIdleTimeout (ms)", 400);
        (QuicConnection connection, QuicTestLiveChannel channel, ManualTimerTimeProvider clock) = await ConnectAsync(parameters => GenerousServerLimits(parameters) with { MaxIdleTimeout = 400 });
        await using (connection)
        {
            Task<IMultiplexedStream> accept = connection.AcceptUnidirectionalStreamAsync(CancellationToken.None).AsTask();
            channel.Silence();

            using (Diagnostics.Phase("idle timers"))
            {
                await RunTimersUntilAsync(clock, accept);
            }

            MultiplexedConnectionFailedException error = await Assert.ThrowsExactlyAsync<MultiplexedConnectionFailedException>(() => accept);

            Diagnostics.Act("exception", error.GetType().Name + ": " + error.Message);
            Diagnostics.Act("PING frames sent", channel.Sent<QuicPingFrame>().Count);
            Diagnostics.Assert("exit code", CurlExitCode.SendError, error.ExitCode);
            Assert.AreEqual(CurlExitCode.SendError, error.ExitCode);
            Diagnostics.Assert("message", "ngtcp2_conn_handle_expiry returned error: ERR_IDLE_CLOSE", error.Message);
            Assert.AreEqual("ngtcp2_conn_handle_expiry returned error: ERR_IDLE_CLOSE", error.Message);
            Diagnostics.Assert("PING frames sent", "> 0", channel.Sent<QuicPingFrame>().Count);
            Assert.IsNotEmpty(channel.Sent<QuicPingFrame>());
        }

        Diagnostics.Assert("CONNECTION_CLOSE frames sent", 0, channel.Sent<QuicConnectionCloseFrame>().Count);
        Assert.IsEmpty(channel.Sent<QuicConnectionCloseFrame>());
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task Loop_UnacknowledgedStreamData_IsProbedWhenTheLossDetectionTimerFires()
    {
        Diagnostics.Arrange("payload", "lost (4 bytes, fin) on a silenced channel");
        (QuicConnection connection, QuicTestLiveChannel channel, ManualTimerTimeProvider clock) = await ConnectAsync();
        await using (connection)
        {
            IMultiplexedStream stream = await connection.OpenBidirectionalStreamAsync(CancellationToken.None);
            channel.Silence();

            using (Diagnostics.Phase("data transfer"))
            {
                await stream.WriteAsync("lost"u8.ToArray(), endStream: true, CancellationToken.None);
                await channel.WaitUntilSentAsync(() => channel.Sent<QuicStreamFrame>().Count == 1);

                await RunTimersUntilAsync(clock, () => channel.Sent<QuicStreamFrame>().Count >= 2);
            }

            bool allResent = channel.Sent<QuicStreamFrame>().All(frame => frame.StreamId == 0 && frame.IsFin);
            Diagnostics.Act("STREAM frames sent", channel.Sent<QuicStreamFrame>().Count);
            Diagnostics.Assert("all frames are stream 0 with fin", true, allResent);
            Assert.IsTrue(allResent);
        }
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task CloseAsync_SendsTheApplicationCloseOnceAndRefusesFurtherUse()
    {
        Diagnostics.Arrange("application error code", 0x100);
        (QuicConnection connection, QuicTestLiveChannel channel, _) = await ConnectAsync();

        await connection.CloseAsync(0x100, CancellationToken.None);
        List<QuicConnectionCloseFrame> closes = channel.Sent<QuicConnectionCloseFrame>();
        ObjectDisposedException openError = await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => connection.OpenBidirectionalStreamAsync(CancellationToken.None).AsTask());
        ObjectDisposedException closeError = await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => connection.CloseAsync(0, CancellationToken.None).AsTask());
        await connection.DisposeAsync();

        Diagnostics.Act("open after close", openError.GetType().Name);
        Diagnostics.Act("close after close", closeError.GetType().Name);
        Diagnostics.Act("CONNECTION_CLOSE frames", closes.Count);
        Diagnostics.Assert("CONNECTION_CLOSE error code", 0x100UL, closes.Single().ErrorCode);
        Assert.AreEqual(0x100UL, closes.Single().ErrorCode);
        Diagnostics.Assert("channel disposed", true, channel.IsDisposed);
        Assert.IsTrue(channel.IsDisposed);
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task DisposeAsync_OpenConnection_ClosesWithApplicationErrorZero()
    {
        Diagnostics.Arrange("connection", "open, never used");
        (QuicConnection connection, QuicTestLiveChannel channel, _) = await ConnectAsync();

        await connection.DisposeAsync();

        ulong errorCode = channel.Sent<QuicConnectionCloseFrame>().Single().ErrorCode;
        Diagnostics.Act("CONNECTION_CLOSE error code", errorCode);
        Diagnostics.Assert("CONNECTION_CLOSE error code", 0UL, errorCode);
        Assert.AreEqual(0UL, errorCode);
    }

    [TestMethod]
    public void Constructor_IncompleteHandshakeOrMissingArguments_Throws()
    {
        Diagnostics.Arrange("handshake", "started, not complete");
        using QuicClientConnectionState handshake = QuicClientConnectionStateTest.Client();
        QuicTestChannel channel = new(null);

        ArgumentException incomplete = Assert.ThrowsExactly<ArgumentException>(() => new QuicConnection(handshake, channel, TimeProvider.System));
        ArgumentNullException noHandshake = Assert.ThrowsExactly<ArgumentNullException>(() => new QuicConnection(null!, channel, TimeProvider.System));
        ArgumentNullException noChannel = Assert.ThrowsExactly<ArgumentNullException>(() => new QuicConnection(handshake, null!, TimeProvider.System));
        ArgumentNullException noClock = Assert.ThrowsExactly<ArgumentNullException>(() => new QuicConnection(handshake, channel, null!));

        Diagnostics.Act("incomplete handshake", incomplete.GetType().Name + ": " + incomplete.Message);
        Diagnostics.Act("null handshake parameter", noHandshake.ParamName);
        Diagnostics.Act("null channel parameter", noChannel.ParamName);
        Diagnostics.Act("null time provider parameter", noClock.ParamName);
        Diagnostics.Assert("incomplete handshake exception type", nameof(ArgumentException), incomplete.GetType().Name);
        Diagnostics.Assert("null argument exception types", nameof(ArgumentNullException), noHandshake.GetType().Name);
    }

    [TestMethod]
    public void Constructor_FailedHandshake_Throws()
    {
        Diagnostics.Arrange("handshake", "complete, then abandoned with InternalError and exit code SendError");
        using QuicTestServer server = new();
        using QuicClientConnectionState handshake = Connect(server);
        handshake.Abandon(QuicTransportErrorCode.InternalError, new QuicHandshakeFailure(CurlExitCode.SendError, "gone"));

        ArgumentException error = Assert.ThrowsExactly<ArgumentException>(() => new QuicConnection(handshake, new QuicTestChannel(null), TimeProvider.System));

        Diagnostics.Act("exception", error.GetType().Name + ": " + error.Message);
        Diagnostics.Assert("exception type", nameof(ArgumentException), error.GetType().Name);
    }

    // The handshake, the connector and the connection all run on one manual clock, which moves
    // only when a test moves it, so no timer fires unless the test drives it there.
    private async Task<(QuicConnection Connection, QuicTestLiveChannel Channel, ManualTimerTimeProvider Clock)> ConnectAsync(Func<QuicTransportParameters, QuicTransportParameters>? serverLimits = null)
    {
        ManualTimerTimeProvider clock = new();
        QuicTestServer server = new() { ConfigureTransportParameters = serverLimits ?? GenerousServerLimits };
        QuicTestLiveChannel channel = new(server);
        QuicClientConnectionState handshake = QuicClientConnectionStateTest.Client(QuicClientConnectionStateTest.CurlSettings with { TransportParameters = SmallClientLimits }, clock: clock);
        object? handshakeFailure;
        using (Diagnostics.Phase("handshake"))
        {
            handshakeFailure = await new QuicClientConnector(clock).RunHandshakeAsync(handshake, channel, null, CancellationToken.None);
        }

        Diagnostics.Act("handshake failure", handshakeFailure?.ToString() ?? "none");
        Assert.IsNull(handshakeFailure);
        return (new QuicConnection(handshake, channel, clock), channel, clock);
    }

    /// <summary>
    /// Moves <paramref name="clock" /> from one timer to the next until <paramref name="outcome" />
    /// completes: each time the connection's loop has armed a timer, the clock goes to its due time
    /// and no further, so no timer is skipped however slowly the loop runs.
    /// </summary>
    private static async Task RunTimersUntilAsync(ManualTimerTimeProvider clock, Task outcome)
    {
        while (!outcome.IsCompleted)
        {
            await Task.WhenAny(outcome, clock.WaitForPendingTimerAsync()).WaitAsync(QuicTestLiveChannel.HangGuard);
            if (!outcome.IsCompleted)
            {
                clock.AdvanceToNextTimer();
            }
        }
    }

    /// <summary>
    /// Moves <paramref name="clock" /> from one timer to the next until <paramref name="sent" />, a
    /// question about what the server has taken from the client, holds; asked each time the loop
    /// has armed a timer, which it does only after sending what the last one queued.
    /// </summary>
    private static async Task RunTimersUntilAsync(ManualTimerTimeProvider clock, Func<bool> sent)
    {
        while (!sent())
        {
            await clock.WaitForPendingTimerAsync().WaitAsync(QuicTestLiveChannel.HangGuard);
            if (!sent())
            {
                clock.AdvanceToNextTimer();
            }
        }
    }

    private static async Task<string> ReadToEndAsync(IMultiplexedStream stream)
    {
        StringBuilder text = new();
        byte[] buffer = new byte[1];
        int read;
        while ((read = await stream.ReadAsync(buffer, CancellationToken.None)) > 0)
        {
            text.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }

        return text.ToString();
    }
}
