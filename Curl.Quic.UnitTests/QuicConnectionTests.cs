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
    public async Task OpenBidirectionalStreamAsync_RequestAndResponse_CarriesBothWays()
    {
        (QuicConnection connection, QuicTestLiveChannel channel) = await ConnectAsync();
        await using (connection)
        {
            IMultiplexedStream stream = await connection.OpenBidirectionalStreamAsync(CancellationToken.None);
            await stream.WriteAsync("GET"u8.ToArray(), endStream: true, CancellationToken.None);
            await QuicTestLiveChannel.WaitUntilAsync(() => channel.Sent<QuicStreamFrame>().Any(frame => frame.IsFin));
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
    public async Task AcceptUnidirectionalStreamAsync_ServerStream_IsAcceptedAndRead()
    {
        (QuicConnection connection, QuicTestLiveChannel channel) = await ConnectAsync();
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
    public async Task ReadAsync_ServerResetsTheStream_ThrowsWithItsCode()
    {
        (QuicConnection connection, QuicTestLiveChannel channel) = await ConnectAsync();
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
    public async Task WriteAsync_AfterStopSending_ThrowsAndTheStreamIsReset()
    {
        (QuicConnection connection, QuicTestLiveChannel channel) = await ConnectAsync();
        await using (connection)
        {
            IMultiplexedStream stream = await connection.OpenBidirectionalStreamAsync(CancellationToken.None);
            await stream.WriteAsync("abc"u8.ToArray(), endStream: false, CancellationToken.None);
            await QuicTestLiveChannel.WaitUntilAsync(() => channel.Sent<QuicStreamFrame>().Count == 1);
            channel.FromServer(new QuicStopSendingFrame(0, 0x10b));
            await QuicTestLiveChannel.WaitUntilAsync(() => channel.Sent<QuicResetStreamFrame>().Count == 1);

            MultiplexedStreamResetException error = await Assert.ThrowsExactlyAsync<MultiplexedStreamResetException>(() => stream.WriteAsync("d"u8.ToArray(), false, CancellationToken.None).AsTask());
            Assert.AreEqual(0x10bL, error.ApplicationErrorCode);
            Assert.AreEqual(new QuicResetStreamFrame(0, 0x10b, 3), channel.Sent<QuicResetStreamFrame>().Single());
        }
    }

    [TestMethod]
    public async Task Abort_SendsResetStreamAndStopSending()
    {
        (QuicConnection connection, QuicTestLiveChannel channel) = await ConnectAsync();
        await using (connection)
        {
            IMultiplexedStream stream = await connection.OpenBidirectionalStreamAsync(CancellationToken.None);

            stream.Abort(0x10c);
            await QuicTestLiveChannel.WaitUntilAsync(() => channel.Sent<QuicStopSendingFrame>().Count == 1);

            Assert.AreEqual(new QuicResetStreamFrame(0, 0x10c, 0), channel.Sent<QuicResetStreamFrame>().Single());
            Assert.AreEqual(new QuicStopSendingFrame(0, 0x10c), channel.Sent<QuicStopSendingFrame>().Single());
        }
    }

    [TestMethod]
    public async Task OpenUnidirectionalStreamAsync_NoStreamAllowed_WaitsForMaxStreamsAndSendsStreamsBlocked()
    {
        (QuicConnection connection, QuicTestLiveChannel channel) = await ConnectAsync(parameters => GenerousServerLimits(parameters) with { InitialMaxStreamsUni = 0 });
        await using (connection)
        {
            Task<IMultiplexedStream> open = connection.OpenUnidirectionalStreamAsync(CancellationToken.None).AsTask();
            await QuicTestLiveChannel.WaitUntilAsync(() => channel.Sent<QuicStreamsBlockedFrame>().Count == 1);
            Assert.IsFalse(open.IsCompleted);
            await Assert.ThrowsAsync<OperationCanceledException>(() => connection.OpenUnidirectionalStreamAsync(new CancellationToken(true)).AsTask());

            channel.FromServer(new QuicMaxStreamsFrame(true, 1));

            Assert.AreEqual(2L, (await open).StreamId);
        }
    }

    [TestMethod]
    public async Task ReadAsync_ServerBreaksFlowControl_FailsTheConnection()
    {
        (QuicConnection connection, QuicTestLiveChannel channel) = await ConnectAsync();
        await using (connection)
        {
            IMultiplexedStream stream = await connection.OpenBidirectionalStreamAsync(CancellationToken.None);
            Task<int> read = stream.ReadAsync(new byte[10], CancellationToken.None).AsTask();

            channel.FromServer(new QuicStreamFrame(0, 0, Bytes(101), false));

            MultiplexedConnectionFailedException error = await Assert.ThrowsExactlyAsync<MultiplexedConnectionFailedException>(() => read);
            Assert.AreEqual(CurlExitCode.CouldntConnect, error.ExitCode);
            await QuicTestLiveChannel.WaitUntilAsync(() => channel.Sent<QuicConnectionCloseFrame>().Count == 1);
            Assert.AreEqual((ulong)QuicTransportErrorCode.FlowControlError, channel.Sent<QuicConnectionCloseFrame>().Single().ErrorCode);
            await Assert.ThrowsExactlyAsync<MultiplexedConnectionFailedException>(() => stream.WriteAsync("x"u8.ToArray(), false, CancellationToken.None).AsTask());
        }

        Assert.HasCount(1, channel.Sent<QuicConnectionCloseFrame>());
    }

    [TestMethod]
    public async Task AcceptUnidirectionalStreamAsync_ReceiveFails_FailsWithRecvError()
    {
        (QuicConnection connection, QuicTestLiveChannel channel) = await ConnectAsync();
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
    public async Task Loop_UnacknowledgedStreamData_IsProbedWhenTheLossDetectionTimerFires()
    {
        (QuicConnection connection, QuicTestLiveChannel channel) = await ConnectAsync();
        await using (connection)
        {
            IMultiplexedStream stream = await connection.OpenBidirectionalStreamAsync(CancellationToken.None);
            channel.Silence();

            await stream.WriteAsync("lost"u8.ToArray(), endStream: true, CancellationToken.None);

            await QuicTestLiveChannel.WaitUntilAsync(() => channel.Sent<QuicStreamFrame>().Count >= 2);
            Assert.IsTrue(channel.Sent<QuicStreamFrame>().All(frame => frame.StreamId == 0 && frame.IsFin));
        }
    }

    [TestMethod]
    public async Task CloseAsync_SendsTheApplicationCloseOnceAndRefusesFurtherUse()
    {
        (QuicConnection connection, QuicTestLiveChannel channel) = await ConnectAsync();

        await connection.CloseAsync(0x100, CancellationToken.None);
        List<QuicConnectionCloseFrame> closes = channel.Sent<QuicConnectionCloseFrame>();
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => connection.OpenBidirectionalStreamAsync(CancellationToken.None).AsTask());
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => connection.CloseAsync(0, CancellationToken.None).AsTask());
        await connection.DisposeAsync();

        Assert.AreEqual(0x100UL, closes.Single().ErrorCode);
        Assert.IsTrue(channel.IsDisposed);
    }

    [TestMethod]
    public async Task DisposeAsync_OpenConnection_ClosesWithApplicationErrorZero()
    {
        (QuicConnection connection, QuicTestLiveChannel channel) = await ConnectAsync();

        await connection.DisposeAsync();

        Assert.AreEqual(0UL, channel.Sent<QuicConnectionCloseFrame>().Single().ErrorCode);
    }

    [TestMethod]
    public void Constructor_IncompleteHandshakeOrMissingArguments_Throws()
    {
        using QuicClientHandshake handshake = QuicHandshakeTest.Client();
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
        using QuicClientHandshake handshake = Connect(server);
        handshake.Abandon(QuicTransportErrorCode.InternalError, new QuicHandshakeFailure(CurlExitCode.SendError, "gone"));

        Assert.ThrowsExactly<ArgumentException>(() => new QuicConnection(handshake, new QuicTestChannel(null), TimeProvider.System));
    }

    private static async Task<(QuicConnection Connection, QuicTestLiveChannel Channel)> ConnectAsync(Func<QuicTransportParameters, QuicTransportParameters>? serverLimits = null)
    {
        QuicTestServer server = new() { ConfigureTransportParameters = serverLimits ?? GenerousServerLimits };
        QuicTestLiveChannel channel = new(server);
        QuicClientHandshake handshake = new(QuicHandshakeTest.CurlSettings with { TransportParameters = SmallClientLimits }, new QuicTestRandomSource(), new QuicTestVerifier(), TimeProvider.System);
        Assert.IsNull(await new QuicClientConnector(TimeProvider.System).RunHandshakeAsync(handshake, channel, null, CancellationToken.None));
        return (new QuicConnection(handshake, channel, TimeProvider.System), channel);
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
