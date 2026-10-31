using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Third-party gap report issue CGR-277 (BL-3806): on an h2c-upgraded connection curl sends
/// the later streams WINDOW_UPDATE 10420224, not the 10420225 of the upgraded stream 1.
/// </summary>
[TestClass]
public sealed class Cgr277Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task StartStreamAsync_AfterTheUpgradedStream_SendsWindowUpdateIncrement10420224()
    {
        ScriptedConnection connection = new([], 65536);
        Http2Session session = new(connection);
        Http2StreamConnection upgraded = (Http2StreamConnection)session.CreateStream("http", 0, ignoresBody: false);
        await upgraded.StartUpgradedAsync(isHead: false, CancellationToken.None);
        Diagnostics.Arrange("session", "stream 1 opened by the h2c upgrade, settings not yet acknowledged");
        Http2StreamConnection second = (Http2StreamConnection)session.CreateStream("http", 0, ignoresBody: false);

        await second.WriteAsync(System.Text.Encoding.Latin1.GetBytes("GET /2 HTTP/1.1\r\nHost: h\r\n\r\n"), CancellationToken.None);

        string written = Convert.ToHexString(connection.Written);
        const string StreamThreeWindowUpdate = "000004080000000003009F0000";
        Diagnostics.Act("bytes written (hex)", written);
        Diagnostics.Assert("stream 3 WINDOW_UPDATE increment 10420224 present", true, written.Contains(StreamThreeWindowUpdate, StringComparison.Ordinal));
        Assert.IsTrue(written.Contains(StreamThreeWindowUpdate, StringComparison.Ordinal));
    }
}
