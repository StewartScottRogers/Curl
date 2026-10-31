using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-278: after an h2c upgrade whose stream 1 the server resets, curl 8.18.0 exits 92
/// and <c>-w</c> still reports the <c>101</c> interim response (<c>1.1 101</c>, two headers),
/// and the connection is closed with a GOAWAY.
/// </summary>
[TestClass]
public sealed class Cgr278Tests
{
    private const string SwitchingProtocolsHead = "HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: h2c\r\n\r\n";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_Http2UpgradeStreamReset_ReportsThe101AndClosesWithGoAway()
    {
        byte[] response =
        [
            .. Encoding.Latin1.GetBytes(SwitchingProtocolsHead),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateRstStream(1, Http2ErrorCode.InternalError)),
        ];
        ScriptedConnection connection = new(response, 65536);
        HttpProtocolHandler handler = new(QueueConnector.For(connection), new SilentAuthenticator());
        Diagnostics.Arrange("response", "101 h2c, SETTINGS, RST_STREAM INTERNAL_ERROR on stream 1");

        TransferResult result = await handler.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("http://127.0.0.1:48717/x"),
            Output = new MemoryStream(),
            Events = new RecordingTransferEvents(),
            Http = new HttpRequestOptions { Version = HttpVersionPreference.Http2 },
        });

        Diagnostics.Assert("exit code", CurlExitCode.Http2Stream, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Http2Stream, result.ExitCode);
        Assert.AreEqual(101, result.Report!.ResponseCode);
        Assert.AreEqual(new Version(1, 1), result.Report!.HttpVersion);
        Assert.AreEqual(2, result.Report!.ResponseHeadersStored);
        byte[] goAway = [0, 0, 17, 7, 0, 0, 0, 0, 0];
        Assert.IsTrue(connection.Written.AsSpan().IndexOf(goAway) >= 0, "The connection closes with a GOAWAY.");
    }
}
