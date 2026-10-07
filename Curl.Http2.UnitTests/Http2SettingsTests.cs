using Curl.Testing;
using static Curl.Http2.Http2Test;

namespace Curl.Http2;

/// <summary>
/// Checks <see cref="Http2Settings" /> starts at RFC 9113 section 6.5.2's initial values
/// and applies, or rejects, each parameter as that section says.
/// </summary>
[TestClass]
public sealed class Http2SettingsTests
{
    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void New_HoldsTheInitialValues()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("settings", "new Http2Settings()");

        var settings = new Http2Settings();
        diagnostics.Act("HeaderTableSize", settings.HeaderTableSize);
        diagnostics.Act("IsPushEnabled", settings.IsPushEnabled);
        diagnostics.Act("MaxConcurrentStreams", settings.MaxConcurrentStreams);
        diagnostics.Act("InitialWindowSize", settings.InitialWindowSize);
        diagnostics.Act("MaxFrameSize", settings.MaxFrameSize);
        diagnostics.Act("MaxHeaderListSize", settings.MaxHeaderListSize);

        diagnostics.Assert("HeaderTableSize", 4096u, settings.HeaderTableSize);
        diagnostics.Assert("IsPushEnabled", true, settings.IsPushEnabled);
        diagnostics.Assert("MaxConcurrentStreams", null, settings.MaxConcurrentStreams);
        diagnostics.Assert("InitialWindowSize", 65535, settings.InitialWindowSize);
        diagnostics.Assert("MaxFrameSize", 16384, settings.MaxFrameSize);
        diagnostics.Assert("MaxHeaderListSize", null, settings.MaxHeaderListSize);
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
        var diagnostics = TestDiagnostics.For(TestContext);
        var settings = new Http2Settings();
        diagnostics.Arrange("parameters applied", "HeaderTableSize=0, EnablePush=0, MaxConcurrentStreams=100, InitialWindowSize=int.MaxValue, MaxFrameSize=largest, MaxHeaderListSize=8192, unknown 0x99=1");

        settings.Apply(new(Http2SettingIdentifier.HeaderTableSize, 0));
        settings.Apply(new(Http2SettingIdentifier.EnablePush, 0));
        settings.Apply(new(Http2SettingIdentifier.MaxConcurrentStreams, 100));
        settings.Apply(new(Http2SettingIdentifier.InitialWindowSize, int.MaxValue));
        settings.Apply(new(Http2SettingIdentifier.MaxFrameSize, Http2FrameCodec.LargestMaximumFrameSize));
        settings.Apply(new(Http2SettingIdentifier.MaxHeaderListSize, 8192));
        settings.Apply(new((Http2SettingIdentifier)0x99, 1));
        diagnostics.Act("HeaderTableSize", settings.HeaderTableSize);
        diagnostics.Act("IsPushEnabled", settings.IsPushEnabled);
        diagnostics.Act("MaxConcurrentStreams", settings.MaxConcurrentStreams);
        diagnostics.Act("InitialWindowSize", settings.InitialWindowSize);
        diagnostics.Act("MaxFrameSize", settings.MaxFrameSize);
        diagnostics.Act("MaxHeaderListSize", settings.MaxHeaderListSize);

        diagnostics.Assert("HeaderTableSize", 0u, settings.HeaderTableSize);
        diagnostics.Assert("IsPushEnabled", false, settings.IsPushEnabled);
        diagnostics.Assert("MaxConcurrentStreams", 100u, settings.MaxConcurrentStreams);
        diagnostics.Assert("InitialWindowSize", int.MaxValue, settings.InitialWindowSize);
        diagnostics.Assert("MaxFrameSize", Http2FrameCodec.LargestMaximumFrameSize, settings.MaxFrameSize);
        diagnostics.Assert("MaxHeaderListSize", 8192u, settings.MaxHeaderListSize);
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
        var diagnostics = TestDiagnostics.For(TestContext);
        var settings = new Http2Settings();
        settings.Apply(new(Http2SettingIdentifier.EnablePush, 0));
        diagnostics.Arrange("IsPushEnabled after EnablePush=0", settings.IsPushEnabled);

        settings.Apply(new(Http2SettingIdentifier.EnablePush, 1));
        diagnostics.Act("IsPushEnabled after EnablePush=1", settings.IsPushEnabled);

        diagnostics.Assert("IsPushEnabled", true, settings.IsPushEnabled);
        Assert.IsTrue(settings.IsPushEnabled);
    }

    [TestMethod]
    public void Apply_EnablePushTwo_IsAProtocolError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("setting", "EnablePush=2");

        var error = ErrorOf(() => new Http2Settings().Apply(new(Http2SettingIdentifier.EnablePush, 2)));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.ProtocolError, error);
        Assert.AreEqual(Http2ErrorCode.ProtocolError, error);
    }

    [TestMethod]
    public void Apply_InitialWindowSizeOverTheMaximum_IsAFlowControlError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("setting", "InitialWindowSize=0x80000000");

        var error = ErrorOf(() => new Http2Settings().Apply(new(Http2SettingIdentifier.InitialWindowSize, 0x80000000)));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.FlowControlError, error);
        Assert.AreEqual(Http2ErrorCode.FlowControlError, error);
    }

    [TestMethod]
    [DataRow(16383u)]
    [DataRow(16777216u)]
    public void Apply_MaxFrameSizeOutOfRange_IsAProtocolError(uint value)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("setting MaxFrameSize", value);

        var error = ErrorOf(() => new Http2Settings().Apply(new(Http2SettingIdentifier.MaxFrameSize, value)));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.ProtocolError, error);
        Assert.AreEqual(Http2ErrorCode.ProtocolError, error);
    }

    [TestMethod]
    public void Apply_SmallestMaxFrameSize_IsAccepted()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var settings = new Http2Settings();
        diagnostics.Arrange("setting MaxFrameSize", 16384);

        settings.Apply(new(Http2SettingIdentifier.MaxFrameSize, 16384));
        diagnostics.Act("MaxFrameSize", settings.MaxFrameSize);

        diagnostics.Assert("MaxFrameSize", 16384, settings.MaxFrameSize);
        Assert.AreEqual(16384, settings.MaxFrameSize);
    }
}
