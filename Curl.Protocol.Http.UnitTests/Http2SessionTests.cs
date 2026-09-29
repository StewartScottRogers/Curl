namespace Curl.Protocol.Http;

/// <summary>
/// Pins what <see cref="Http2Session" /> gives an h2c upgrade request.
/// </summary>
[TestClass]
public sealed class Http2SessionTests
{
    [TestMethod]
    public void UpgradeSettings_IsTheMeasuredHttp2SettingsValue()
    {
        // curl --http2 -v http://127.0.0.1:48716/ sent HTTP2-Settings: AAMAAABkAAQAAQAAAAIAAAAA (BL-716 Notes).
        Assert.AreEqual("AAMAAABkAAQAAQAAAAIAAAAA", Http2Session.UpgradeSettings);
    }
}
