using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins the upgrade request head against curl 8.21.0, measured on 2026-09-28 with
/// <c>Record-CurlExchange.ps1</c> (ADR-0128, rows 1, 3, 4 and 24).
/// </summary>
[TestClass]
public sealed class WsUpgradeRequestFormatterTests
{
    private const string Key = FixedRandomSource.Key;

    private const string UpgradeHeaders =
        "Upgrade: websocket\r\n" +
        "Sec-WebSocket-Version: 13\r\n" +
        "Sec-WebSocket-Key: " + Key + "\r\n";

    [TestMethod]
    public void Format_PlainUrl_WritesCurlsUpgradeRequest()
    {
        string head = Format("ws://127.0.0.1:47901/chat", new HttpRequestOptions());

        Assert.AreEqual(
            "GET /chat HTTP/1.1\r\n" +
            "Host: 127.0.0.1:47901\r\n" +
            "User-Agent: curl/8.21.0\r\n" +
            "Accept: */*\r\n" +
            UpgradeHeaders +
            "Connection: Upgrade\r\n" +
            "\r\n",
            head);
    }

    [TestMethod]
    public void Format_AuthorizationAndRange_PutsRangeAfterAuthorizationAndBeforeUserAgent()
    {
        string head = Encoding.Latin1.GetString(WsUpgradeRequestFormatter.Format(CurlUrl.Parse("ws://127.0.0.1:47901/"), new HttpRequestOptions(), "GET", Key, "Basic dXNlcjpwdw==", "1-2"));

        StringAssert.StartsWith(head, "GET / HTTP/1.1\r\nHost: 127.0.0.1:47901\r\nAuthorization: Basic dXNlcjpwdw==\r\nRange: bytes=1-2\r\nUser-Agent: curl/8.21.0\r\n");
    }

    [TestMethod]
    [DataRow(null, null, null)]
    [DataRow(0L, null, null)]
    [DataRow(0L, "1-2", "1-2")]
    [DataRow(null, "abc", "abc")]
    [DataRow(5L, null, "5-")]
    [DataRow(5L, "1-2", "5-")]
    public void RangeValue_ResumeOffsetAndRangeText_PicksWhatCurlsSetupRangeDoes(long? resumeFrom, string? rangeText, string? expected)
    {
        Assert.AreEqual(expected, WsUpgradeRequestFormatter.RangeValue(resumeFrom, rangeText));
    }

    [TestMethod]
    public void Format_HeaderAgentAndAuthorization_PlacesEachWhereCurlDoes()
    {
        var options = new HttpRequestOptions { Headers = ["X-Test: 1"], UserAgent = "agent/1" };

        string head = Format("ws://127.0.0.1:47901/p?q=1", options, authorization: "Basic dXNlcjpwdw==");

        Assert.AreEqual(
            "GET /p?q=1 HTTP/1.1\r\n" +
            "Host: 127.0.0.1:47901\r\n" +
            "Authorization: Basic dXNlcjpwdw==\r\n" +
            "User-Agent: agent/1\r\n" +
            "Accept: */*\r\n" +
            UpgradeHeaders +
            "X-Test: 1\r\n" +
            "Connection: Upgrade\r\n" +
            "\r\n",
            head);
    }

    [TestMethod]
    public void Format_RefererMethodAndConnectionHeader_AppendsUpgradeToTheConnectionValue()
    {
        var options = new HttpRequestOptions { Headers = ["Connection: keep-alive"], Referer = "http://ref/" };

        string head = Format("ws://127.0.0.1:47901/", options, method: "POST");

        Assert.AreEqual(
            "POST / HTTP/1.1\r\n" +
            "Host: 127.0.0.1:47901\r\n" +
            "User-Agent: curl/8.21.0\r\n" +
            "Accept: */*\r\n" +
            "Referer: http://ref/\r\n" +
            UpgradeHeaders +
            "Connection: keep-alive, Upgrade\r\n" +
            "\r\n",
            head);
    }

    [TestMethod]
    public void Format_DefaultPort_LeavesThePortOutOfHost()
    {
        StringAssert.StartsWith(Format("ws://example.invalid/", new HttpRequestOptions()), "GET / HTTP/1.1\r\nHost: example.invalid\r\n");
    }

    [TestMethod]
    public void Format_WssDefaultPort_LeavesThePortOutOfHost()
    {
        StringAssert.StartsWith(Format("wss://example.invalid:443/", new HttpRequestOptions()), "GET / HTTP/1.1\r\nHost: example.invalid\r\n");
    }

    [TestMethod]
    public void Format_HostHeader_ReplacesCurlsHostInPlace()
    {
        string head = Format("ws://h:81/", new HttpRequestOptions { Headers = ["host: other"] });

        StringAssert.StartsWith(head, "GET / HTTP/1.1\r\nHost: other\r\nUser-Agent: curl/8.21.0\r\n");
        Assert.AreEqual(1, CountOf(head, "other"));
    }

    [TestMethod]
    public void Format_EmptyHostHeader_RemovesHost()
    {
        StringAssert.StartsWith(Format("ws://h:81/", new HttpRequestOptions { Headers = ["Host:"] }), "GET / HTTP/1.1\r\nUser-Agent: curl/8.21.0\r\n");
    }

    [TestMethod]
    public void Format_HostHeaderOfBlanks_SendsItAsWritten()
    {
        StringAssert.StartsWith(Format("ws://h:81/", new HttpRequestOptions { Headers = ["Host:   "] }), "GET / HTTP/1.1\r\nHost:   \r\nUser-Agent: curl/8.21.0\r\n");
    }

    [TestMethod]
    public void Format_EmptyHeaderOfCurlsOwn_RemovesIt()
    {
        string head = Format("ws://h:81/", new HttpRequestOptions { Headers = ["Accept:", "Sec-WebSocket-Version:"] });

        Assert.IsFalse(head.Contains("Accept", StringComparison.Ordinal));
        Assert.IsFalse(head.Contains("Sec-WebSocket-Version", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Format_HeaderEndingInSemicolon_SendsItEmpty()
    {
        StringAssert.Contains(Format("ws://h:81/", new HttpRequestOptions { Headers = ["X-Empty;"] }), "Sec-WebSocket-Key: " + Key + "\r\nX-Empty:\r\nConnection: Upgrade\r\n");
    }

    [TestMethod]
    public void Format_UpgradeHeader_ReplacesCurlsUpgradeAfterTheKey()
    {
        string head = Format("ws://h:81/", new HttpRequestOptions { Headers = ["Upgrade: other"] });

        StringAssert.Contains(head, "Accept: */*\r\nSec-WebSocket-Version: 13\r\nSec-WebSocket-Key: " + Key + "\r\nUpgrade: other\r\n");
        Assert.IsFalse(head.Contains("websocket", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Format_EmptyConnectionHeader_KeepsCurlsConnectionUpgrade()
    {
        string head = Format("ws://h:81/", new HttpRequestOptions { Headers = ["Connection:"] });

        StringAssert.EndsWith(head, "Sec-WebSocket-Key: " + Key + "\r\nConnection: Upgrade\r\n\r\n");
    }

    [TestMethod]
    public void Format_TwoConnectionHeaders_KeepsTheFirstValue()
    {
        string head = Format("ws://h:81/", new HttpRequestOptions { Headers = ["Connection: a", "Connection: b"] });

        StringAssert.EndsWith(head, "Connection: a, Upgrade\r\n\r\n");
    }

    [TestMethod]
    public void Format_NonAsciiPathAndQuery_PercentEncodesThePathAndSendsTheQueryAsUtf8()
    {
        byte[] head = WsUpgradeRequestFormatter.Format(CurlUrl.Parse("ws://h:81/é?q=é"), new HttpRequestOptions(), "GET", Key, null);

        CollectionAssert.AreEqual(
            Encoding.ASCII.GetBytes("GET /%C3%A9?q=").Concat(new byte[] { 0xC3, 0xA9 }).Concat(Encoding.ASCII.GetBytes(" HTTP/1.1\r\n")).ToArray(),
            head.Take(27).ToArray());
    }

    [TestMethod]
    public void Format_AgentInTheCommandLineEncoding_SendsItsBytes()
    {
        var options = new HttpRequestOptions { UserAgent = "é", CommandLineTextEncoding = Encoding.UTF8 };

        StringAssert.Contains(Format("ws://h:81/", options), "User-Agent: Ã©\r\n");
    }

    [TestMethod]
    public void RequestTarget_UrlWithoutQuery_IsThePath()
    {
        Assert.AreEqual("/a/b", WsUpgradeRequestFormatter.RequestTarget(CurlUrl.Parse("ws://h/a/b")));
    }

    private static string Format(string url, HttpRequestOptions options, string method = "GET", string? authorization = null) =>
        Encoding.Latin1.GetString(WsUpgradeRequestFormatter.Format(CurlUrl.Parse(url), options, method, Key, authorization));

    private static int CountOf(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;
}
