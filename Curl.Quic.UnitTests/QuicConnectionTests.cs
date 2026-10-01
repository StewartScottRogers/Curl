using System.Net.Sockets;
using System.Text;
using Curl.Protocol.Abstractions;
using static Curl.Quic.QuicStreamTest;

namespace Curl.Quic;

/// <summary>The connection loop and the multiplexed-connection contract over the in-memory server.</summary>
[TestClass]
public sealed class QuicConnectionTests
{
    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task OpenBidirectionalStreamAsync_RequestAndResponse_CarriesBothWays()
    {
        (QuicConnection connection, QuicTestLiveChannel channel, _) = await ConnectAsync();
        await using (connection)
        {
            IMultiplexedStream stream = await connection.OpenBidirectionalStreamAsync(CancellationToken.None);
            await stream.WriteAsync("GET"u8.ToArray(), endStream: true, CancellationToken.None);
            await channel.WaitUntilSentAsync(() => channel.Sent<QuicStreamFrame>().Any(frame => frame.IsFin));
            Task<string> response = ReadToEndAsync(stream);
            channel.FromServer(new QuicStreamFrame(0, 2, "ok"u8.ToArray(), true));
            channel.FromServer(new QuicStreamFrame(0, 0, "is"u8.ToArray(), false));

            Assert.AreEqual("isok", await response);
            Assert.AreEqual("GET", Encoding.ASCII.GetString(channel.Sent<QuicStreamFrame>().Single().Data.Span));
            Assert.AreEqual(0L, stream.StreamId);
            Assert.AreEqual("h3", connection.ApplicationProtocol);
            Assert.AreEqual(QuicTestChannel.ServerAddress, connection.RemoteEndPoint);
            Assert.AreEqual(QuicTestLiveChannel.ClientAddress, connection.LocalEndPoint);
            Assert.AreEqual(0, await stream.ReadAsync(Memory<byte>.Empty, CancellationToken.None));
            await stream.DisposeAsync();
        }
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task AcceptUnidirectionalStreamAsync_ServerStream_IsAcceptedAndRead()
    {
        (QuicConnection connection, QuicTestLiveChannel channel, _) = await ConnectAsync();
        await using (connection)
        {
            Task<IMultiplexedStream> accept = connection.AcceptUnidirectionalStreamAsync(CancellationToken.None).AsTask();
            channel.FromElsewhere(new QuicStreamFrame(3, 0, "spoofed"u8.ToArray(), true));
            channel.FromServer(new QuicStreamFrame(3, 0, "control"u8.ToArray(), true));
            IMultiplexedStream stream = await accept;

            Assert.AreEqual(3L, stream.StreamId);
            Assert.AreEqual("control", await ReadToEndAsync(stream));
        }
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task ReadAsync_ServerResetsTheStream_ThrowsWithItsCode()
    {
        (QuicConnection connection, QuicTestLiveChannel channel, _) = await ConnectAsync();
        await using (connection)
        {
            IMultiplexedStream stream = await connection.OpenBidirectionalStreamAsync(CancellationToken.None);
            await stream.WriteAsync("x"u8.ToArray(), endStream: false, CancellationToken.None);
            Task<int> read = stream.ReadAsync(new byte[10], CancellationToken.None).AsTask();
            channel.FromServer(new QuicResetStreamFrame(0, 0x10c, 0));

            MultiplexedStreamResetException error = await Assert.ThrowsExactlyAsync<MultiplexedStreamResetException>(() => read);
            Assert.AreEqual(0x10cL, error.ApplicationErrorCode);
        }
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task WriteAsync_AfterStopSending_ThrowsAndTheStreamIsReset()
    {
        (QuicConnection connection, QuicTestLiveChannel channel, _) = await ConnectAsync();
        await using (connection)
        {
            IMultiplexedStream stream = await connection.OpenBidirectionalStreamAsync(CancellationToken.None);
            await stream.WriteAsync("abc"u8.ToArray(), endStream: false, CancellationToken.None);
            await channel.WaitUntilSentAsync(() => channel.Sent<QuicStreamFrame>().Count == 1);
            channel.FromServer(new QuicStopSendingFrame(0, 0x10b));
            await channel.WaitUntilSentAsync(() => channel.Sent<QuicResetStreamFrame>().Count == 1);

            MultiplexedStreamResetException error = await Assert.ThrowsExactlyAsync<MultiplexedStreamResetException>(() => stream.WriteAsync("d"u8.ToArray(), false, CancellationToken.None).AsTask());
            Assert.AreEqual(0x10bL, error.ApplicationErrorCode);
            Assert.AreEqual(new QuicResetStreamFrame(0, 0x10b, 3), channel.Sent<QuicResetStreamFrame>().Single());
        }
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task Abort_SendsResetStreamAndStopSending()
    {
        (QuicConnection connection, QuicTestLiveChannel channel, _) = await ConnectAsync();
        await using (connection)
        {
            IMultiplexedStream stream = await connection.OpenBidirectionalStreamAsync(CancellationToken.None);

            stream.Abort(0x10c);
            await channel.WaitUntilSentAsync(() => channel.Sent<QuicStopSendingFrame>().Count == 1);

            Assert.AreEqual(new QuicResetStreamFrame(0, 0x10c, 0), channel.Sent<QuicResetStreamFrame>().Single());
            Assert.AreEqual(new QuicStopSendingFrame(0, 0x10c), channel.Sent<QuicStopSendingFrame>().Single());
        }
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task OpenUnidirectionalStreamAsync_NoStreamAllowed_WaitsForMaxStreamsAndSendsStreamsBlocked()
    {
        (QuicConnection connection, QuicTestLiveChannel channel, _) = await ConnectAsync(parameters => GenerousServerLimits(parameters) with { InitialMaxStreamsUni = 0 });
        await using (connection)
        {
            Task<IMultiplexedStream> open = connection.OpenUnidirectionalStreamAsync(CancellationToken.None).AsTask();
            await channel.WaitUntilSentAsync(() => channel.Sent<QuicStreamsBlockedFrame>().Count == 1);
            Assert.IsFalse(open.IsCompleted);
            await Assert.ThrowsAsync<OperationCanceledException>(() => connection.OpenUnidirectionalStreamAsync(new CancellationToken(true)).AsTask());

            channel.FromServer(new QuicMaxStreamsFrame(true, 1));

            Assert.AreEqual(2L, (await open).StreamId);
        }
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task ReadAsync_ServerBreaksFlowControl_FailsTheConnectionWithRecvError()
    {
        (QuicConnection connection, QuicTestLiveChannel channel, _) = await ConnectAsync();
        await using (connection)
        {
            IMultiplexedStream stream = await connection.OpenBidirectionalStreamAsync(CancellationToken.None);
            Task<int> read = stream.ReadAsync(new byte[10], CancellationToken.None).AsTask();

            channel.FromServer(new QuicStreamFrame(0, 0, Bytes(101), false));

            MultiplexedConnectionFailedException error = await Assert.ThrowsExactlyAsync<MultiplexedConnectionFailedException>(() => read);
            Assert.AreEqual(CurlExitCode.RecvError, error.ExitCode);
            await channel.WaitUntilSentAsync(() => channel.Sent<QuicConnectionCloseFrame>().Count == 1);
            Assert.AreEqual((ulong)QuicTransportErrorCode.FlowControlError, channel.Sent<QuicConnectionCloseFrame>().Single().ErrorCode);
            await Assert.ThrowsExactlyAsync<MultiplexedConnectionFailedException>(() => stream.WriteAsync("x"u8.ToArray(), false, CancellationToken.None).AsTask());
        }

        Assert.HasCount(1, channel.Sent<QuicConnectionCloseFrame>());
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task AcceptUnidirectionalStreamAsync_ReceiveFails_FailsWithRecvError()
    {
        (QuicConnection connection, QuicTestLiveChannel channel, _) = await ConnectAsync();
        await using (connection)
        {
            Task<IMultiplexedStream> accept = connection.AcceptUnidirectionalStreamAsync(CancellationToken.None).AsTask();

            channel.FailNextReceive(new SocketException(10054));

            MultiplexedConnectionFailedException error = await Assert.ThrowsExactlyAsync<MultiplexedConnectionFailedException>(() => accept);
            Assert.AreEqual(CurlExitCode.RecvError, error.ExitCode);
            StringAssert.StartsWith(error.Message, "QUIC: the datagram channel failed: ");
        }
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task AcceptUnidirectionalStreamAsync_ServerClosesTheConnection_FailsWithRecvErrorAndSendsNoClose()
    {
        (QuicConnection connection, QuicTestLiveChannel channel, _) = await ConnectAsync();
        await using (connection)
        {
            Task<IMultiplexedStream> accept = connection.AcceptUnidirectionalStreamAsync(CancellationToken.None).AsTask();

            channel.FromServer(new QuicConnectionCloseFrame((ulong)QuicTransportErrorCode.InternalError, 0, ReadOnlyMemory<byte>.Empty));

            MultiplexedConnectionFailedException error = await Assert.ThrowsExactlyAsync<MultiplexedConnectionFailedException>(() => accept);
            Assert.AreEqual(CurlExitCode.RecvError, error.ExitCode);
            Assert.AreEqual("Failure when receiving data from the peer", error.Message);
        }

        Assert.IsEmpty(channel.Sent<QuicConnectionCloseFrame>());
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task Loop_ServerGoesSilent_SendsKeepAlivesThenFailsWithTheIdleTimeout()
    {
        (QuicConnection connection, QuicTestLiveChannel channel, ManualTimerTimeProvider clock) = await ConnectAsync(parameters => GenerousServerLimits(parameters) with { MaxIdleTimeout = 400 });
        await using (connection)
        {
            Task<IMultiplexedStream> accept = connection.AcceptUnidirectionalStreamAsync(CancellationToken.None).AsTask();
            channel.Silence();

            await RunTimersUntilAsync(clock, accept);

            MultiplexedConnectionFailedException error = await Assert.ThrowsExactlyAsync<MultiplexedConnectionFailedException>(() => accept);

            Assert.AreEqual(CurlExitCode.SendError, error.ExitCode);
            Assert.AreEqual("ngtcp2_conn_handle_expiry returned error: ERR_IDLE_CLOSE", error.Message);
            Assert.IsNotEmpty(channel.Sent<QuicPingFrame>());
        }

        Assert.IsEmpty(channel.Sent<QuicConnectionCloseFrame>());
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task Loop_UnacknowledgedStreamData_IsProbedWhenTheLossDetectionTimerFires()
    {
        (QuicConnection connection, QuicTestLiveChannel channel, ManualTimerTimeProvider clock) = await ConnectAsync();
        await using (connection)
        {
            IMultiplexedStream stream = await connection.OpenBidirectionalStreamAsync(CancellationToken.None);
            channel.Silence();

            await stream.WriteAsync("lost"u8.ToArray(), endStream: true, CancellationToken.None);
            await channel.WaitUntilSentAsync(() => channel.Sent<QuicStreamFrame>().Count == 1);

            await RunTimersUntilAsync(clock, () => channel.Sent<QuicStreamFrame>().Count >= 2);
            Assert.IsTrue(channel.Sent<QuicStreamFrame>().All(frame => frame.StreamId == 0 && frame.IsFin));
        }
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task CloseAsync_SendsTheApplicationCloseOnceAndRefusesFurtherUse()
    {
        (QuicConnection connection, QuicTestLiveChannel channel, _) = await ConnectAsync();

        await connection.CloseAsync(0x100, CancellationToken.None);
        List<QuicConnectionCloseFrame> closes = channel.Sent<QuicConnectionCloseFrame>();
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => connection.OpenBidirectionalStreamAsync(CancellationToken.None).AsTask());
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => connection.CloseAsync(0, CancellationToken.None).AsTask());
        await connection.DisposeAsync();

        Assert.AreEqual(0x100UL, closes.Single().ErrorCode);
        Assert.IsTrue(channel.IsDisposed);
    }

    [TestMethod]
    [Timeout(QuicTest.HangTimeoutMilliseconds)]
    public async Task DisposeAsync_OpenConnection_ClosesWithApplicationErrorZero()
    {
        (QuicConnection connection, QuicTestLiveChannel channel, _) = await ConnectAsync();

        await connection.DisposeAsync();

        Assert.AreEqual(0UL, channel.Sent<QuicConnectionCloseFrame>().Single().ErrorCode);
    }

    [TestMethod]
    public void Constructor_IncompleteHandshakeOrMissingArguments_Throws()
    {
        using QuicClientConnectionState handshake = QuicClientConnectionStateTest.Client();
        QuicTestChannel channel = new(null);

        Assert.ThrowsExactly<ArgumentException>(() => new QuicConnection(handshake, channel, TimeProvider.System));
        Assert.ThrowsExactly<ArgumentNullException>(() => new QuicConnection(null!, channel, TimeProvider.System));
        Assert.ThrowsExactly<ArgumentNullException>(() => new QuicConnection(handshake, null!, TimeProvider.System));
        Assert.ThrowsExactly<ArgumentNullException>(() => new QuicConnection(handshake, channel, null!));
    }

    [TestMethod]
    public void Constructor_FailedHandshake_Throws()
    {
        using QuicTestServer server = new();
        using QuicClientConnectionState handshake = Connect(server);
        handshake.Abandon(QuicTransportErrorCode.InternalError, new QuicHandshakeFailure(CurlExitCode.SendError, "gone"));

        Assert.ThrowsExactly<ArgumentException>(() => new QuicConnection(handshake, new QuicTestChannel(null), TimeProvider.System));
    }

    // The handshake, the connector and the connection all run on one manual clock, which moves
    // only when a test moves it, so no timer fires unless the test drives it there.
    private static async Task<(QuicConnection Connection, QuicTestLiveChannel Channel, ManualTimerTimeProvider Clock)> ConnectAsync(Func<QuicTransportParameters, QuicTransportParameters>? serverLimits = null)
    {
        ManualTimerTimeProvider clock = new();
        QuicTestServer server = new() { ConfigureTransportParameters = serverLimits ?? GenerousServerLimits };
        QuicTestLiveChannel channel = new(server);
        QuicClientConnectionState handshake = QuicClientConnectionStateTest.Client(QuicClientConnectionStateTest.CurlSettings with { TransportParameters = SmallClientLimits }, clock: clock);
        Assert.IsNull(await new QuicClientConnector(clock).RunHandshakeAsync(handshake, channel, null, CancellationToken.None));
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
