using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Rtsp.Fakes;

namespace Curl.Original.Protocol.Rtsp;

/// <summary>
/// Pins how <see cref="RtspInterleavedFrameFilter" /> splits <c>$</c> RTP frames out of the bytes
/// read and hands each one whole to its sink, as curl 8.22.0's <c>rtsp_filter_rtp</c> does (BL-2477).
/// </summary>
[TestClass]
public sealed class RtspInterleavedFrameFilterTests
{
    [TestMethod]
    public async Task ConsumeAsync_TwoWholeFrames_HandsEachWithItsHeaderAndConsumesAll()
    {
        var sink = new RecordingInterleaveSink();
        var filter = new RtspInterleavedFrameFilter(sink, EveryChannel);
        byte[] received = Bytes("$\0\0\u0004abcd$\u0001\0\u0002xy");

        int consumed = await filter.ConsumeAsync(received, stopAtReply: false, CancellationToken.None);

        Assert.AreEqual(received.Length, consumed);
        Assert.HasCount(2, sink.Frames);
        CollectionAssert.AreEqual(Bytes("$\0\0\u0004abcd"), sink.Frames[0]);
        CollectionAssert.AreEqual(Bytes("$\u0001\0\u0002xy"), sink.Frames[1]);
        Assert.AreEqual(14L, filter.BytesHanded);
    }

    [TestMethod]
    [DataRow("$")]
    [DataRow("$\0\0")]
    [DataRow("$\0\0\u0004abc")]
    public async Task ConsumeAsync_FrameNotYetWhole_ConsumesNothingAndHandsNothing(string received)
    {
        var sink = new RecordingInterleaveSink();

        int consumed = await new RtspInterleavedFrameFilter(sink, EveryChannel).ConsumeAsync(Bytes(received), stopAtReply: false, CancellationToken.None);

        Assert.AreEqual(0, consumed);
        Assert.IsEmpty(sink.Frames);
    }

    [TestMethod]
    public async Task ConsumeAsync_ChannelNotSetUp_SkipsTheDollarAsAStrayByte()
    {
        var sink = new RecordingInterleaveSink();
        byte[] received = Bytes("$_12$\0\0\u0001a");

        int consumed = await new RtspInterleavedFrameFilter(sink, channel => channel == 0).ConsumeAsync(received, stopAtReply: false, CancellationToken.None);

        Assert.AreEqual(received.Length, consumed);
        Assert.HasCount(1, sink.Frames);
        CollectionAssert.AreEqual(Bytes("$\0\0\u0001a"), sink.Frames[0]);
    }

    [TestMethod]
    public async Task ConsumeAsync_LengthBigEndian_ReadsTheHighByteFirst()
    {
        var sink = new RecordingInterleaveSink();
        byte[] frame = new byte[4 + 0x0102];
        frame[0] = (byte)'$';
        frame[2] = 0x01;
        frame[3] = 0x02;

        int consumed = await new RtspInterleavedFrameFilter(sink, EveryChannel).ConsumeAsync(frame, stopAtReply: false, CancellationToken.None);

        Assert.AreEqual(frame.Length, consumed);
        Assert.HasCount(1, sink.Frames);
    }

    [TestMethod]
    public async Task ConsumeAsync_StopAtReply_StopsWhereRtspMayBeginAfterSkippingStrayBytes()
    {
        var sink = new RecordingInterleaveSink();

        int consumed = await new RtspInterleavedFrameFilter(sink, EveryChannel).ConsumeAsync(Bytes("xx$\0\0\u0001aRTSP/1.0"), stopAtReply: true, CancellationToken.None);

        Assert.AreEqual(7, consumed);
        Assert.HasCount(1, sink.Frames);
    }

    [TestMethod]
    public async Task ConsumeAsync_NotStopAtReply_SkipsRtspAsStrayBytes()
    {
        var sink = new RecordingInterleaveSink();

        int consumed = await new RtspInterleavedFrameFilter(sink, EveryChannel).ConsumeAsync(Bytes("RTSP/$\0\0\0"), stopAtReply: false, CancellationToken.None);

        Assert.AreEqual(9, consumed);
        Assert.HasCount(1, sink.Frames);
    }

    [TestMethod]
    public async Task ConsumeAsync_SinkTakesTooFewBytes_FailsWith23()
    {
        var filter = new RtspInterleavedFrameFilter(new RecordingInterleaveSink(shortBy: 1), EveryChannel);

        RtspTransferException thrown = await Assert.ThrowsExactlyAsync<RtspTransferException>(
            async () => await filter.ConsumeAsync(Bytes("$\0\0\u0001a"), stopAtReply: false, CancellationToken.None));

        Assert.AreEqual(CurlExitCode.WriteError, thrown.ExitCode);
        Assert.AreEqual("Failed writing RTP data", thrown.Message);
        Assert.AreEqual(0L, filter.BytesHanded);
    }

    [TestMethod]
    [DataRow("", true)]
    [DataRow("RT", true)]
    [DataRow("RTSP/1.0", true)]
    [DataRow("RTSX", false)]
    [DataRow("$", false)]
    public void CouldStartReply_Prefix_MatchesRtspAsFarAsItGoes(string received, bool expected) =>
        Assert.AreEqual(expected, RtspInterleavedFrameFilter.CouldStartReply(Bytes(received)));

    [TestMethod]
    public async Task StreamSinkWriteAsync_Frame_WritesItAndTakesItAll()
    {
        using var output = new MemoryStream();

        long taken = await new RtspStreamInterleaveSink(output).WriteAsync(Bytes("$\0\0\u0001a"), CancellationToken.None);

        Assert.AreEqual(5L, taken);
        CollectionAssert.AreEqual(Bytes("$\0\0\u0001a"), output.ToArray());
    }

    [TestMethod]
    public async Task StreamSinkWriteAsync_WriteFails_TakesNothing()
    {
        long taken = await new RtspStreamInterleaveSink(new FailingStream(new IOException("full"))).WriteAsync(Bytes("$\0\0\0"), CancellationToken.None);

        Assert.AreEqual(0L, taken);
    }

    [TestMethod]
    public void StreamSink_NullOutput_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new RtspStreamInterleaveSink(null!));

    private static bool EveryChannel(byte channel) => true;

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);
}
