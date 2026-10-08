using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins the <c>Range</c> line <c>-r</c> and <c>-C</c> put on the upgrade request, measured on
/// 2026-10-02 against curl 8.21.0 with <c>Record-CurlExchange.ps1</c> (BL-1295): between
/// <c>Host</c> and <c>User-Agent</c>, a non-zero <c>-C</c> winning over <c>-r</c>, and a
/// <c>-H</c> header naming <c>Range</c> taking its place.
/// </summary>
[TestClass]
public sealed class WsProtocolHandlerRangeTests
{
    private const string Reply =
        "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: xxx\r\n\r\n\x81\x05hello\x88\x00";

    private const string UpgradeHeaders =
        "User-Agent: curl/8.21.0\r\n" +
        "Accept: */*\r\n" +
        "Upgrade: websocket\r\n" +
        "Sec-WebSocket-Version: 13\r\n" +
        "Sec-WebSocket-Key: " + FixedRandomSource.Key + "\r\n";

    public TestContext TestContext { get; set; } = null!;

    private static CurlUrl Url => CurlUrl.Parse("ws://127.0.0.1:47901/");

    [TestMethod]
    public async Task ExecuteAsync_RangeOneToTwo_SendsRangeBetweenHostAndUserAgent()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("range text", "1-2");
        diagnostics.Arrange("resume from", null);

        string sent = await SentRequestAsync(new TransferContext { Url = Url, Output = new MemoryStream(), RangeText = "1-2" }, diagnostics);

        string expected = RequestWith("Range: bytes=1-2\r\n");
        diagnostics.Diff("upgrade request", expected, sent);
        Assert.AreEqual(RequestWith("Range: bytes=1-2\r\n"), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContinueAtFive_SendsRangeFromFive()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("range text", null);
        diagnostics.Arrange("resume from", 5);

        string sent = await SentRequestAsync(new TransferContext { Url = Url, Output = new MemoryStream(), ResumeFrom = 5 }, diagnostics);

        string expected = RequestWith("Range: bytes=5-\r\n");
        diagnostics.Diff("upgrade request", expected, sent);
        Assert.AreEqual(RequestWith("Range: bytes=5-\r\n"), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContinueAtZeroAlone_SendsNoRange()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("range text", null);
        diagnostics.Arrange("resume from", 0);

        string sent = await SentRequestAsync(new TransferContext { Url = Url, Output = new MemoryStream(), ResumeFrom = 0 }, diagnostics);

        string expected = RequestWith(string.Empty);
        diagnostics.Diff("upgrade request", expected, sent);
        Assert.AreEqual(RequestWith(string.Empty), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContinueAtZeroWithRange_SendsTheRangeText()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("range text", "1-2");
        diagnostics.Arrange("resume from", 0);

        string sent = await SentRequestAsync(new TransferContext { Url = Url, Output = new MemoryStream(), ResumeFrom = 0, RangeText = "1-2" }, diagnostics);

        string expected = RequestWith("Range: bytes=1-2\r\n");
        diagnostics.Diff("upgrade request", expected, sent);
        Assert.AreEqual(RequestWith("Range: bytes=1-2\r\n"), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContinueAtFiveWithRange_SendsTheResumeRange()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("range text", "1-2");
        diagnostics.Arrange("resume from", 5);

        string sent = await SentRequestAsync(new TransferContext { Url = Url, Output = new MemoryStream(), ResumeFrom = 5, RangeText = "1-2" }, diagnostics);

        string expected = RequestWith("Range: bytes=5-\r\n");
        diagnostics.Diff("upgrade request", expected, sent);
        Assert.AreEqual(RequestWith("Range: bytes=5-\r\n"), sent);
    }

    [TestMethod]
    [DataRow("range: x")]
    [DataRow("RANGE: x")]
    [DataRow("Range: x")]
    public async Task ExecuteAsync_HeaderNamingRange_SendsItInItsOwnPlaceInsteadOfCurls(string header)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var context = new TransferContext { Url = Url, Output = new MemoryStream(), ResumeFrom = 5, RangeText = "1-2", Http = new HttpRequestOptions { Headers = [header] } };
        diagnostics.Arrange("header", header);
        diagnostics.Arrange("range text", "1-2");
        diagnostics.Arrange("resume from", 5);

        string sent = await SentRequestAsync(context, diagnostics);

        string expected =
            "GET / HTTP/1.1\r\n" +
            "Host: 127.0.0.1:47901\r\n" +
            UpgradeHeaders +
            header + "\r\n" +
            "Connection: Upgrade\r\n" +
            "\r\n";
        diagnostics.Diff("upgrade request", expected, sent);
        Assert.AreEqual(
            "GET / HTTP/1.1\r\n" +
            "Host: 127.0.0.1:47901\r\n" +
            UpgradeHeaders +
            header + "\r\n" +
            "Connection: Upgrade\r\n" +
            "\r\n",
            sent);
    }

    private static string RequestWith(string rangeLine) =>
        "GET / HTTP/1.1\r\n" +
        "Host: 127.0.0.1:47901\r\n" +
        rangeLine +
        UpgradeHeaders +
        "Connection: Upgrade\r\n" +
        "\r\n";

    private static async Task<string> SentRequestAsync(TransferContext context, TestDiagnostics diagnostics)
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(Reply));
        var handler = new WsProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), new RecordingAuthenticator(), new FixedRandomSource());
        diagnostics.Bytes("scripted reply", Encoding.Latin1.GetBytes(Reply));

        TransferResult result;
        using (diagnostics.Phase("upgrade"))
        {
            result = await handler.ExecuteAsync(context);
        }

        diagnostics.Act("exit code", $"{result.ExitCode} ({result.ErrorMessage})");
        diagnostics.Bytes("upgrade request sent", connection.Sent);
        diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        return Encoding.Latin1.GetString(connection.Sent);
    }
}
