using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins what <see cref="Http2Session" /> gives an h2c upgrade request, and that its closing
/// GOAWAY tolerates a connection already gone.
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

    [TestMethod]
    public async Task ShutDownAsync_ConnectionThatFailsTheGoAway_CompletesWithoutThrowing()
    {
        FailingSendConnection connection = new(new IOException("reset"), writesBeforeFailure: null);
        Http2Session session = new(connection);
        _ = await session.StartStreamAsync([new(":method", "GET")], isEndStream: true, CancellationToken.None);

        await session.ShutDownAsync(CancellationToken.None);

        Assert.IsTrue(session.Frames.IsGoAwaySent, "the GOAWAY was written; the flush after it failed");
    }
}
