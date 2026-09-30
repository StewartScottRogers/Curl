using System.Text;
using static Curl.Quic.QuicStreamTest;

namespace Curl.Quic;

/// <summary>Streams and flow control carried by a client whose handshake with the in-memory server is complete (RFC 9000 sections 2 to 4).</summary>
[TestClass]
public sealed class QuicClientStreamsTests
{
    [TestMethod]
    public void Receive_ReorderedAndDuplicatedStreamFrames_ReassemblesTheServersStreamInOrder()
    {
        using QuicTestServer server = new();
        using QuicClientConnectionState client = Connect(server);

        Deliver(client, server, new QuicStreamFrame(3, 5, "world"u8.ToArray(), false));
        Deliver(client, server, new QuicStreamFrame(3, 0, "hello"u8.ToArray(), false), new QuicStreamFrame(3, 0, "hel"u8.ToArray(), false));
        Deliver(client, server, new QuicStreamFrame(3, 3, "lowo"u8.ToArray(), false), new QuicStreamFrame(3, 10, "!"u8.ToArray(), true));
        QuicStream stream = client.Streams.AcceptUnidirectional()!;

        Assert.AreEqual(3UL, stream.Id);
        Assert.AreEqual("helloworld!", Encoding.ASCII.GetString(ReadAll(stream)));
        Assert.IsTrue(stream.IsReadComplete);
        Assert.IsNull(client.Streams.AcceptUnidirectional());
        Assert.IsNull(client.Failure);
    }

    [TestMethod]
    public void TakeDatagramsToSend_PastTheServersLimits_StopsReportsBlockedAndResumesAfterMaxStreamDataAndMaxData()
    {
        using QuicTestServer server = new()
        {
            ConfigureTransportParameters = parameters => GenerousServerLimits(parameters) with { InitialMaxStreamDataBidiRemote = 10, InitialMaxData = 15 },
        };
        using QuicClientConnectionState client = Connect(server);
        QuicStream stream = client.Streams.OpenBidirectional()!;

        stream.Write(Bytes(30), endStream: true);
        Flush(client, server);
        Assert.AreEqual(10, Sent<QuicStreamFrame>(server).Sum(frame => frame.Data.Length));
        Assert.AreEqual(new QuicStreamDataBlockedFrame(0, 10), Sent<QuicStreamDataBlockedFrame>(server).Single());

        Deliver(client, server, new QuicMaxStreamDataFrame(0, 100));
        Assert.AreEqual(15, Sent<QuicStreamFrame>(server).Sum(frame => frame.Data.Length));
        Assert.AreEqual(new QuicDataBlockedFrame(15), Sent<QuicDataBlockedFrame>(server).Single());

        Deliver(client, server, new QuicMaxDataFrame(100));
        List<QuicStreamFrame> frames = Sent<QuicStreamFrame>(server);
        CollectionAssert.AreEqual(Bytes(30), frames.SelectMany(frame => frame.Data.ToArray()).ToArray());
        CollectionAssert.AreEqual(new ulong[] { 0, 10, 15 }, frames.Select(frame => frame.Offset).ToArray());
        Assert.IsTrue(frames[^1].IsFin);
        Assert.AreEqual(70UL, client.Streams.ConnectionSendAvailable);
    }

    [TestMethod]
    public void TakeDatagramsToSend_MoreThanOnePacketOfData_SendsEach1RttPacketInItsOwnDatagram()
    {
        using QuicTestServer server = new() { ConfigureTransportParameters = GenerousServerLimits };
        using QuicClientConnectionState client = Connect(server);
        QuicStream stream = client.Streams.OpenBidirectional()!;

        stream.Write(Bytes(3000), endStream: true);
        IReadOnlyList<byte[]> datagrams = client.TakeDatagramsToSend();
        QuicClientConnectionStateTests.Exchange(client, server, datagrams);

        Assert.HasCount(3, datagrams);
        CollectionAssert.AreEqual(Bytes(3000), Sent<QuicStreamFrame>(server).SelectMany(frame => frame.Data.ToArray()).ToArray());
    }

    [TestMethod]
    public void TakeDatagramsToSend_LessDataThanTheWindow_MarksThePacketsApplicationLimitedAndTheirAcknowledgementLeavesTheWindow()
    {
        using QuicTestServer server = new() { ConfigureTransportParameters = GenerousServerLimits };
        using QuicClientConnectionState client = Connect(server);
        client.Streams.OpenBidirectional()!.Write(Bytes(3000), endStream: true);

        IReadOnlyList<byte[]> datagrams = client.TakeDatagramsToSend();

        // RFC 9002 section 7.8: 3000 bytes leave most of the 12000-byte window unused.
        Assert.IsTrue(SentStreamPackets(client).All(packet => packet.IsApplicationLimited));
        QuicClientConnectionStateTests.Exchange(client, server, datagrams);
        Assert.AreEqual(12000, client.Recovery.Congestion.CongestionWindow);
    }

    [TestMethod]
    public void TakeDatagramsToSend_BlockedByFlowControl_MarksThePacketsApplicationLimited()
    {
        using QuicTestServer server = new()
        {
            ConfigureTransportParameters = parameters => GenerousServerLimits(parameters) with { InitialMaxData = 15 },
        };
        using QuicClientConnectionState client = Connect(server);
        client.Streams.OpenBidirectional()!.Write(Bytes(30000), endStream: true);

        client.TakeDatagramsToSend();

        Assert.IsTrue(SentStreamPackets(client).All(packet => packet.IsApplicationLimited));
    }

    [TestMethod]
    public void TakeDatagramsToSend_MoreDataThanTheWindow_FillsItWithPacketsThatAreNotApplicationLimitedAndGrowIt()
    {
        using QuicTestServer server = new() { ConfigureTransportParameters = GenerousServerLimits };
        using QuicClientConnectionState client = Connect(server);
        client.Streams.OpenBidirectional()!.Write(Bytes(30000), endStream: true);

        IReadOnlyList<byte[]> datagrams = client.TakeDatagramsToSend();

        List<QuicSentPacket> sent = SentStreamPackets(client);
        Assert.IsTrue(sent.All(packet => !packet.IsApplicationLimited));
        Assert.IsGreaterThanOrEqualTo(12000, client.Recovery.Congestion.BytesInFlight);
        QuicClientConnectionStateTests.Exchange(client, server, datagrams);
        Assert.IsGreaterThan(12000, client.Recovery.Congestion.CongestionWindow);
    }

    [TestMethod]
    public void Read_HalfTheWindowConsumed_RaisesMaxStreamDataAndMaxData()
    {
        using QuicTestServer server = new();
        using QuicClientConnectionState client = Connect(server);

        Deliver(client, server, new QuicStreamFrame(3, 0, Bytes(60), false));
        QuicStream stream = client.Streams.AcceptUnidirectional()!;
        Assert.IsEmpty(Sent<QuicMaxStreamDataFrame>(server));

        ReadAll(stream);
        Flush(client, server);

        Assert.AreEqual(new QuicMaxStreamDataFrame(3, 160), Sent<QuicMaxStreamDataFrame>(server).Single());
        Assert.AreEqual(new QuicMaxDataFrame(160), Sent<QuicMaxDataFrame>(server).Single());
        Assert.AreEqual(160UL, client.Streams.ConnectionReceiveLimit);
    }

    [TestMethod]
    public void Receive_ResetStream_DropsUnreadBytesAndReportsTheCode()
    {
        using QuicTestServer server = new();
        using QuicClientConnectionState client = Connect(server);

        Deliver(client, server, new QuicStreamFrame(3, 0, Bytes(3), false), new QuicResetStreamFrame(3, 0x10c, 50));
        QuicStream stream = client.Streams.AcceptUnidirectional()!;

        Assert.AreEqual(0x10cUL, stream.PeerResetErrorCode);
        Assert.AreEqual(0, stream.ReadableLength);
        Assert.IsTrue(stream.IsReadComplete);
        Assert.IsNull(client.Failure);
    }

    [TestMethod]
    public void Receive_StopSending_AnswersWithResetStreamAtTheSentOffset()
    {
        using QuicTestServer server = new() { ConfigureTransportParameters = GenerousServerLimits };
        using QuicClientConnectionState client = Connect(server);
        QuicStream stream = client.Streams.OpenBidirectional()!;
        stream.Write(Bytes(7), endStream: false);
        Flush(client, server);

        Deliver(client, server, new QuicStopSendingFrame(0, 0x10b));

        Assert.AreEqual(new QuicResetStreamFrame(0, 0x10b, 7), Sent<QuicResetStreamFrame>(server).Single());
        Assert.AreEqual(0x10bUL, stream.PeerStopSendingErrorCode);
        Assert.IsTrue(stream.IsWriteEnded);
    }

    [TestMethod]
    public void OpenUnidirectional_PastTheServersMaxStreams_RefusesSendsStreamsBlockedAndOpensAfterMaxStreams()
    {
        using QuicTestServer server = new() { ConfigureTransportParameters = parameters => GenerousServerLimits(parameters) with { InitialMaxStreamsUni = 1 } };
        using QuicClientConnectionState client = Connect(server);

        Assert.AreEqual(2UL, client.Streams.OpenUnidirectional()!.Id);
        Assert.IsNull(client.Streams.OpenUnidirectional());
        Assert.IsNull(client.Streams.OpenUnidirectional());
        Flush(client, server);
        Assert.AreEqual(new QuicStreamsBlockedFrame(true, 1), Sent<QuicStreamsBlockedFrame>(server).Single());

        Deliver(client, server, new QuicMaxStreamsFrame(true, 2));

        Assert.AreEqual(6UL, client.Streams.OpenUnidirectional()!.Id);
    }

    [TestMethod]
    public void Receive_ServerStreamPastTheClientsMaxStreams_ClosesWithStreamLimitError()
    {
        using QuicTestServer server = new();
        using QuicClientConnectionState client = Connect(server);

        Deliver(client, server, new QuicStreamFrame(11, 0, Bytes(1), false));

        Assert.AreEqual((ulong)QuicTransportErrorCode.StreamLimitError, Sent<QuicConnectionCloseFrame>(server).Single().ErrorCode);
        Assert.IsNotNull(client.Failure);
    }

    [TestMethod]
    public void Receive_MoreThanTheStreamLimit_ClosesWithFlowControlErrorAndExit56()
    {
        using QuicTestServer server = new();
        using QuicClientConnectionState client = Connect(server);

        Deliver(client, server, new QuicStreamFrame(3, 0, Bytes(101), false));

        Assert.AreEqual((ulong)QuicTransportErrorCode.FlowControlError, Sent<QuicConnectionCloseFrame>(server).Single().ErrorCode);
        Assert.AreEqual(Protocol.Abstractions.CurlExitCode.RecvError, client.Failure!.ExitCode);
        Assert.IsEmpty(client.TakeDatagramsToSend());
    }

    [TestMethod]
    public void Receive_MoreThanTheConnectionLimitAcrossStreams_ClosesWithFlowControlError()
    {
        using QuicTestServer server = new();
        using QuicClientConnectionState client = Connect(server);

        Deliver(client, server, new QuicStreamFrame(3, 0, Bytes(60), false), new QuicStreamFrame(7, 0, Bytes(41), false));

        Assert.AreEqual((ulong)QuicTransportErrorCode.FlowControlError, Sent<QuicConnectionCloseFrame>(server).Single().ErrorCode);
    }

    [TestMethod]
    public void OnLossDetectionTimeout_LostStreamData_SendsItAgainButNotOnceTheStreamIsReset()
    {
        ManualTimerTimeProvider clock = new();
        using QuicTestServer server = new() { ConfigureTransportParameters = GenerousServerLimits };
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client(QuicClientConnectionStateTest.CurlSettings with { TransportParameters = SmallClientLimits }, clock: clock);
        QuicClientConnectionStateTests.Run(client, server);
        QuicStream kept = client.Streams.OpenBidirectional()!;
        QuicStream reset = client.Streams.OpenBidirectional()!;
        kept.Write(Bytes(4), endStream: true);
        reset.Write(Bytes(5), endStream: true);
        Assert.IsNotEmpty(client.TakeDatagramsToSend());
        reset.Abort(9);

        clock.Advance((long)client.TimeUntilLossDetectionTimeout.TotalMilliseconds);
        Flush(client, server);
        QuicClientConnectionStateTests.Exchange(client, server, client.OnLossDetectionTimeout());

        Assert.AreEqual(0UL, Sent<QuicStreamFrame>(server).Single().StreamId);
        Assert.AreEqual(new QuicResetStreamFrame(4, 9, 5), Sent<QuicResetStreamFrame>(server).Single());
    }

    [TestMethod]
    public void CloseWithApplicationError_SendsAnApplicationConnectionClose()
    {
        using QuicTestServer server = new();
        using QuicClientConnectionState client = Connect(server);

        QuicClientConnectionStateTests.Exchange(client, server, client.CloseWithApplicationError(0x100));

        QuicConnectionCloseFrame close = Sent<QuicConnectionCloseFrame>(server).Single();
        Assert.AreEqual((0x100UL, (ulong?)null), (close.ErrorCode, close.FrameType));
    }

    // The 1-RTT packets the client has sent with stream data and the server has not acknowledged yet.
    private static List<QuicSentPacket> SentStreamPackets(QuicClientConnectionState client)
    {
        List<QuicSentPacket> sent = [.. client.Recovery.GetUnacknowledgedPackets(QuicPacketNumberSpaceId.ApplicationData).Where(packet => packet.Frames.OfType<QuicStreamFrame>().Any())];
        Assert.IsNotEmpty(sent);
        return sent;
    }
}
