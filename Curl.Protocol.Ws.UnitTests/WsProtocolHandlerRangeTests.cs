using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws.Fakes;
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

    [TestMethod]
    public async Task ExecuteAsync_RangeOneToTwo_SendsRangeBetweenHostAndUserAgent()
    {
        string sent = await SentRequestAsync(new TransferContext { Url = Url, Output = new MemoryStream(), RangeText = "1-2" });

        Assert.AreEqual(RequestWith("Range: bytes=1-2\r\n"), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContinueAtFive_SendsRangeFromFive()
    {
        string sent = await SentRequestAsync(new TransferContext { Url = Url, Output = new MemoryStream(), ResumeFrom = 5 });

        Assert.AreEqual(RequestWith("Range: bytes=5-\r\n"), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContinueAtZeroAlone_SendsNoRange()
    {
        string sent = await SentRequestAsync(new TransferContext { Url = Url, Output = new MemoryStream(), ResumeFrom = 0 });

        Assert.AreEqual(RequestWith(string.Empty), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContinueAtZeroWithRange_SendsTheRangeText()
    {
        string sent = await SentRequestAsync(new TransferContext { Url = Url, Output = new MemoryStream(), ResumeFrom = 0, RangeText = "1-2" });

        Assert.AreEqual(RequestWith("Range: bytes=1-2\r\n"), sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContinueAtFiveWithRange_SendsTheResumeRange()
    {
        string sent = await SentRequestAsync(new TransferContext { Url = Url, Output = new MemoryStream(), ResumeFrom = 5, RangeText = "1-2" });

        Assert.AreEqual(RequestWith("Range: bytes=5-\r\n"), sent);
    }

    [TestMethod]
    [DataRow("range: x")]
    [DataRow("RANGE: x")]
    [DataRow("Range: x")]
    public async Task ExecuteAsync_HeaderNamingRange_SendsItInItsOwnPlaceInsteadOfCurls(string header)
    {
        var context = new TransferContext { Url = Url, Output = new MemoryStream(), ResumeFrom = 5, RangeText = "1-2", Http = new HttpRequestOptions { Headers = [header] } };

        string sent = await SentRequestAsync(context);

        Assert.AreEqual(
            "GET / HTTP/1.1\r\n" +
            "Host: 127.0.0.1:47901\r\n" +
            UpgradeHeaders +
            header + "\r\n" +
            "Connection: Upgrade\r\n" +
            "\r\n",
            sent);
    }

    private static CurlUrl Url => CurlUrl.Parse("ws://127.0.0.1:47901/");

    private static string RequestWith(string rangeLine) =>
        "GET / HTTP/1.1\r\n" +
        "Host: 127.0.0.1:47901\r\n" +
        rangeLine +
        UpgradeHeaders +
        "Connection: Upgrade\r\n" +
        "\r\n";

    private static async Task<string> SentRequestAsync(TransferContext context)
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(Reply));
        var handler = new WsProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), new RecordingAuthenticator(), new FixedRandomSource());

        TransferResult result = await handler.ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        return Encoding.Latin1.GetString(connection.Sent);
    }
}
