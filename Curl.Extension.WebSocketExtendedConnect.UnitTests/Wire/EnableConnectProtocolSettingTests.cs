using Curl.Extension.WebSocketExtendedConnect.Wire;
using Curl.Http2;
using Curl.Http3;

namespace Curl.Extension.WebSocketExtendedConnect.UnitTests.Wire;

[TestClass]
public sealed class EnableConnectProtocolSettingTests
{
    [TestMethod]
    public void IsEnabled_Http2Advertised_IsTrue()
    {
        Http2Setting[] settings = [new(Http2SettingIdentifier.MaxConcurrentStreams, 100), new((Http2SettingIdentifier)0x8, 1)];

        Assert.IsTrue(EnableConnectProtocolSetting.IsEnabled(settings));
    }

    [TestMethod]
    public void IsEnabled_Http2Absent_IsFalse()
    {
        Http2Setting[] settings = [new(Http2SettingIdentifier.MaxConcurrentStreams, 100)];

        Assert.IsFalse(EnableConnectProtocolSetting.IsEnabled(settings));
    }

    [TestMethod]
    public void IsEnabled_Http2ValueZero_IsFalse()
    {
        Http2Setting[] settings = [new((Http2SettingIdentifier)0x8, 0)];

        Assert.IsFalse(EnableConnectProtocolSetting.IsEnabled(settings));
    }

    [TestMethod]
    public void IsEnabled_Http2Repeated_TheLastCounts()
    {
        Http2Setting[] settings = [new((Http2SettingIdentifier)0x8, 1), new((Http2SettingIdentifier)0x8, 0)];

        Assert.IsFalse(EnableConnectProtocolSetting.IsEnabled(settings));
    }

    [TestMethod]
    public void IsEnabled_Http3Advertised_IsTrue()
    {
        Http3Setting[] settings = [new(0x06, 4096), new(0x08, 1)];

        Assert.IsTrue(EnableConnectProtocolSetting.IsEnabled(settings));
    }

    [TestMethod]
    public void IsEnabled_Http3Absent_IsFalse()
    {
        Http3Setting[] settings = [new(0x06, 4096)];

        Assert.IsFalse(EnableConnectProtocolSetting.IsEnabled(settings));
    }

    [TestMethod]
    public void IsEnabled_Http3ValueZero_IsFalse()
    {
        Http3Setting[] settings = [new(0x08, 0)];

        Assert.IsFalse(EnableConnectProtocolSetting.IsEnabled(settings));
    }
}
