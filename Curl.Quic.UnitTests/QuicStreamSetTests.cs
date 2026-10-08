using Curl.Testing;
using static Curl.Quic.QuicStreamTest;
using static Curl.Quic.QuicTest;

namespace Curl.Quic;

/// <summary>The stream set and its streams on their own, frame by frame (RFC 9000 sections 2 to 4 and 19.4 to 19.14).</summary>
[TestClass]
public sealed class QuicStreamSetTests
{
    private static readonly QuicTransportParameters ServerLimits = GenerousServerLimits(new QuicTransportParameters()) with { InitialMaxStreamDataBidiRemote = 10, InitialMaxStreamDataBidiLocal = 20, InitialMaxStreamDataUni = 30 };

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Constructor_NullParameters_Throws()
    {
        Diagnostics.Arrange("client parameters", "null");

        ArgumentNullException constructed = Assert.ThrowsExactly<ArgumentNullException>(() => new QuicStreamSet(null!));
        ArgumentNullException peer = Assert.ThrowsExactly<ArgumentNullException>(() => new QuicStreamSet(SmallClientLimits).SetPeerTransportParameters(null!));

        Diagnostics.Act("constructor exception", constructed.GetType().Name + " " + constructed.ParamName);
        Diagnostics.Act("SetPeerTransportParameters exception", peer.GetType().Name + " " + peer.ParamName);
        Diagnostics.Assert("constructor exception type", nameof(ArgumentNullException), constructed.GetType().Name);
        Diagnostics.Assert("SetPeerTransportParameters exception type", nameof(ArgumentNullException), peer.GetType().Name);
    }

    [TestMethod]
    public void Open_BeforeTheServersParameters_AllowsNoStream()
    {
        Diagnostics.Arrange("server parameters set", false);
        QuicStreamSet set = new(SmallClientLimits);

        QuicStream? opened = set.OpenBidirectional();
        bool hasFrames = set.HasFramesToSend;
        QuicFrame blocked = Take(set).Single();
        bool hasFramesAfter = set.HasFramesToSend;

        Diagnostics.Act("opened stream", opened is null ? "none" : "a stream");
        Diagnostics.Act("frame taken", blocked);
        Diagnostics.Assert("opened stream is null", true, opened is null);
        Diagnostics.Assert("has frames before take", true, hasFrames);
        Diagnostics.Assert("blocked frame", new QuicStreamsBlockedFrame(false, 0), blocked);
        Diagnostics.Assert("has frames after take", false, hasFramesAfter);
        Assert.IsNull(opened);
        Assert.IsTrue(hasFrames);
        Assert.AreEqual(new QuicStreamsBlockedFrame(false, 0), blocked);
        Assert.IsFalse(hasFramesAfter);
    }

    [TestMethod]
    public void Open_TakesTheServersLimitsForEachKindOfStream()
    {
        Diagnostics.Arrange("server stream-data limits (bidi remote, uni)", "10, 30");
        Diagnostics.Arrange("payload length written to each stream", 50);
        QuicStreamSet set = Set();

        QuicStream bidirectional = set.OpenBidirectional()!;
        QuicStream unidirectional = set.OpenUnidirectional()!;
        bidirectional.Write(Bytes(50), endStream: false);
        unidirectional.Write(Bytes(50), endStream: false);

        Diagnostics.Act("bidirectional (id, clientInitiated, canRead, canWrite, uni)", (bidirectional.Id, bidirectional.IsClientInitiated, bidirectional.CanRead, bidirectional.CanWrite, bidirectional.IsUnidirectional));
        Diagnostics.Act("unidirectional (id, canRead, canWrite, uni)", (unidirectional.Id, unidirectional.CanRead, unidirectional.CanWrite, unidirectional.IsUnidirectional));
        Diagnostics.Assert("bidirectional properties", (0UL, true, true, true, false), (bidirectional.Id, bidirectional.IsClientInitiated, bidirectional.CanRead, bidirectional.CanWrite, bidirectional.IsUnidirectional));
        Diagnostics.Assert("unidirectional properties", (2UL, false, true, true), (unidirectional.Id, unidirectional.CanRead, unidirectional.CanWrite, unidirectional.IsUnidirectional));
        Assert.AreEqual((0UL, true, true, true, false), (bidirectional.Id, bidirectional.IsClientInitiated, bidirectional.CanRead, bidirectional.CanWrite, bidirectional.IsUnidirectional));
        Assert.AreEqual((2UL, false, true, true), (unidirectional.Id, unidirectional.CanRead, unidirectional.CanWrite, unidirectional.IsUnidirectional));
        List<QuicFrame> frames = Take(set);
        Diagnostics.Act("frames taken", frames.Count);
        Diagnostics.Assert("stream 0 data length", 10, frames.OfType<QuicStreamFrame>().Single(frame => frame.StreamId == 0).Data.Length);
        Diagnostics.Assert("stream 2 data length", 30, frames.OfType<QuicStreamFrame>().Single(frame => frame.StreamId == 2).Data.Length);
        Assert.AreEqual(10, frames.OfType<QuicStreamFrame>().Single(frame => frame.StreamId == 0).Data.Length);
        Assert.AreEqual(30, frames.OfType<QuicStreamFrame>().Single(frame => frame.StreamId == 2).Data.Length);
        CollectionAssert.AreEqual(new QuicFrame[] { new QuicStreamDataBlockedFrame(0, 10), new QuicStreamDataBlockedFrame(2, 30) }, frames.OfType<QuicStreamDataBlockedFrame>().ToList());
        Diagnostics.Assert("has frames after take", false, set.HasFramesToSend);
        Assert.IsFalse(set.HasFramesToSend);
    }

    [TestMethod]
    public void Receive_ServerStreamFrame_OpensEveryLowerStreamOfItsTypeWithTheClientsLimits()
    {
        Diagnostics.Arrange("server frames", "STREAM 5 and STREAM 7, one byte each");
        QuicStreamSet set = Set();

        set.Receive(new QuicStreamFrame(5, 0, Bytes(1), false));
        set.Receive(new QuicStreamFrame(7, 0, Bytes(1), false));

        ulong[] bidirectionalIds = AcceptAll(set.AcceptBidirectional).Select(stream => stream.Id).ToArray();
        Diagnostics.Act("accepted bidirectional ids", string.Join(",", bidirectionalIds));
        Diagnostics.Assert("accepted bidirectional ids", "1,5", string.Join(",", bidirectionalIds));
        CollectionAssert.AreEqual(new ulong[] { 1, 5 }, bidirectionalIds);
        QuicStream[] unidirectional = AcceptAll(set.AcceptUnidirectional);
        ulong[] unidirectionalIds = unidirectional.Select(stream => stream.Id).ToArray();
        Diagnostics.Act("accepted unidirectional ids", string.Join(",", unidirectionalIds));
        Diagnostics.Assert("accepted unidirectional ids", "3,7", string.Join(",", unidirectionalIds));
        CollectionAssert.AreEqual(new ulong[] { 3, 7 }, unidirectionalIds);
        Diagnostics.Assert("server unidirectional can write", false, unidirectional[0].CanWrite);
        Assert.IsFalse(unidirectional[0].CanWrite);
        Diagnostics.Arrange("overflowing frame length on stream 1", 101);
        Assert.AreEqual(QuicTransportErrorCode.FlowControlError, ErrorOfLogged("101 bytes past the 100-byte limit", QuicTransportErrorCode.FlowControlError, () => set.Receive(new QuicStreamFrame(1, 0, Bytes(101), false))));
    }

    [TestMethod]
    public void Receive_ServerBidirectionalStream_CanBeWrittenUpToTheServersBidiLocalLimit()
    {
        Diagnostics.Arrange("server bidi-local limit", 20);
        Diagnostics.Arrange("payload length", 25);
        QuicStreamSet set = Set();
        set.Receive(new QuicMaxStreamDataFrame(1, 5));
        QuicStream stream = set.AcceptBidirectional()!;

        stream.Write(Bytes(25), endStream: false);

        int length = Take(set).OfType<QuicStreamFrame>().Single().Data.Length;
        Diagnostics.Act("stream data length sent", length);
        Diagnostics.Assert("stream data length sent", 20, length);
        Assert.AreEqual(20, length);
    }

    [TestMethod]
    [DataRow(2UL, DisplayName = "STREAM on the client's unidirectional stream")]
    [DataRow(4UL, DisplayName = "STREAM on a client stream not opened")]
    public void Receive_StreamFrameOnAStreamWithNoReceivingPart_IsAStreamStateError(ulong streamId)
    {
        Diagnostics.Arrange("stream id", streamId);
        QuicStreamSet set = Set();
        set.OpenUnidirectional();

        Assert.AreEqual(QuicTransportErrorCode.StreamStateError, ErrorOfLogged("STREAM frame", QuicTransportErrorCode.StreamStateError, () => set.Receive(new QuicStreamFrame(streamId, 0, Bytes(1), false))));
        Assert.AreEqual(QuicTransportErrorCode.StreamStateError, ErrorOfLogged("RESET_STREAM frame", QuicTransportErrorCode.StreamStateError, () => set.Receive(new QuicResetStreamFrame(streamId, 0, 0))));
        Assert.AreEqual(QuicTransportErrorCode.StreamStateError, ErrorOfLogged("STREAM_DATA_BLOCKED frame", QuicTransportErrorCode.StreamStateError, () => set.Receive(new QuicStreamDataBlockedFrame(streamId, 0))));
    }

    [TestMethod]
    public void Receive_SendingFrameOnTheServersUnidirectionalStream_IsAStreamStateError()
    {
        Diagnostics.Arrange("stream id", 3);
        QuicStreamSet set = Set();

        Assert.AreEqual(QuicTransportErrorCode.StreamStateError, ErrorOfLogged("STOP_SENDING frame", QuicTransportErrorCode.StreamStateError, () => set.Receive(new QuicStopSendingFrame(3, 0))));
        Assert.AreEqual(QuicTransportErrorCode.StreamStateError, ErrorOfLogged("MAX_STREAM_DATA frame", QuicTransportErrorCode.StreamStateError, () => set.Receive(new QuicMaxStreamDataFrame(3, 0))));
    }

    [TestMethod]
    public void Receive_InformationalAndOtherFrames_ChangeNothing()
    {
        Diagnostics.Arrange("frames", "DATA_BLOCKED, STREAMS_BLOCKED, PING, STREAM_DATA_BLOCKED");
        QuicStreamSet set = Set();

        set.Receive(new QuicDataBlockedFrame(5));
        set.Receive(new QuicStreamsBlockedFrame(true, 5));
        set.Receive(new QuicPingFrame());
        set.Receive(new QuicStreamDataBlockedFrame(1, 0));

        ulong acceptedId = set.AcceptBidirectional()!.Id;
        bool hasFrames = set.HasFramesToSend;
        Diagnostics.Act("accepted bidirectional id", acceptedId);
        Diagnostics.Assert("accepted bidirectional id", 1UL, acceptedId);
        Diagnostics.Assert("has frames to send", false, hasFrames);
        Assert.AreEqual(1UL, acceptedId);
        Assert.IsFalse(hasFrames);
    }

    [TestMethod]
    public void ClientBidirectionalStreamLimit_FollowsTheServersParametersAndMaxStreams()
    {
        Diagnostics.Arrange("server InitialMaxStreamsBidi", 3);
        Diagnostics.Arrange("MAX_STREAMS value", 7);
        QuicStreamSet set = new(SmallClientLimits);
        Diagnostics.Act("limit before server parameters", set.ClientBidirectionalStreamLimit);
        Diagnostics.Assert("limit before server parameters", 0UL, set.ClientBidirectionalStreamLimit);
        Assert.AreEqual(0UL, set.ClientBidirectionalStreamLimit);

        set.SetPeerTransportParameters(ServerLimits with { InitialMaxStreamsBidi = 3 });
        Diagnostics.Act("limit after server parameters", set.ClientBidirectionalStreamLimit);
        Diagnostics.Assert("limit after server parameters", 3UL, set.ClientBidirectionalStreamLimit);
        Assert.AreEqual(3UL, set.ClientBidirectionalStreamLimit);

        set.Receive(new QuicMaxStreamsFrame(false, 7));
        Diagnostics.Act("limit after MAX_STREAMS", set.ClientBidirectionalStreamLimit);
        Diagnostics.Assert("limit after MAX_STREAMS", 7UL, set.ClientBidirectionalStreamLimit);
        Assert.AreEqual(7UL, set.ClientBidirectionalStreamLimit);
    }

    [TestMethod]
    public void Receive_MaxFramesBelowTheCurrentLimits_AreIgnored()
    {
        Diagnostics.Arrange("MAX frames", "MAX_DATA 1, MAX_STREAM_DATA 1, MAX_STREAMS 1");
        QuicStreamSet set = Set();
        QuicStream stream = set.OpenBidirectional()!;

        set.Receive(new QuicMaxDataFrame(1));
        set.Receive(new QuicMaxStreamDataFrame(0, 1));
        set.Receive(new QuicMaxStreamsFrame(false, 1));
        stream.Write(Bytes(10), endStream: false);

        int length = Take(set).OfType<QuicStreamFrame>().Single().Data.Length;
        QuicStream? second = set.OpenBidirectional();
        Diagnostics.Act("stream data length sent", length);
        Diagnostics.Act("second stream opened", second is not null);
        Diagnostics.Assert("stream data length sent", 10, length);
        Diagnostics.Assert("second stream opened", true, second is not null);
        Assert.AreEqual(10, length);
        Assert.IsNotNull(second);
    }

    [TestMethod]
    [DataRow(0UL, 5UL, true, 3UL, 1UL, true, DisplayName = "A second, different final size")]
    [DataRow(0UL, 5UL, true, 5UL, 1UL, false, DisplayName = "Data past the final size")]
    [DataRow(0UL, 5UL, false, 0UL, 3UL, true, DisplayName = "A final size below what arrived")]
    public void Receive_FinalSizeBroken_IsAFinalSizeError(ulong firstOffset, ulong firstLength, bool firstFin, ulong secondOffset, ulong secondLength, bool secondFin)
    {
        Diagnostics.Arrange("first frame (offset, length, fin)", (firstOffset, firstLength, firstFin));
        Diagnostics.Arrange("second frame (offset, length, fin)", (secondOffset, secondLength, secondFin));
        QuicStreamSet set = Set();
        set.Receive(new QuicStreamFrame(3, firstOffset, Bytes((int)firstLength), firstFin));

        Assert.AreEqual(QuicTransportErrorCode.FinalSizeError, ErrorOfLogged("second frame", QuicTransportErrorCode.FinalSizeError, () => set.Receive(new QuicStreamFrame(3, secondOffset, Bytes((int)secondLength), secondFin))));
    }

    [TestMethod]
    public void Receive_ResetBelowWhatArrived_IsAFinalSizeError()
    {
        Diagnostics.Arrange("bytes arrived", 5);
        Diagnostics.Arrange("RESET_STREAM final size", 4);
        QuicStreamSet set = Set();
        set.Receive(new QuicStreamFrame(3, 0, Bytes(5), false));

        Assert.AreEqual(QuicTransportErrorCode.FinalSizeError, ErrorOfLogged("RESET_STREAM below received", QuicTransportErrorCode.FinalSizeError, () => set.Receive(new QuicResetStreamFrame(3, 0, 4))));
    }

    [TestMethod]
    public void Receive_SameFinalSizeAgainAndAfterReset_IsAccepted()
    {
        Diagnostics.Arrange("final size", 5);
        Diagnostics.Arrange("reset error code", 7);
        QuicStreamSet set = Set();
        set.Receive(new QuicStreamFrame(3, 0, Bytes(5), true));
        set.Receive(new QuicStreamFrame(3, 2, Bytes(3), true));
        set.Receive(new QuicResetStreamFrame(3, 7, 5));
        set.Receive(new QuicResetStreamFrame(3, 8, 5));
        set.Receive(new QuicStreamFrame(3, 0, Bytes(5), true));
        QuicStream stream = set.AcceptUnidirectional()!;

        int read = stream.Read(new byte[5]);
        Diagnostics.Act("peer reset error code", stream.PeerResetErrorCode);
        Diagnostics.Act("bytes read", read);
        Diagnostics.Assert("peer reset error code", 7UL, stream.PeerResetErrorCode);
        Diagnostics.Assert("bytes read", 0, read);
        Assert.AreEqual(7UL, stream.PeerResetErrorCode);
        Assert.AreEqual(0, read);
    }

    [TestMethod]
    public void Receive_OverlappingChunks_DeliversEachByteOnce()
    {
        Diagnostics.Arrange("chunks", "7 overlapping chunks covering offsets 0 to 7");
        QuicStreamSet set = Set();

        set.Receive(new QuicStreamFrame(3, 4, new byte[] { 4, 5 }, false));
        set.Receive(new QuicStreamFrame(3, 4, new byte[] { 4 }, false));
        set.Receive(new QuicStreamFrame(3, 2, new byte[] { 2 }, false));
        set.Receive(new QuicStreamFrame(3, 2, new byte[] { 2, 3, 4 }, false));
        set.Receive(new QuicStreamFrame(3, 0, new byte[] { 0, 1, 2, 3, 4, 5, 6 }, false));
        set.Receive(new QuicStreamFrame(3, 1, new byte[] { 1 }, false));
        set.Receive(new QuicStreamFrame(3, 5, new byte[] { 5, 6, 7 }, false));
        QuicStream stream = set.AcceptUnidirectional()!;

        byte[] actual = ReadAll(stream);
        Diagnostics.Bytes("delivered", actual);
        Diagnostics.Act("delivered length", actual.Length);
        Diagnostics.Diff("delivered", Bytes(8), actual);
        CollectionAssert.AreEqual(Bytes(8), actual);
    }

    [TestMethod]
    public void Read_SmallBuffer_HandsOutBytesAcrossChunksInOrder()
    {
        Diagnostics.Arrange("chunks", "offset 0 length 3, offset 3 length 2");
        Diagnostics.Arrange("read buffer length", 2);
        QuicStreamSet set = Set();
        set.Receive(new QuicStreamFrame(3, 0, new byte[] { 0, 1, 2 }, false));
        set.Receive(new QuicStreamFrame(3, 3, new byte[] { 3, 4 }, false));
        QuicStream stream = set.AcceptUnidirectional()!;
        byte[] buffer = new byte[2];

        List<byte> read = [];
        int count;
        while ((count = stream.Read(buffer)) > 0)
        {
            read.AddRange(buffer.Take(count));
        }

        Diagnostics.Bytes("read", read.ToArray());
        Diagnostics.Act("read complete", stream.IsReadComplete);
        Diagnostics.Diff("read", Bytes(5), read.ToArray());
        Diagnostics.Assert("read complete", false, stream.IsReadComplete);
        CollectionAssert.AreEqual(Bytes(5), read.ToArray());
        Assert.IsFalse(stream.IsReadComplete);
    }

    [TestMethod]
    public void ReadAndWrite_WrongDirection_Throw()
    {
        Diagnostics.Arrange("streams", "client unidirectional (read) and server unidirectional (write)");
        QuicStreamSet set = Set();
        QuicStream clientUnidirectional = set.OpenUnidirectional()!;
        set.Receive(new QuicStreamFrame(3, 0, Bytes(1), false));
        QuicStream serverUnidirectional = set.AcceptUnidirectional()!;

        InvalidOperationException readError = Assert.ThrowsExactly<InvalidOperationException>(() => clientUnidirectional.Read(new byte[1]));
        InvalidOperationException writeError = Assert.ThrowsExactly<InvalidOperationException>(() => serverUnidirectional.Write(Bytes(1), false));

        Diagnostics.Act("read on send-only stream", readError.GetType().Name + ": " + readError.Message);
        Diagnostics.Act("write on receive-only stream", writeError.GetType().Name + ": " + writeError.Message);
        Diagnostics.Assert("read exception type", nameof(InvalidOperationException), readError.GetType().Name);
        Diagnostics.Assert("write exception type", nameof(InvalidOperationException), writeError.GetType().Name);
    }

    [TestMethod]
    public void Write_AfterFin_Throws()
    {
        Diagnostics.Arrange("first write", "empty with endStream");
        QuicStream stream = Set().OpenBidirectional()!;
        stream.Write(ReadOnlySpan<byte>.Empty, endStream: true);

        InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(() => stream.Write(Bytes(1), false));

        Diagnostics.Act("write after fin", error.GetType().Name + ": " + error.Message);
        Diagnostics.Assert("exception type", nameof(InvalidOperationException), error.GetType().Name);
    }

    [TestMethod]
    public void TakeFrames_FinWithNoBytes_GoesEvenWithNoCreditLeft()
    {
        Diagnostics.Arrange("server InitialMaxData", 0);
        QuicStreamSet set = Set(ServerLimits with { InitialMaxData = 0 });
        QuicStream stream = set.OpenBidirectional()!;

        stream.Write(ReadOnlySpan<byte>.Empty, endStream: true);
        QuicStreamFrame fin = (QuicStreamFrame)Take(set).Single();

        Diagnostics.Act("fin frame (stream, offset, length, fin)", (fin.StreamId, fin.Offset, fin.Data.Length, fin.IsFin));
        Diagnostics.Assert("fin frame", (0UL, 0UL, 0, true), (fin.StreamId, fin.Offset, fin.Data.Length, fin.IsFin));
        Assert.AreEqual((0UL, 0UL, 0, true), (fin.StreamId, fin.Offset, fin.Data.Length, fin.IsFin));
        Diagnostics.Assert("has frames to send", false, set.HasFramesToSend);
        Assert.IsFalse(set.HasFramesToSend);
    }

    [TestMethod]
    public void TakeFrames_ManyWrites_SplitsThemAcrossFramesAndPackets()
    {
        Diagnostics.Arrange("writes", "30 bytes then 30 bytes with fin");
        Diagnostics.Arrange("first packet room", QuicStreamSet.StreamFrameOverhead + 40);
        QuicStreamSet set = Set(ServerLimits with { InitialMaxStreamDataBidiRemote = 1000 });
        QuicStream stream = set.OpenBidirectional()!;
        stream.Write(Bytes(30), endStream: false);
        stream.Write(Bytes(30), endStream: true);

        List<QuicFrame> first = [];
        set.TakeFrames(first, QuicStreamSet.StreamFrameOverhead + 40);
        List<QuicFrame> tooSmall = [];
        set.TakeFrames(tooSmall, QuicStreamSet.StreamFrameOverhead);
        List<QuicFrame> second = Take(set);

        QuicStreamFrame head = (QuicStreamFrame)first.Single();
        QuicStreamFrame tail = (QuicStreamFrame)second.Single();
        Diagnostics.Act("head (offset, length, fin)", (head.Offset, head.Data.Length, head.IsFin));
        Diagnostics.Act("tail (offset, length, fin)", (tail.Offset, tail.Data.Length, tail.IsFin));
        Diagnostics.Act("frames taken with too little room", tooSmall.Count);
        Diagnostics.Assert("head", (0UL, 40, false), (head.Offset, head.Data.Length, head.IsFin));
        Diagnostics.Assert("tail", (40UL, 20, true), (tail.Offset, tail.Data.Length, tail.IsFin));
        Assert.AreEqual((0UL, 40, false), (head.Offset, head.Data.Length, head.IsFin));
        Assert.AreEqual((40UL, 20, true), (tail.Offset, tail.Data.Length, tail.IsFin));
        Assert.IsEmpty(tooSmall);
        byte[] expected = [.. Bytes(30), .. Bytes(30)];
        byte[] actual = [.. head.Data.Span, .. tail.Data.Span];
        Diagnostics.Diff("reassembled payload", expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TakeFrames_ControlFrameLargerThanTheRoom_WaitsForTheNextPacket()
    {
        Diagnostics.Arrange("room", 2);
        Diagnostics.Arrange("abort error code", 0x3fffffff);
        QuicStreamSet set = Set();
        set.OpenBidirectional()!.Abort(0x3fffffff);

        List<QuicFrame> frames = [];
        set.TakeFrames(frames, 2);

        Diagnostics.Act("frames taken", frames.Count);
        Diagnostics.Act("has frames to send", set.HasFramesToSend);
        Diagnostics.Assert("frames taken", 0, frames.Count);
        Diagnostics.Assert("has frames to send", true, set.HasFramesToSend);
        Assert.IsEmpty(frames);
        Assert.IsTrue(set.HasFramesToSend);
    }

    [TestMethod]
    public void Abort_BidirectionalStream_SendsResetStreamAndStopSendingOnceAndDiscardsWhatArrives()
    {
        Diagnostics.Arrange("bytes written", 5);
        Diagnostics.Arrange("bytes received before abort", 60);
        Diagnostics.Arrange("abort error codes", "0x10c then 0x10d");
        QuicStreamSet set = Set();
        QuicStream stream = set.OpenBidirectional()!;
        stream.Write(Bytes(5), endStream: false);
        Take(set);
        set.Receive(new QuicStreamFrame(0, 0, Bytes(60), false));

        stream.Abort(0x10c);
        stream.Abort(0x10d);
        set.Receive(new QuicStreamFrame(0, 60, Bytes(40), true));
        List<QuicFrame> frames = Take(set);

        Diagnostics.Act("frames taken", string.Join("; ", frames));
        Diagnostics.Act("readable length", stream.ReadableLength);
        Diagnostics.Assert("frame count", 3, frames.Count);
        Diagnostics.Assert("readable length", 0, stream.ReadableLength);
        Diagnostics.Assert("read complete", true, stream.IsReadComplete);
        Diagnostics.Assert("read abandoned", true, stream.IsReadAbandoned);
        Diagnostics.Assert("send reset", true, stream.IsSendReset);
        CollectionAssert.AreEqual(
            new QuicFrame[] { new QuicResetStreamFrame(0, 0x10c, 5), new QuicStopSendingFrame(0, 0x10c), new QuicMaxDataFrame(200) },
            frames);
        Assert.AreEqual(0, stream.ReadableLength);
        Assert.IsTrue(stream.IsReadComplete);
        Assert.IsTrue(stream.IsReadAbandoned);
        Assert.IsTrue(stream.IsSendReset);
        InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(() => stream.Write(Bytes(1), false));
        Diagnostics.Act("write after abort", error.GetType().Name + ": " + error.Message);
        Diagnostics.Assert("write exception type", nameof(InvalidOperationException), error.GetType().Name);
    }

    [TestMethod]
    public void Abort_OneWayOrFinishedStreams_SendsOnlyWhatApplies()
    {
        Diagnostics.Arrange("streams", "client uni 2 aborted 1, finished server uni 3 aborted 2, server uni 7 aborted 3");
        QuicStreamSet set = Set();
        set.OpenUnidirectional()!.Abort(1);
        set.Receive(new QuicStreamFrame(3, 0, Bytes(2), true));
        QuicStream finished = set.AcceptUnidirectional()!;
        ReadAll(finished);
        finished.Abort(2);
        set.Receive(new QuicStreamFrame(7, 0, Bytes(2), false));
        set.AcceptUnidirectional()!.Abort(3);

        List<QuicFrame> frames = Take(set).Where(frame => frame is not QuicMaxStreamsFrame).ToList();

        Diagnostics.Act("frames taken (without MAX_STREAMS)", string.Join("; ", frames));
        Diagnostics.Assert("frame count", 2, frames.Count);
        CollectionAssert.AreEqual(new QuicFrame[] { new QuicResetStreamFrame(2, 1, 0), new QuicStopSendingFrame(7, 3) }, frames);
    }

    [TestMethod]
    public void Receive_StopSendingTwice_KeepsTheFirstCodeAndResetsOnce()
    {
        Diagnostics.Arrange("STOP_SENDING codes", "5 then 6");
        QuicStreamSet set = Set();
        QuicStream stream = set.OpenBidirectional()!;

        set.Receive(new QuicStopSendingFrame(0, 5));
        set.Receive(new QuicStopSendingFrame(0, 6));

        QuicFrame taken = Take(set).Single();
        Diagnostics.Act("peer stop-sending code", stream.PeerStopSendingErrorCode);
        Diagnostics.Act("frame taken", taken);
        Diagnostics.Assert("peer stop-sending code", 5UL, stream.PeerStopSendingErrorCode);
        Diagnostics.Assert("frame taken", new QuicResetStreamFrame(0, 5, 0), taken);
        Assert.AreEqual(5UL, stream.PeerStopSendingErrorCode);
        Assert.AreEqual(new QuicResetStreamFrame(0, 5, 0), taken);
    }

    [TestMethod]
    public void ShouldResend_StreamDataOfAResetStreamOnly_IsDropped()
    {
        Diagnostics.Arrange("streams", "0 aborted, 4 open");
        QuicStreamSet set = Set();
        set.OpenBidirectional()!.Abort(1);
        set.OpenBidirectional();

        bool resetStreamData = set.ShouldResend(new QuicStreamFrame(0, 0, Bytes(1), false));
        bool openStreamData = set.ShouldResend(new QuicStreamFrame(4, 0, Bytes(1), false));
        bool resetFrame = set.ShouldResend(new QuicResetStreamFrame(0, 1, 0));

        Diagnostics.Act("resend data of reset stream", resetStreamData);
        Diagnostics.Act("resend data of open stream", openStreamData);
        Diagnostics.Act("resend RESET_STREAM", resetFrame);
        Diagnostics.Assert("resend data of reset stream", false, resetStreamData);
        Diagnostics.Assert("resend data of open stream", true, openStreamData);
        Diagnostics.Assert("resend RESET_STREAM", true, resetFrame);
        Assert.IsFalse(resetStreamData);
        Assert.IsTrue(openStreamData);
        Assert.IsTrue(resetFrame);
    }

    [TestMethod]
    public void ServerStreamsClosing_RaiseMaxStreamsOfTheirType()
    {
        Diagnostics.Arrange("server streams", "uni 3 and 7 finished, bidi 5 and 1 reset");
        QuicStreamSet set = Set();
        set.Receive(new QuicStreamFrame(3, 0, Bytes(1), true));
        set.Receive(new QuicStreamFrame(7, 0, Bytes(1), true));
        set.Receive(new QuicResetStreamFrame(5, 0, 0));
        set.Receive(new QuicResetStreamFrame(1, 0, 0));
        QuicStream[] unidirectional = AcceptAll(set.AcceptUnidirectional);
        QuicStream[] bidirectional = AcceptAll(set.AcceptBidirectional);
        Diagnostics.Assert("has frames after accepting", false, set.HasFramesToSend);
        Assert.IsFalse(set.HasFramesToSend);

        ReadAll(unidirectional[0]);
        ReadAll(unidirectional[0]);
        Diagnostics.Assert("has frames after reading the first uni stream", false, set.HasFramesToSend);
        Assert.IsFalse(set.HasFramesToSend);
        ReadAll(unidirectional[1]);
        QuicFrame maxStreams = Take(set).Single();
        Diagnostics.Act("frame after both uni streams read", maxStreams);
        Diagnostics.Assert("frame after both uni streams read", new QuicMaxStreamsFrame(true, 4), maxStreams);
        Assert.AreEqual(new QuicMaxStreamsFrame(true, 4), maxStreams);
        bidirectional[0].Write(ReadOnlySpan<byte>.Empty, endStream: true);
        bool isFin = ((QuicStreamFrame)Take(set).Single()).IsFin;
        Diagnostics.Assert("fin frame sent", true, isFin);
        Assert.IsTrue(isFin);
        bidirectional[1].Abort(1);

        List<QuicFrame> frames = Take(set);
        Diagnostics.Act("frames after abort", string.Join("; ", frames));
        Diagnostics.Assert("frame count", 2, frames.Count);
        CollectionAssert.AreEqual(new QuicFrame[] { new QuicResetStreamFrame(5, 1, 0), new QuicMaxStreamsFrame(false, 4) }, frames);
    }

    [TestMethod]
    public void Credits_RaiseOnlyWhileThePeerMaySend()
    {
        Diagnostics.Arrange("bytes received with fin", 60);
        QuicStreamSet set = Set();
        set.Receive(new QuicStreamFrame(3, 0, Bytes(60), true));
        ReadAll(set.AcceptUnidirectional()!);

        QuicFrame credit = Take(set).Where(frame => frame is not QuicMaxStreamsFrame).Single();

        Diagnostics.Act("credit frame", credit);
        Diagnostics.Assert("credit frame", new QuicMaxDataFrame(160), credit);
        Assert.AreEqual(new QuicMaxDataFrame(160), credit);
    }

    [TestMethod]
    public void HasFramesToSend_OnlyAStreamLimitDue_IsTrueAndSendsMaxStreamData()
    {
        Diagnostics.Arrange("client InitialMaxData", 1000);
        Diagnostics.Arrange("bytes received without fin", 60);
        QuicStreamSet set = new(SmallClientLimits with { InitialMaxData = 1000 });
        set.SetPeerTransportParameters(ServerLimits);
        set.Receive(new QuicStreamFrame(3, 0, Bytes(60), false));

        ReadAll(set.AcceptUnidirectional()!);

        bool hasFrames = set.HasFramesToSend;
        QuicFrame taken = Take(set).Single();
        Diagnostics.Act("has frames to send", hasFrames);
        Diagnostics.Act("frame taken", taken);
        Diagnostics.Assert("has frames to send", true, hasFrames);
        Diagnostics.Assert("frame taken", new QuicMaxStreamDataFrame(3, 160), taken);
        Assert.IsTrue(hasFrames);
        Assert.AreEqual(new QuicMaxStreamDataFrame(3, 160), taken);
    }

    [TestMethod]
    public void SendCredit_BlockedReportsOncePerLimit()
    {
        Diagnostics.Arrange("send credit limit", 2);
        QuicSendCredit credit = new(2);

        bool beforeUse = credit.TakeBlockedReport();
        Diagnostics.Act("blocked report before use", beforeUse);
        Diagnostics.Assert("blocked report before use", false, beforeUse);
        Assert.IsFalse(beforeUse);
        credit.Use(2);
        bool atLimit = credit.TakeBlockedReport();
        bool again = credit.TakeBlockedReport();
        Diagnostics.Act("blocked report at limit", atLimit);
        Diagnostics.Act("blocked report again", again);
        Diagnostics.Assert("blocked report at limit", true, atLimit);
        Diagnostics.Assert("blocked report again", false, again);
        Assert.IsTrue(atLimit);
        Assert.IsFalse(again);
        credit.Raise(3);
        credit.Use(1);
        bool afterRaise = credit.TakeBlockedReport();
        Diagnostics.Act("blocked report after raise to 3 and use 1", afterRaise);
        Diagnostics.Assert("blocked report after raise", true, afterRaise);
        Assert.IsTrue(afterRaise);
    }

    [TestMethod]
    public void ReceiveCredit_ZeroWindow_NeverRaises()
    {
        Diagnostics.Arrange("window", 0);
        QuicReceiveCredit credit = new(0, QuicTransportErrorCode.FlowControlError, "nothing");

        ulong received = credit.Receive(0);
        Diagnostics.Act("received 0 bytes returns", received);
        Diagnostics.Assert("received 0 bytes returns", 0UL, received);
        Assert.AreEqual(0UL, received);
        Diagnostics.Assert("raise due", false, credit.IsRaiseDue);
        Assert.IsFalse(credit.IsRaiseDue);
        Assert.AreEqual(QuicTransportErrorCode.FlowControlError, ErrorOfLogged("receiving 1 byte", QuicTransportErrorCode.FlowControlError, () => credit.Receive(1)));
    }

    private static QuicStreamSet Set(QuicTransportParameters? server = null)
    {
        QuicStreamSet set = new(SmallClientLimits);
        set.SetPeerTransportParameters(server ?? ServerLimits);
        return set;
    }

    private static List<QuicFrame> Take(QuicStreamSet set)
    {
        List<QuicFrame> frames = [];
        set.TakeFrames(frames, 1100);
        return frames;
    }

    private static QuicStream[] AcceptAll(Func<QuicStream?> accept)
    {
        List<QuicStream> streams = [];
        while (accept() is { } stream)
        {
            streams.Add(stream);
        }

        return [.. streams];
    }

    private QuicTransportErrorCode ErrorOfLogged(string label, QuicTransportErrorCode expected, Action action)
    {
        QuicTransportErrorCode actual = ErrorOf(action);
        Diagnostics.Act(label + " error code", actual);
        Diagnostics.Assert(label + " error code", expected, actual);
        return actual;
    }
}
