using Curl.Api.Options.Protocols;

namespace Curl.Api.UnitTests.Options.Protocols;

[TestClass]
public sealed class EasyWebSocketOptionsTests
{
    private static readonly EasyWebSocketOptions Defaults = new();

    [TestMethod]
    public void New_Options_SetsNoBit()
    {
        Assert.AreEqual(WebSocketOptionBits.None, Defaults.Options);
    }

    [TestMethod]
    public void WebSocketOptionBits_CarryLibcurlBits()
    {
        Assert.AreEqual(0, (int)WebSocketOptionBits.None);
        Assert.AreEqual(1, (int)WebSocketOptionBits.RawMode);
        Assert.AreEqual(2, (int)WebSocketOptionBits.NoAutoPong);
    }

    [TestMethod]
    public void With_Options_ChangesOnlyThatOption()
    {
        OptionRecordAssert.ChangesOnly(
            Defaults,
            Defaults with { Options = WebSocketOptionBits.RawMode | WebSocketOptionBits.NoAutoPong },
            o => o with { Options = Defaults.Options });
        Assert.HasCount(1, typeof(EasyWebSocketOptions).GetProperties());
    }
}
