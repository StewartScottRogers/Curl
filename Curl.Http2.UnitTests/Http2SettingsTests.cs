using static Curl.Http2.Http2Test;

namespace Curl.Http2;

/// <summary>
/// Checks <see cref="Http2Settings" /> starts at RFC 9113 section 6.5.2's initial values
/// and applies, or rejects, each parameter as that section says.
/// </summary>
[TestClass]
public sealed class Http2SettingsTests
{
    [TestMethod]
    public void New_HoldsTheInitialValues()
    {
        var settings = new Http2Settings();

        Assert.AreEqual(4096u, settings.HeaderTableSize);
        Assert.IsTrue(settings.IsPushEnabled);
        Assert.IsNull(settings.MaxConcurrentStreams);
        Assert.AreEqual(65535, settings.InitialWindowSize);
        Assert.AreEqual(16384, settings.MaxFrameSize);
        Assert.IsNull(settings.MaxHeaderListSize);
    }

    [TestMethod]
    public void Apply_EveryParameter_SetsIt()
    {
        var settings = new Http2Settings();

        settings.Apply(new(Http2SettingIdentifier.HeaderTableSize, 0));
        settings.Apply(new(Http2SettingIdentifier.EnablePush, 0));
        settings.Apply(new(Http2SettingIdentifier.MaxConcurrentStreams, 100));
        settings.Apply(new(Http2SettingIdentifier.InitialWindowSize, int.MaxValue));
        settings.Apply(new(Http2SettingIdentifier.MaxFrameSize, Http2FrameCodec.LargestMaximumFrameSize));
        settings.Apply(new(Http2SettingIdentifier.MaxHeaderListSize, 8192));
        settings.Apply(new((Http2SettingIdentifier)0x99, 1));

        Assert.AreEqual(0u, settings.HeaderTableSize);
        Assert.IsFalse(settings.IsPushEnabled);
        Assert.AreEqual(100u, settings.MaxConcurrentStreams);
        Assert.AreEqual(int.MaxValue, settings.InitialWindowSize);
        Assert.AreEqual(Http2FrameCodec.LargestMaximumFrameSize, settings.MaxFrameSize);
        Assert.AreEqual(8192u, settings.MaxHeaderListSize);
    }

    [TestMethod]
    public void Apply_EnablePushOne_EnablesPush()
    {
        var settings = new Http2Settings();
        settings.Apply(new(Http2SettingIdentifier.EnablePush, 0));

        settings.Apply(new(Http2SettingIdentifier.EnablePush, 1));

        Assert.IsTrue(settings.IsPushEnabled);
    }

    [TestMethod]
    public void Apply_EnablePushTwo_IsAProtocolError() =>
        Assert.AreEqual(Http2ErrorCode.ProtocolError, ErrorOf(() => new Http2Settings().Apply(new(Http2SettingIdentifier.EnablePush, 2))));

    [TestMethod]
    public void Apply_InitialWindowSizeOverTheMaximum_IsAFlowControlError() =>
        Assert.AreEqual(Http2ErrorCode.FlowControlError, ErrorOf(() => new Http2Settings().Apply(new(Http2SettingIdentifier.InitialWindowSize, 0x80000000))));

    [TestMethod]
    [DataRow(16383u)]
    [DataRow(16777216u)]
    public void Apply_MaxFrameSizeOutOfRange_IsAProtocolError(uint value) =>
        Assert.AreEqual(Http2ErrorCode.ProtocolError, ErrorOf(() => new Http2Settings().Apply(new(Http2SettingIdentifier.MaxFrameSize, value))));

    [TestMethod]
    public void Apply_SmallestMaxFrameSize_IsAccepted()
    {
        var settings = new Http2Settings();

        settings.Apply(new(Http2SettingIdentifier.MaxFrameSize, 16384));

        Assert.AreEqual(16384, settings.MaxFrameSize);
    }
}
