using System.Text;
using Curl.Testing;
using static Curl.Quic.QuicStreamTest;

namespace Curl.Quic;

/// <summary>Streams and flow control carried by a client whose handshake with the in-memory server is complete (RFC 9000 sections 2 to 4).</summary>
[TestClass]
public sealed class QuicClientStreamsTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Receive_ReorderedAndDuplicatedStreamFrames_ReassemblesTheServersStreamInOrder()
    {
        Diagnostics.Arrange("server stream", "3, chunks 'world'@5, 'hello'@0, 'hel'@0, 'lowo'@3, '!'@10 with fin");
        using QuicTestServer server = new();
        using QuicClientConnectionState client = ConnectClient(server);

        using (Diagnostics.Phase("data transfer"))
        {
            Deliver(client, server, new QuicStreamFrame(3, 5, "world"u8.ToArray(), false));
            Deliver(client, server, new QuicStreamFrame(3, 0, "hello"u8.ToArray(), false), new QuicStreamFrame(3, 0, "hel"u8.ToArray(), false));
            Deliver(client, server, new QuicStreamFrame(3, 3, "lowo"u8.ToArray(), false), new QuicStreamFrame(3, 10, "!"u8.ToArray(), true));
        }

        QuicStream stream = client.Streams.AcceptUnidirectional()!;

        string text = Encoding.ASCII.GetString(ReadAll(stream));
        Diagnostics.Act("stream id", stream.Id);
        Diagnostics.Act("text read", text);
        Diagnostics.Assert("stream id", 3UL, stream.Id);
        Assert.AreEqual(3UL, stream.Id);
        Diagnostics.Diff("text read", "helloworld!", text);
        Assert.AreEqual("helloworld!", text);
        Diagnostics.Assert("read complete", true, stream.IsReadComplete);
        Assert.IsTrue(stream.IsReadComplete);
        QuicStream? another = client.Streams.AcceptUnidirectional();
        Diagnostics.Assert("another stream accepted", false, another is not null);
        Assert.IsNull(another);
        Diagnostics.Assert("client failure", "none", client.Failure?.ToString() ?? "none");
        Assert.IsNull(client.Failure);
    }

    [TestMethod]
    public void TakeDatagramsToSend_PastTheServersLimits_StopsReportsBlockedAndResumesAfterMaxStreamDataAndMaxData()
    {
        Diagnostics.Arrange("server InitialMaxStreamDataBidiRemote", 10);
        Diagnostics.Arrange("server InitialMaxData", 15);
        Diagnostics.Arrange("payload length", 30);
        using QuicTestServer server = new()
        {
            ConfigureTransportParameters = parameters => GenerousServerLimits(parameters) with { InitialMaxStreamDataBidiRemote = 10, InitialMaxData = 15 },
        };
        using QuicClientConnectionState client = ConnectClient(server);
        QuicStream stream = client.Streams.OpenBidirectional()!;

        using (Diagnostics.Phase("data transfer"))
        {
            stream.Write(Bytes(30), endStream: true);
            Flush(client, server);
        }

        int afterStreamLimit = Sent<QuicStreamFrame>(server).Sum(frame => frame.Data.Length);
        QuicStreamDataBlockedFrame streamBlocked = Sent<QuicStreamDataBlockedFrame>(server).Single();
        Diagnostics.Act("bytes sent at the stream limit", afterStreamLimit);
        Diagnostics.Act("blocked frame", streamBlocked);
        Diagnostics.Assert("bytes sent at the stream limit", 10, afterStreamLimit);
        Assert.AreEqual(10, afterStreamLimit);
        Diagnostics.Assert("stream blocked frame", new QuicStreamDataBlockedFrame(0, 10), streamBlocked);
        Assert.AreEqual(new QuicStreamDataBlockedFrame(0, 10), streamBlocked);

        Deliver(client, server, new QuicMaxStreamDataFrame(0, 100));
        int afterConnectionLimit = Sent<QuicStreamFrame>(server).Sum(frame => frame.Data.Length);
        QuicDataBlockedFrame dataBlocked = Sent<QuicDataBlockedFrame>(server).Single();
        Diagnostics.Act("bytes sent at the connection limit", afterConnectionLimit);
        Diagnostics.Act("blocked frame", dataBlocked);
        Diagnostics.Assert("bytes sent at the connection limit", 15, afterConnectionLimit);
        Assert.AreEqual(15, afterConnectionLimit);
        Diagnostics.Assert("data blocked frame", new QuicDataBlockedFrame(15), dataBlocked);
        Assert.AreEqual(new QuicDataBlockedFrame(15), dataBlocked);

        Deliver(client, server, new QuicMaxDataFrame(100));
        List<QuicStreamFrame> frames = Sent<QuicStreamFrame>(server);
        byte[] sent = frames.SelectMany(frame => frame.Data.ToArray()).ToArray();
        ulong[] offsets = frames.Select(frame => frame.Offset).ToArray();
        Diagnostics.Bytes("payload sent", sent);
        Diagnostics.Act("frame offsets", string.Join(",", offsets));
        Diagnostics.Diff("payload sent", Bytes(30), sent);
        CollectionAssert.AreEqual(Bytes(30), sent);
        Diagnostics.Assert("frame offsets", "0,10,15", string.Join(",", offsets));
        CollectionAssert.AreEqual(new ulong[] { 0, 10, 15 }, offsets);
        Diagnostics.Assert("last frame fin", true, frames[^1].IsFin);
        Assert.IsTrue(frames[^1].IsFin);
        Diagnostics.Assert("connection send available", 70UL, client.Streams.ConnectionSendAvailable);
        Assert.AreEqual(70UL, client.Streams.ConnectionSendAvailable);
    }

    [TestMethod]
    public void TakeDatagramsToSend_MoreThanOnePacketOfData_SendsEach1RttPacketInItsOwnDatagram()
    {
        Diagnostics.Arrange("payload length", 3000);
        using QuicTestServer server = new() { ConfigureTransportParameters = GenerousServerLimits };
        using QuicClientConnectionState client = ConnectClient(server);
        QuicStream stream = client.Streams.OpenBidirectional()!;

        IReadOnlyList<byte[]> datagrams;
        using (Diagnostics.Phase("data transfer"))
        {
            stream.Write(Bytes(3000), endStream: true);
            datagrams = client.TakeDatagramsToSend();
            QuicClientConnectionStateTests.Exchange(client, server, datagrams);
        }

        byte[] sent = Sent<QuicStreamFrame>(server).SelectMany(frame => frame.Data.ToArray()).ToArray();
        Diagnostics.Act("datagrams", datagrams.Count);
        Diagnostics.Act("payload received by server length", sent.Length);
        Diagnostics.Assert("datagrams", 3, datagrams.Count);
        Assert.HasCount(3, datagrams);
        Diagnostics.Diff("payload received by server", Bytes(3000), sent);
        CollectionAssert.AreEqual(Bytes(3000), sent);
    }

    [TestMethod]
    public void TakeDatagramsToSend_LessDataThanTheWindow_MarksThePacketsApplicationLimitedAndTheirAcknowledgementLeavesTheWindow()
    {
        Diagnostics.Arrange("payload length", 3000);
        Diagnostics.Arrange("initial congestion window", 12000);
        using QuicTestServer server = new() { ConfigureTransportParameters = GenerousServerLimits };
        using QuicClientConnectionState client = ConnectClient(server);
        client.Streams.OpenBidirectional()!.Write(Bytes(3000), endStream: true);

        IReadOnlyList<byte[]> datagrams = client.TakeDatagramsToSend();

        // RFC 9002 section 7.8: 3000 bytes leave most of the 12000-byte window unused.
        bool allLimited = SentStreamPackets(client).All(packet => packet.IsApplicationLimited);
        Diagnostics.Act("datagrams", datagrams.Count);
        Diagnostics.Assert("all stream packets application limited", true, allLimited);
        Assert.IsTrue(allLimited);
        using (Diagnostics.Phase("data transfer"))
        {
            QuicClientConnectionStateTests.Exchange(client, server, datagrams);
        }

        Diagnostics.Act("congestion window", client.Recovery.Congestion.CongestionWindow);
        Diagnostics.Assert("congestion window", 12000, client.Recovery.Congestion.CongestionWindow);
        Assert.AreEqual(12000, client.Recovery.Congestion.CongestionWindow);
    }

    [TestMethod]
    public void TakeDatagramsToSend_BlockedByFlowControl_MarksThePacketsApplicationLimited()
    {
        Diagnostics.Arrange("server InitialMaxData", 15);
        Diagnostics.Arrange("payload length", 30000);
        using QuicTestServer server = new()
        {
            ConfigureTransportParameters = parameters => GenerousServerLimits(parameters) with { InitialMaxData = 15 },
        };
        using QuicClientConnectionState client = ConnectClient(server);
        client.Streams.OpenBidirectional()!.Write(Bytes(30000), endStream: true);

        IReadOnlyList<byte[]> datagrams = client.TakeDatagramsToSend();

        bool allLimited = SentStreamPackets(client).All(packet => packet.IsApplicationLimited);
        Diagnostics.Act("datagrams", datagrams.Count);
        Diagnostics.Assert("all stream packets application limited", true, allLimited);
        Assert.IsTrue(allLimited);
    }

    [TestMethod]
    public void TakeDatagramsToSend_MoreDataThanTheWindow_FillsItWithPacketsThatAreNotApplicationLimitedAndGrowIt()
    {
        Diagnostics.Arrange("payload length", 30000);
        Diagnostics.Arrange("initial congestion window", 12000);
        using QuicTestServer server = new() { ConfigureTransportParameters = GenerousServerLimits };
        using QuicClientConnectionState client = ConnectClient(server);
        client.Streams.OpenBidirectional()!.Write(Bytes(30000), endStream: true);

        IReadOnlyList<byte[]> datagrams = client.TakeDatagramsToSend();

        List<QuicSentPacket> sent = SentStreamPackets(client);
        bool noneLimited = sent.All(packet => !packet.IsApplicationLimited);
        Diagnostics.Act("datagrams", datagrams.Count);
        Diagnostics.Act("bytes in flight", client.Recovery.Congestion.BytesInFlight);
        Diagnostics.Assert("no stream packet application limited", true, noneLimited);
        Assert.IsTrue(noneLimited);
        Diagnostics.Assert("bytes in flight at least", ">= 12000", client.Recovery.Congestion.BytesInFlight);
        Assert.IsGreaterThanOrEqualTo(12000, client.Recovery.Congestion.BytesInFlight);
        using (Diagnostics.Phase("data transfer"))
        {
            QuicClientConnectionStateTests.Exchange(client, server, datagrams);
        }

        Diagnostics.Act("congestion window", client.Recovery.Congestion.CongestionWindow);
        Diagnostics.Assert("congestion window above", "> 12000", client.Recovery.Congestion.CongestionWindow);
        Assert.IsGreaterThan(12000, client.Recovery.Congestion.CongestionWindow);
    }

    [TestMethod]
    public void Read_HalfTheWindowConsumed_RaisesMaxStreamDataAndMaxData()
    {
        Diagnostics.Arrange("bytes delivered on stream 3", 60);
        using QuicTestServer server = new();
        using QuicClientConnectionState client = ConnectClient(server);

        Deliver(client, server, new QuicStreamFrame(3, 0, Bytes(60), false));
        QuicStream stream = client.Streams.AcceptUnidirectional()!;
        int earlyCount = Sent<QuicMaxStreamDataFrame>(server).Count;
        Diagnostics.Act("MAX_STREAM_DATA frames before reading", earlyCount);
        Diagnostics.Assert("MAX_STREAM_DATA frames before reading", 0, earlyCount);
        Assert.IsEmpty(Sent<QuicMaxStreamDataFrame>(server));

        ReadAll(stream);
        Flush(client, server);

        QuicMaxStreamDataFrame maxStreamData = Sent<QuicMaxStreamDataFrame>(server).Single();
        QuicMaxDataFrame maxData = Sent<QuicMaxDataFrame>(server).Single();
        Diagnostics.Act("MAX_STREAM_DATA", maxStreamData);
        Diagnostics.Act("MAX_DATA", maxData);
        Diagnostics.Assert("MAX_STREAM_DATA", new QuicMaxStreamDataFrame(3, 160), maxStreamData);
        Assert.AreEqual(new QuicMaxStreamDataFrame(3, 160), maxStreamData);
        Diagnostics.Assert("MAX_DATA", new QuicMaxDataFrame(160), maxData);
        Assert.AreEqual(new QuicMaxDataFrame(160), maxData);
        Diagnostics.Assert("connection receive limit", 160UL, client.Streams.ConnectionReceiveLimit);
        Assert.AreEqual(160UL, client.Streams.ConnectionReceiveLimit);
    }

    [TestMethod]
    public void Receive_ResetStream_DropsUnreadBytesAndReportsTheCode()
    {
        Diagnostics.Arrange("stream bytes before reset", 3);
        Diagnostics.Arrange("reset error code", 0x10c);
        using QuicTestServer server = new();
        using QuicClientConnectionState client = ConnectClient(server);

        Deliver(client, server, new QuicStreamFrame(3, 0, Bytes(3), false), new QuicResetStreamFrame(3, 0x10c, 50));
        QuicStream stream = client.Streams.AcceptUnidirectional()!;

        Diagnostics.Act("peer reset error code", stream.PeerResetErrorCode);
        Diagnostics.Act("readable length", stream.ReadableLength);
        Diagnostics.Assert("peer reset error code", 0x10cUL, stream.PeerResetErrorCode);
        Assert.AreEqual(0x10cUL, stream.PeerResetErrorCode);
        Diagnostics.Assert("readable length", 0, stream.ReadableLength);
        Assert.AreEqual(0, stream.ReadableLength);
        Diagnostics.Assert("read complete", true, stream.IsReadComplete);
        Assert.IsTrue(stream.IsReadComplete);
        Diagnostics.Assert("client failure", "none", client.Failure?.ToString() ?? "none");
        Assert.IsNull(client.Failure);
    }

    [TestMethod]
    public void Receive_StopSending_AnswersWithResetStreamAtTheSentOffset()
    {
        Diagnostics.Arrange("bytes written", 7);
        Diagnostics.Arrange("STOP_SENDING error code", 0x10b);
        using QuicTestServer server = new() { ConfigureTransportParameters = GenerousServerLimits };
        using QuicClientConnectionState client = ConnectClient(server);
        QuicStream stream = client.Streams.OpenBidirectional()!;
        stream.Write(Bytes(7), endStream: false);
        Flush(client, server);

        Deliver(client, server, new QuicStopSendingFrame(0, 0x10b));

        QuicResetStreamFrame reset = Sent<QuicResetStreamFrame>(server).Single();
        Diagnostics.Act("RESET_STREAM", reset);
        Diagnostics.Act("peer stop-sending code", stream.PeerStopSendingErrorCode);
        Diagnostics.Assert("RESET_STREAM", new QuicResetStreamFrame(0, 0x10b, 7), reset);
        Assert.AreEqual(new QuicResetStreamFrame(0, 0x10b, 7), reset);
        Diagnostics.Assert("peer stop-sending code", 0x10bUL, stream.PeerStopSendingErrorCode);
        Assert.AreEqual(0x10bUL, stream.PeerStopSendingErrorCode);
        Diagnostics.Assert("write ended", true, stream.IsWriteEnded);
        Assert.IsTrue(stream.IsWriteEnded);
    }

    [TestMethod]
    public void OpenUnidirectional_PastTheServersMaxStreams_RefusesSendsStreamsBlockedAndOpensAfterMaxStreams()
    {
        Diagnostics.Arrange("server InitialMaxStreamsUni", 1);
        Diagnostics.Arrange("MAX_STREAMS (uni) value", 2);
        using QuicTestServer server = new() { ConfigureTransportParameters = parameters => GenerousServerLimits(parameters) with { InitialMaxStreamsUni = 1 } };
        using QuicClientConnectionState client = ConnectClient(server);

        ulong firstId = client.Streams.OpenUnidirectional()!.Id;
        Diagnostics.Act("first stream id", firstId);
        Diagnostics.Assert("first stream id", 2UL, firstId);
        Assert.AreEqual(2UL, firstId);
        QuicStream? second = client.Streams.OpenUnidirectional();
        QuicStream? third = client.Streams.OpenUnidirectional();
        Diagnostics.Act("second open returned a stream", second is not null);
        Diagnostics.Act("third open returned a stream", third is not null);
        Diagnostics.Assert("second open returned a stream", false, second is not null);
        Assert.IsNull(second);
        Diagnostics.Assert("third open returned a stream", false, third is not null);
        Assert.IsNull(third);
        Flush(client, server);
        QuicStreamsBlockedFrame blocked = Sent<QuicStreamsBlockedFrame>(server).Single();
        Diagnostics.Act("STREAMS_BLOCKED", blocked);
        Diagnostics.Assert("STREAMS_BLOCKED", new QuicStreamsBlockedFrame(true, 1), blocked);
        Assert.AreEqual(new QuicStreamsBlockedFrame(true, 1), blocked);

        Deliver(client, server, new QuicMaxStreamsFrame(true, 2));

        ulong reopenedId = client.Streams.OpenUnidirectional()!.Id;
        Diagnostics.Act("stream id after MAX_STREAMS", reopenedId);
        Diagnostics.Assert("stream id after MAX_STREAMS", 6UL, reopenedId);
        Assert.AreEqual(6UL, reopenedId);
    }

    [TestMethod]
    public void Receive_ServerStreamPastTheClientsMaxStreams_ClosesWithStreamLimitError()
    {
        Diagnostics.Arrange("server stream id", 11);
        using QuicTestServer server = new();
        using QuicClientConnectionState client = ConnectClient(server);

        Deliver(client, server, new QuicStreamFrame(11, 0, Bytes(1), false));

        ulong errorCode = Sent<QuicConnectionCloseFrame>(server).Single().ErrorCode;
        Diagnostics.Act("CONNECTION_CLOSE error code", errorCode);
        Diagnostics.Assert("CONNECTION_CLOSE error code", (ulong)QuicTransportErrorCode.StreamLimitError, errorCode);
        Assert.AreEqual((ulong)QuicTransportErrorCode.StreamLimitError, errorCode);
        Diagnostics.Assert("client failed", true, client.Failure is not null);
        Assert.IsNotNull(client.Failure);
    }

    [TestMethod]
    public void Receive_MoreThanTheStreamLimit_ClosesWithFlowControlErrorAndExit56()
    {
        Diagnostics.Arrange("bytes delivered on stream 3", 101);
        using QuicTestServer server = new();
        using QuicClientConnectionState client = ConnectClient(server);

        Deliver(client, server, new QuicStreamFrame(3, 0, Bytes(101), false));

        ulong errorCode = Sent<QuicConnectionCloseFrame>(server).Single().ErrorCode;
        Diagnostics.Act("CONNECTION_CLOSE error code", errorCode);
        Diagnostics.Assert("CONNECTION_CLOSE error code", (ulong)QuicTransportErrorCode.FlowControlError, errorCode);
        Assert.AreEqual((ulong)QuicTransportErrorCode.FlowControlError, errorCode);
        Diagnostics.Act("failure exit code", client.Failure!.ExitCode);
        Diagnostics.Assert("failure exit code", Protocol.Abstractions.CurlExitCode.RecvError, client.Failure!.ExitCode);
        Assert.AreEqual(Protocol.Abstractions.CurlExitCode.RecvError, client.Failure!.ExitCode);
        IReadOnlyList<byte[]> pending = client.TakeDatagramsToSend();
        Diagnostics.Assert("datagrams to send", 0, pending.Count);
        Assert.IsEmpty(pending);
    }

    [TestMethod]
    public void Receive_MoreThanTheConnectionLimitAcrossStreams_ClosesWithFlowControlError()
    {
        Diagnostics.Arrange("bytes delivered on streams 3 and 7", "60 + 41");
        using QuicTestServer server = new();
        using QuicClientConnectionState client = ConnectClient(server);

        Deliver(client, server, new QuicStreamFrame(3, 0, Bytes(60), false), new QuicStreamFrame(7, 0, Bytes(41), false));

        ulong errorCode = Sent<QuicConnectionCloseFrame>(server).Single().ErrorCode;
        Diagnostics.Act("CONNECTION_CLOSE error code", errorCode);
        Diagnostics.Assert("CONNECTION_CLOSE error code", (ulong)QuicTransportErrorCode.FlowControlError, errorCode);
        Assert.AreEqual((ulong)QuicTransportErrorCode.FlowControlError, errorCode);
    }

    [TestMethod]
    public void OnLossDetectionTimeout_LostStreamData_SendsItAgainButNotOnceTheStreamIsReset()
    {
        Diagnostics.Arrange("streams", "0 kept (4 bytes), 4 reset with code 9 (5 bytes)");
        ManualTimerTimeProvider clock = new();
        using QuicTestServer server = new() { ConfigureTransportParameters = GenerousServerLimits };
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client(QuicClientConnectionStateTest.CurlSettings with { TransportParameters = SmallClientLimits }, clock: clock);
        using (Diagnostics.Phase("handshake"))
        {
            QuicClientConnectionStateTests.Run(client, server);
        }

        QuicStream kept = client.Streams.OpenBidirectional()!;
        QuicStream reset = client.Streams.OpenBidirectional()!;
        kept.Write(Bytes(4), endStream: true);
        reset.Write(Bytes(5), endStream: true);
        IReadOnlyList<byte[]> firstFlight = client.TakeDatagramsToSend();
        Diagnostics.Act("datagrams in first flight", firstFlight.Count);
        Diagnostics.Assert("first flight has datagrams", ">= 1", firstFlight.Count);
        Assert.IsNotEmpty(firstFlight);
        reset.Abort(9);

        using (Diagnostics.Phase("data transfer"))
        {
            clock.Advance((long)client.TimeUntilLossDetectionTimeout.TotalMilliseconds);
            Flush(client, server);
            QuicClientConnectionStateTests.Exchange(client, server, client.OnLossDetectionTimeout());
        }

        ulong resentStreamId = Sent<QuicStreamFrame>(server).Single().StreamId;
        QuicResetStreamFrame resetFrame = Sent<QuicResetStreamFrame>(server).Single();
        Diagnostics.Act("stream data resent for stream", resentStreamId);
        Diagnostics.Act("RESET_STREAM", resetFrame);
        Diagnostics.Assert("stream data resent for stream", 0UL, resentStreamId);
        Assert.AreEqual(0UL, resentStreamId);
        Diagnostics.Assert("RESET_STREAM", new QuicResetStreamFrame(4, 9, 5), resetFrame);
        Assert.AreEqual(new QuicResetStreamFrame(4, 9, 5), resetFrame);
    }

    [TestMethod]
    public void CloseWithApplicationError_SendsAnApplicationConnectionClose()
    {
        Diagnostics.Arrange("application error code", 0x100);
        using QuicTestServer server = new();
        using QuicClientConnectionState client = ConnectClient(server);

        QuicClientConnectionStateTests.Exchange(client, server, client.CloseWithApplicationError(0x100));

        QuicConnectionCloseFrame close = Sent<QuicConnectionCloseFrame>(server).Single();
        Diagnostics.Act("CONNECTION_CLOSE (error code, frame type)", (close.ErrorCode, close.FrameType));
        Diagnostics.Assert("CONNECTION_CLOSE (error code, frame type)", (0x100UL, (ulong?)null), (close.ErrorCode, close.FrameType));
        Assert.AreEqual((0x100UL, (ulong?)null), (close.ErrorCode, close.FrameType));
    }

    // The 1-RTT packets the client has sent with stream data and the server has not acknowledged yet.
    private static List<QuicSentPacket> SentStreamPackets(QuicClientConnectionState client)
    {
        List<QuicSentPacket> sent = [.. client.Recovery.GetUnacknowledgedPackets(QuicPacketNumberSpaceId.ApplicationData).Where(packet => packet.Frames.OfType<QuicStreamFrame>().Any())];
        Assert.IsNotEmpty(sent);
        return sent;
    }

    private QuicClientConnectionState ConnectClient(QuicTestServer server)
    {
        using (Diagnostics.Phase("handshake"))
        {
            return Connect(server);
        }
    }
}
