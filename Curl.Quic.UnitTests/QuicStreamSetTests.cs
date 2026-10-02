using static Curl.Quic.QuicStreamTest;
using static Curl.Quic.QuicTest;

namespace Curl.Quic;

/// <summary>The stream set and its streams on their own, frame by frame (RFC 9000 sections 2 to 4 and 19.4 to 19.14).</summary>
[TestClass]
public sealed class QuicStreamSetTests
{
    private static readonly QuicTransportParameters ServerLimits = GenerousServerLimits(new QuicTransportParameters()) with { InitialMaxStreamDataBidiRemote = 10, InitialMaxStreamDataBidiLocal = 20, InitialMaxStreamDataUni = 30 };

    [TestMethod]
    public void Constructor_NullParameters_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new QuicStreamSet(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new QuicStreamSet(SmallClientLimits).SetPeerTransportParameters(null!));
    }

    [TestMethod]
    public void Open_BeforeTheServersParameters_AllowsNoStream()
    {
        QuicStreamSet set = new(SmallClientLimits);

        Assert.IsNull(set.OpenBidirectional());
        Assert.IsTrue(set.HasFramesToSend);
        Assert.AreEqual(new QuicStreamsBlockedFrame(false, 0), Take(set).Single());
        Assert.IsFalse(set.HasFramesToSend);
    }

    [TestMethod]
    public void Open_TakesTheServersLimitsForEachKindOfStream()
    {
        QuicStreamSet set = Set();

        QuicStream bidirectional = set.OpenBidirectional()!;
        QuicStream unidirectional = set.OpenUnidirectional()!;
        bidirectional.Write(Bytes(50), endStream: false);
        unidirectional.Write(Bytes(50), endStream: false);

        Assert.AreEqual((0UL, true, true, true, false), (bidirectional.Id, bidirectional.IsClientInitiated, bidirectional.CanRead, bidirectional.CanWrite, bidirectional.IsUnidirectional));
        Assert.AreEqual((2UL, false, true, true), (unidirectional.Id, unidirectional.CanRead, unidirectional.CanWrite, unidirectional.IsUnidirectional));
        List<QuicFrame> frames = Take(set);
        Assert.AreEqual(10, frames.OfType<QuicStreamFrame>().Single(frame => frame.StreamId == 0).Data.Length);
        Assert.AreEqual(30, frames.OfType<QuicStreamFrame>().Single(frame => frame.StreamId == 2).Data.Length);
        CollectionAssert.AreEqual(new QuicFrame[] { new QuicStreamDataBlockedFrame(0, 10), new QuicStreamDataBlockedFrame(2, 30) }, frames.OfType<QuicStreamDataBlockedFrame>().ToList());
        Assert.IsFalse(set.HasFramesToSend);
    }

    [TestMethod]
    public void Receive_ServerStreamFrame_OpensEveryLowerStreamOfItsTypeWithTheClientsLimits()
    {
        QuicStreamSet set = Set();

        set.Receive(new QuicStreamFrame(5, 0, Bytes(1), false));
        set.Receive(new QuicStreamFrame(7, 0, Bytes(1), false));

        CollectionAssert.AreEqual(new ulong[] { 1, 5 }, AcceptAll(set.AcceptBidirectional).Select(stream => stream.Id).ToArray());
        QuicStream[] unidirectional = AcceptAll(set.AcceptUnidirectional);
        CollectionAssert.AreEqual(new ulong[] { 3, 7 }, unidirectional.Select(stream => stream.Id).ToArray());
        Assert.IsFalse(unidirectional[0].CanWrite);
        Assert.AreEqual(QuicTransportErrorCode.FlowControlError, ErrorOf(() => set.Receive(new QuicStreamFrame(1, 0, Bytes(101), false))));
    }

    [TestMethod]
    public void Receive_ServerBidirectionalStream_CanBeWrittenUpToTheServersBidiLocalLimit()
    {
        QuicStreamSet set = Set();
        set.Receive(new QuicMaxStreamDataFrame(1, 5));
        QuicStream stream = set.AcceptBidirectional()!;

        stream.Write(Bytes(25), endStream: false);

        Assert.AreEqual(20, Take(set).OfType<QuicStreamFrame>().Single().Data.Length);
    }

    [TestMethod]
    [DataRow(2UL, DisplayName = "STREAM on the client's unidirectional stream")]
    [DataRow(4UL, DisplayName = "STREAM on a client stream not opened")]
    public void Receive_StreamFrameOnAStreamWithNoReceivingPart_IsAStreamStateError(ulong streamId)
    {
        QuicStreamSet set = Set();
        set.OpenUnidirectional();

        Assert.AreEqual(QuicTransportErrorCode.StreamStateError, ErrorOf(() => set.Receive(new QuicStreamFrame(streamId, 0, Bytes(1), false))));
        Assert.AreEqual(QuicTransportErrorCode.StreamStateError, ErrorOf(() => set.Receive(new QuicResetStreamFrame(streamId, 0, 0))));
        Assert.AreEqual(QuicTransportErrorCode.StreamStateError, ErrorOf(() => set.Receive(new QuicStreamDataBlockedFrame(streamId, 0))));
    }

    [TestMethod]
    public void Receive_SendingFrameOnTheServersUnidirectionalStream_IsAStreamStateError()
    {
        QuicStreamSet set = Set();

        Assert.AreEqual(QuicTransportErrorCode.StreamStateError, ErrorOf(() => set.Receive(new QuicStopSendingFrame(3, 0))));
        Assert.AreEqual(QuicTransportErrorCode.StreamStateError, ErrorOf(() => set.Receive(new QuicMaxStreamDataFrame(3, 0))));
    }

    [TestMethod]
    public void Receive_InformationalAndOtherFrames_ChangeNothing()
    {
        QuicStreamSet set = Set();

        set.Receive(new QuicDataBlockedFrame(5));
        set.Receive(new QuicStreamsBlockedFrame(true, 5));
        set.Receive(new QuicPingFrame());
        set.Receive(new QuicStreamDataBlockedFrame(1, 0));

        Assert.AreEqual(1UL, set.AcceptBidirectional()!.Id);
        Assert.IsFalse(set.HasFramesToSend);
    }

    [TestMethod]
    public void ClientBidirectionalStreamLimit_FollowsTheServersParametersAndMaxStreams()
    {
        QuicStreamSet set = new(SmallClientLimits);
        Assert.AreEqual(0UL, set.ClientBidirectionalStreamLimit);

        set.SetPeerTransportParameters(ServerLimits with { InitialMaxStreamsBidi = 3 });
        Assert.AreEqual(3UL, set.ClientBidirectionalStreamLimit);

        set.Receive(new QuicMaxStreamsFrame(false, 7));
        Assert.AreEqual(7UL, set.ClientBidirectionalStreamLimit);
    }

    [TestMethod]
    public void Receive_MaxFramesBelowTheCurrentLimits_AreIgnored()
    {
        QuicStreamSet set = Set();
        QuicStream stream = set.OpenBidirectional()!;

        set.Receive(new QuicMaxDataFrame(1));
        set.Receive(new QuicMaxStreamDataFrame(0, 1));
        set.Receive(new QuicMaxStreamsFrame(false, 1));
        stream.Write(Bytes(10), endStream: false);

        Assert.AreEqual(10, Take(set).OfType<QuicStreamFrame>().Single().Data.Length);
        Assert.IsNotNull(set.OpenBidirectional());
    }

    [TestMethod]
    [DataRow(0UL, 5UL, true, 3UL, 1UL, true, DisplayName = "A second, different final size")]
    [DataRow(0UL, 5UL, true, 5UL, 1UL, false, DisplayName = "Data past the final size")]
    [DataRow(0UL, 5UL, false, 0UL, 3UL, true, DisplayName = "A final size below what arrived")]
    public void Receive_FinalSizeBroken_IsAFinalSizeError(ulong firstOffset, ulong firstLength, bool firstFin, ulong secondOffset, ulong secondLength, bool secondFin)
    {
        QuicStreamSet set = Set();
        set.Receive(new QuicStreamFrame(3, firstOffset, Bytes((int)firstLength), firstFin));

        Assert.AreEqual(QuicTransportErrorCode.FinalSizeError, ErrorOf(() => set.Receive(new QuicStreamFrame(3, secondOffset, Bytes((int)secondLength), secondFin))));
    }

    [TestMethod]
    public void Receive_ResetBelowWhatArrived_IsAFinalSizeError()
    {
        QuicStreamSet set = Set();
        set.Receive(new QuicStreamFrame(3, 0, Bytes(5), false));

        Assert.AreEqual(QuicTransportErrorCode.FinalSizeError, ErrorOf(() => set.Receive(new QuicResetStreamFrame(3, 0, 4))));
    }

    [TestMethod]
    public void Receive_SameFinalSizeAgainAndAfterReset_IsAccepted()
    {
        QuicStreamSet set = Set();
        set.Receive(new QuicStreamFrame(3, 0, Bytes(5), true));
        set.Receive(new QuicStreamFrame(3, 2, Bytes(3), true));
        set.Receive(new QuicResetStreamFrame(3, 7, 5));
        set.Receive(new QuicResetStreamFrame(3, 8, 5));
        set.Receive(new QuicStreamFrame(3, 0, Bytes(5), true));
        QuicStream stream = set.AcceptUnidirectional()!;

        Assert.AreEqual(7UL, stream.PeerResetErrorCode);
        Assert.AreEqual(0, stream.Read(new byte[5]));
    }

    [TestMethod]
    public void Receive_OverlappingChunks_DeliversEachByteOnce()
    {
        QuicStreamSet set = Set();

        set.Receive(new QuicStreamFrame(3, 4, new byte[] { 4, 5 }, false));
        set.Receive(new QuicStreamFrame(3, 4, new byte[] { 4 }, false));
        set.Receive(new QuicStreamFrame(3, 2, new byte[] { 2 }, false));
        set.Receive(new QuicStreamFrame(3, 2, new byte[] { 2, 3, 4 }, false));
        set.Receive(new QuicStreamFrame(3, 0, new byte[] { 0, 1, 2, 3, 4, 5, 6 }, false));
        set.Receive(new QuicStreamFrame(3, 1, new byte[] { 1 }, false));
        set.Receive(new QuicStreamFrame(3, 5, new byte[] { 5, 6, 7 }, false));
        QuicStream stream = set.AcceptUnidirectional()!;

        CollectionAssert.AreEqual(Bytes(8), ReadAll(stream));
    }

    [TestMethod]
    public void Read_SmallBuffer_HandsOutBytesAcrossChunksInOrder()
    {
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

        CollectionAssert.AreEqual(Bytes(5), read.ToArray());
        Assert.IsFalse(stream.IsReadComplete);
    }

    [TestMethod]
    public void ReadAndWrite_WrongDirection_Throw()
    {
        QuicStreamSet set = Set();
        QuicStream clientUnidirectional = set.OpenUnidirectional()!;
        set.Receive(new QuicStreamFrame(3, 0, Bytes(1), false));
        QuicStream serverUnidirectional = set.AcceptUnidirectional()!;

        Assert.ThrowsExactly<InvalidOperationException>(() => clientUnidirectional.Read(new byte[1]));
        Assert.ThrowsExactly<InvalidOperationException>(() => serverUnidirectional.Write(Bytes(1), false));
    }

    [TestMethod]
    public void Write_AfterFin_Throws()
    {
        QuicStream stream = Set().OpenBidirectional()!;
        stream.Write(ReadOnlySpan<byte>.Empty, endStream: true);

        Assert.ThrowsExactly<InvalidOperationException>(() => stream.Write(Bytes(1), false));
    }

    [TestMethod]
    public void TakeFrames_FinWithNoBytes_GoesEvenWithNoCreditLeft()
    {
        QuicStreamSet set = Set(ServerLimits with { InitialMaxData = 0 });
        QuicStream stream = set.OpenBidirectional()!;

        stream.Write(ReadOnlySpan<byte>.Empty, endStream: true);
        QuicStreamFrame fin = (QuicStreamFrame)Take(set).Single();

        Assert.AreEqual((0UL, 0UL, 0, true), (fin.StreamId, fin.Offset, fin.Data.Length, fin.IsFin));
        Assert.IsFalse(set.HasFramesToSend);
    }

    [TestMethod]
    public void TakeFrames_ManyWrites_SplitsThemAcrossFramesAndPackets()
    {
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
        Assert.AreEqual((0UL, 40, false), (head.Offset, head.Data.Length, head.IsFin));
        Assert.AreEqual((40UL, 20, true), (tail.Offset, tail.Data.Length, tail.IsFin));
        Assert.IsEmpty(tooSmall);
        CollectionAssert.AreEqual((byte[])[.. Bytes(30), .. Bytes(30)], (byte[])[.. head.Data.Span, .. tail.Data.Span]);
    }

    [TestMethod]
    public void TakeFrames_ControlFrameLargerThanTheRoom_WaitsForTheNextPacket()
    {
        QuicStreamSet set = Set();
        set.OpenBidirectional()!.Abort(0x3fffffff);

        List<QuicFrame> frames = [];
        set.TakeFrames(frames, 2);

        Assert.IsEmpty(frames);
        Assert.IsTrue(set.HasFramesToSend);
    }

    [TestMethod]
    public void Abort_BidirectionalStream_SendsResetStreamAndStopSendingOnceAndDiscardsWhatArrives()
    {
        QuicStreamSet set = Set();
        QuicStream stream = set.OpenBidirectional()!;
        stream.Write(Bytes(5), endStream: false);
        Take(set);
        set.Receive(new QuicStreamFrame(0, 0, Bytes(60), false));

        stream.Abort(0x10c);
        stream.Abort(0x10d);
        set.Receive(new QuicStreamFrame(0, 60, Bytes(40), true));
        List<QuicFrame> frames = Take(set);

        CollectionAssert.AreEqual(
            new QuicFrame[] { new QuicResetStreamFrame(0, 0x10c, 5), new QuicStopSendingFrame(0, 0x10c), new QuicMaxDataFrame(200) },
            frames);
        Assert.AreEqual(0, stream.ReadableLength);
        Assert.IsTrue(stream.IsReadComplete);
        Assert.IsTrue(stream.IsReadAbandoned);
        Assert.IsTrue(stream.IsSendReset);
        Assert.ThrowsExactly<InvalidOperationException>(() => stream.Write(Bytes(1), false));
    }

    [TestMethod]
    public void Abort_OneWayOrFinishedStreams_SendsOnlyWhatApplies()
    {
        QuicStreamSet set = Set();
        set.OpenUnidirectional()!.Abort(1);
        set.Receive(new QuicStreamFrame(3, 0, Bytes(2), true));
        QuicStream finished = set.AcceptUnidirectional()!;
        ReadAll(finished);
        finished.Abort(2);
        set.Receive(new QuicStreamFrame(7, 0, Bytes(2), false));
        set.AcceptUnidirectional()!.Abort(3);

        CollectionAssert.AreEqual(new QuicFrame[] { new QuicResetStreamFrame(2, 1, 0), new QuicStopSendingFrame(7, 3) }, Take(set).Where(frame => frame is not QuicMaxStreamsFrame).ToList());
    }

    [TestMethod]
    public void Receive_StopSendingTwice_KeepsTheFirstCodeAndResetsOnce()
    {
        QuicStreamSet set = Set();
        QuicStream stream = set.OpenBidirectional()!;

        set.Receive(new QuicStopSendingFrame(0, 5));
        set.Receive(new QuicStopSendingFrame(0, 6));

        Assert.AreEqual(5UL, stream.PeerStopSendingErrorCode);
        Assert.AreEqual(new QuicResetStreamFrame(0, 5, 0), Take(set).Single());
    }

    [TestMethod]
    public void ShouldResend_StreamDataOfAResetStreamOnly_IsDropped()
    {
        QuicStreamSet set = Set();
        set.OpenBidirectional()!.Abort(1);
        set.OpenBidirectional();

        Assert.IsFalse(set.ShouldResend(new QuicStreamFrame(0, 0, Bytes(1), false)));
        Assert.IsTrue(set.ShouldResend(new QuicStreamFrame(4, 0, Bytes(1), false)));
        Assert.IsTrue(set.ShouldResend(new QuicResetStreamFrame(0, 1, 0)));
    }

    [TestMethod]
    public void ServerStreamsClosing_RaiseMaxStreamsOfTheirType()
    {
        QuicStreamSet set = Set();
        set.Receive(new QuicStreamFrame(3, 0, Bytes(1), true));
        set.Receive(new QuicStreamFrame(7, 0, Bytes(1), true));
        set.Receive(new QuicResetStreamFrame(5, 0, 0));
        set.Receive(new QuicResetStreamFrame(1, 0, 0));
        QuicStream[] unidirectional = AcceptAll(set.AcceptUnidirectional);
        QuicStream[] bidirectional = AcceptAll(set.AcceptBidirectional);
        Assert.IsFalse(set.HasFramesToSend);

        ReadAll(unidirectional[0]);
        ReadAll(unidirectional[0]);
        Assert.IsFalse(set.HasFramesToSend);
        ReadAll(unidirectional[1]);
        Assert.AreEqual(new QuicMaxStreamsFrame(true, 4), Take(set).Single());
        bidirectional[0].Write(ReadOnlySpan<byte>.Empty, endStream: true);
        Assert.IsTrue(((QuicStreamFrame)Take(set).Single()).IsFin);
        bidirectional[1].Abort(1);

        CollectionAssert.AreEqual(new QuicFrame[] { new QuicResetStreamFrame(5, 1, 0), new QuicMaxStreamsFrame(false, 4) }, Take(set));
    }

    [TestMethod]
    public void Credits_RaiseOnlyWhileThePeerMaySend()
    {
        QuicStreamSet set = Set();
        set.Receive(new QuicStreamFrame(3, 0, Bytes(60), true));
        ReadAll(set.AcceptUnidirectional()!);

        Assert.AreEqual(new QuicMaxDataFrame(160), Take(set).Where(frame => frame is not QuicMaxStreamsFrame).Single());
    }

    [TestMethod]
    public void HasFramesToSend_OnlyAStreamLimitDue_IsTrueAndSendsMaxStreamData()
    {
        QuicStreamSet set = new(SmallClientLimits with { InitialMaxData = 1000 });
        set.SetPeerTransportParameters(ServerLimits);
        set.Receive(new QuicStreamFrame(3, 0, Bytes(60), false));

        ReadAll(set.AcceptUnidirectional()!);

        Assert.IsTrue(set.HasFramesToSend);
        Assert.AreEqual(new QuicMaxStreamDataFrame(3, 160), Take(set).Single());
    }

    [TestMethod]
    public void SendCredit_BlockedReportsOncePerLimit()
    {
        QuicSendCredit credit = new(2);

        Assert.IsFalse(credit.TakeBlockedReport());
        credit.Use(2);
        Assert.IsTrue(credit.TakeBlockedReport());
        Assert.IsFalse(credit.TakeBlockedReport());
        credit.Raise(3);
        credit.Use(1);
        Assert.IsTrue(credit.TakeBlockedReport());
    }

    [TestMethod]
    public void ReceiveCredit_ZeroWindow_NeverRaises()
    {
        QuicReceiveCredit credit = new(0, QuicTransportErrorCode.FlowControlError, "nothing");

        Assert.AreEqual(0UL, credit.Receive(0));
        Assert.IsFalse(credit.IsRaiseDue);
        Assert.AreEqual(QuicTransportErrorCode.FlowControlError, ErrorOf(() => credit.Receive(1)));
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
}
