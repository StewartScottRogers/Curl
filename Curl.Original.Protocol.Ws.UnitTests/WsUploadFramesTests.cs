using Curl.Original.Protocol.Ws.Fakes;

namespace Curl.Original.Protocol.Ws;

[TestClass]
public sealed class WsUploadFramesTests
{
    [TestMethod]
    public void CanStartFrame_BeforeOpenAndAfterClose_IsFalse()
    {
        WsUploadFrames frames = new();
        bool before = frames.CanStartFrame;
        frames.Open();
        bool open = frames.CanStartFrame;
        frames.Close();

        Assert.IsFalse(before);
        Assert.IsTrue(open);
        Assert.IsFalse(frames.CanStartFrame);
    }

    [TestMethod]
    public void CanStartFrame_WhileAStartedFrameHasPayloadLeft_IsFalse()
    {
        WsUploadFrames frames = new();
        frames.Open();

        frames.StartFrame(WsOpcode.Binary, isFinal: true, 2, new FixedRandomSource());

        Assert.IsFalse(frames.CanStartFrame);
        Assert.AreEqual(2, frames.ReadLength(10));
    }

    [TestMethod]
    public void ContinuesMessage_AfterAnUnfinishedTextFrameAndAPing_IsTrueUntilAFinalFrame()
    {
        WsUploadFrames frames = new();
        frames.Open();

        frames.StartFrame(WsOpcode.Text, isFinal: false, 0, new FixedRandomSource());
        frames.StartFrame(WsOpcode.Ping, isFinal: true, 0, new FixedRandomSource());
        bool afterPing = frames.ContinuesMessage;
        frames.StartFrame(WsOpcode.Continuation, isFinal: true, 0, new FixedRandomSource());

        Assert.IsTrue(afterPing);
        Assert.IsFalse(frames.ContinuesMessage);
    }

    [TestMethod]
    public void Encode_TwoHeadsStartedBeforeARead_SendsBothHeadsBeforeThePayload()
    {
        WsUploadFrames frames = new();
        frames.Open();
        frames.StartFrame(WsOpcode.Ping, isFinal: true, 0, new FixedRandomSource());
        frames.StartFrame(WsOpcode.Text, isFinal: true, 1, new FixedRandomSource());

        byte[] bytes = frames.Encode("a"u8, new FixedRandomSource(), out bool startsBinaryFrame);

        byte[] expected = [.. WsFrameEncoder.Encode(WsOpcode.Ping, true, [], new FixedRandomSource()), .. WsFrameEncoder.Encode(WsOpcode.Text, true, "a"u8, new FixedRandomSource())];
        CollectionAssert.AreEqual(expected, bytes);
        Assert.IsFalse(startsBinaryFrame);
    }

    [TestMethod]
    public void Encode_NothingReadAndNothingStarted_IsEmpty()
    {
        WsUploadFrames frames = new();
        frames.Open();

        byte[] bytes = frames.Encode([], new FixedRandomSource(), out bool startsBinaryFrame);

        Assert.IsEmpty(bytes);
        Assert.IsFalse(startsBinaryFrame);
    }
}
