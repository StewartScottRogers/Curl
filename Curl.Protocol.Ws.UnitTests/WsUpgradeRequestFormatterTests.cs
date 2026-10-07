using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws.Fakes;
using Curl.Testing;
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

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Format_PlainUrl_WritesCurlsUpgradeRequest()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const string url = "ws://127.0.0.1:47901/chat";
        diagnostics.Arrange("url", url);
        diagnostics.Arrange("method", "GET");

        string head = Format(url, new HttpRequestOptions());

        diagnostics.Act("upgrade request head", head);
        string expected =
            "GET /chat HTTP/1.1\r\n" +
            "Host: 127.0.0.1:47901\r\n" +
            "User-Agent: curl/8.21.0\r\n" +
            "Accept: */*\r\n" +
            UpgradeHeaders +
            "Connection: Upgrade\r\n" +
            "\r\n";
        diagnostics.Diff("upgrade request head", expected, head);
        Assert.AreEqual(expected, head);
    }

    [TestMethod]
    public void Format_AuthorizationAndRange_PutsRangeAfterAuthorizationAndBeforeUserAgent()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "ws://127.0.0.1:47901/");
        diagnostics.Arrange("authorization", "Basic dXNlcjpwdw==");
        diagnostics.Arrange("range", "1-2");

        string head = Encoding.Latin1.GetString(WsUpgradeRequestFormatter.Format(CurlUrl.Parse("ws://127.0.0.1:47901/"), new HttpRequestOptions(), "GET", Key, "Basic dXNlcjpwdw==", "1-2"));

        diagnostics.Act("upgrade request head", head);
        const string expectedStart = "GET / HTTP/1.1\r\nHost: 127.0.0.1:47901\r\nAuthorization: Basic dXNlcjpwdw==\r\nRange: bytes=1-2\r\nUser-Agent: curl/8.21.0\r\n";
        diagnostics.Assert("head starts with", expectedStart, head);
        StringAssert.StartsWith(head, expectedStart);
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
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("resumeFrom", resumeFrom);
        diagnostics.Arrange("rangeText", rangeText);

        string? actual = WsUpgradeRequestFormatter.RangeValue(resumeFrom, rangeText);

        diagnostics.Act("range value", actual);
        diagnostics.Assert("range value", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Format_HeaderAgentAndAuthorization_PlacesEachWhereCurlDoes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var options = new HttpRequestOptions { Headers = ["X-Test: 1"], UserAgent = "agent/1" };
        diagnostics.Arrange("headers", "X-Test: 1");
        diagnostics.Arrange("user agent", "agent/1");
        diagnostics.Arrange("authorization", "Basic dXNlcjpwdw==");

        string head = Format("ws://127.0.0.1:47901/p?q=1", options, authorization: "Basic dXNlcjpwdw==");

        diagnostics.Act("upgrade request head", head);
        string expected =
            "GET /p?q=1 HTTP/1.1\r\n" +
            "Host: 127.0.0.1:47901\r\n" +
            "Authorization: Basic dXNlcjpwdw==\r\n" +
            "User-Agent: agent/1\r\n" +
            "Accept: */*\r\n" +
            UpgradeHeaders +
            "X-Test: 1\r\n" +
            "Connection: Upgrade\r\n" +
            "\r\n";
        diagnostics.Diff("upgrade request head", expected, head);
        Assert.AreEqual(expected, head);
    }

    [TestMethod]
    public void Format_RefererMethodAndConnectionHeader_AppendsUpgradeToTheConnectionValue()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var options = new HttpRequestOptions { Headers = ["Connection: keep-alive"], Referer = "http://ref/" };
        diagnostics.Arrange("headers", "Connection: keep-alive");
        diagnostics.Arrange("referer", "http://ref/");
        diagnostics.Arrange("method", "POST");

        string head = Format("ws://127.0.0.1:47901/", options, method: "POST");

        diagnostics.Act("upgrade request head", head);
        string expected =
            "POST / HTTP/1.1\r\n" +
            "Host: 127.0.0.1:47901\r\n" +
            "User-Agent: curl/8.21.0\r\n" +
            "Accept: */*\r\n" +
            "Referer: http://ref/\r\n" +
            UpgradeHeaders +
            "Connection: keep-alive, Upgrade\r\n" +
            "\r\n";
        diagnostics.Diff("upgrade request head", expected, head);
        Assert.AreEqual(expected, head);
    }

    [TestMethod]
    public void Format_DefaultPort_LeavesThePortOutOfHost()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "ws://example.invalid/");

        string head = Format("ws://example.invalid/", new HttpRequestOptions());

        diagnostics.Act("upgrade request head", head);
        const string expectedStart = "GET / HTTP/1.1\r\nHost: example.invalid\r\n";
        diagnostics.Assert("head starts with", expectedStart, head);
        StringAssert.StartsWith(head, expectedStart);
    }

    [TestMethod]
    public void Format_WssDefaultPort_LeavesThePortOutOfHost()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "wss://example.invalid:443/");

        string head = Format("wss://example.invalid:443/", new HttpRequestOptions());

        diagnostics.Act("upgrade request head", head);
        const string expectedStart = "GET / HTTP/1.1\r\nHost: example.invalid\r\n";
        diagnostics.Assert("head starts with", expectedStart, head);
        StringAssert.StartsWith(head, expectedStart);
    }

    [TestMethod]
    public void Format_HostHeader_ReplacesCurlsHostInPlace()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "ws://h:81/");
        diagnostics.Arrange("headers", "host: other");

        string head = Format("ws://h:81/", new HttpRequestOptions { Headers = ["host: other"] });

        diagnostics.Act("upgrade request head", head);
        const string expectedStart = "GET / HTTP/1.1\r\nHost: other\r\nUser-Agent: curl/8.21.0\r\n";
        diagnostics.Assert("head starts with", expectedStart, head);
        StringAssert.StartsWith(head, expectedStart);
        int count = CountOf(head, "other");
        diagnostics.Assert("occurrences of other", 1, count);
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public void Format_EmptyHostHeader_RemovesHost()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "ws://h:81/");
        diagnostics.Arrange("headers", "Host:");

        string head = Format("ws://h:81/", new HttpRequestOptions { Headers = ["Host:"] });

        diagnostics.Act("upgrade request head", head);
        const string expectedStart = "GET / HTTP/1.1\r\nUser-Agent: curl/8.21.0\r\n";
        diagnostics.Assert("head starts with", expectedStart, head);
        StringAssert.StartsWith(head, expectedStart);
    }

    [TestMethod]
    public void Format_HostHeaderOfBlanks_SendsItAsWritten()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "ws://h:81/");
        diagnostics.Arrange("headers", "Host:   ");

        string head = Format("ws://h:81/", new HttpRequestOptions { Headers = ["Host:   "] });

        diagnostics.Act("upgrade request head", head);
        const string expectedStart = "GET / HTTP/1.1\r\nHost:   \r\nUser-Agent: curl/8.21.0\r\n";
        diagnostics.Assert("head starts with", expectedStart, head);
        StringAssert.StartsWith(head, expectedStart);
    }

    [TestMethod]
    public void Format_EmptyHeaderOfCurlsOwn_RemovesIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "ws://h:81/");
        diagnostics.Arrange("headers", "Accept:, Sec-WebSocket-Version:");

        string head = Format("ws://h:81/", new HttpRequestOptions { Headers = ["Accept:", "Sec-WebSocket-Version:"] });

        diagnostics.Act("upgrade request head", head);
        bool hasAccept = head.Contains("Accept", StringComparison.Ordinal);
        bool hasVersion = head.Contains("Sec-WebSocket-Version", StringComparison.Ordinal);
        diagnostics.Assert("head contains Accept", false, hasAccept);
        diagnostics.Assert("head contains Sec-WebSocket-Version", false, hasVersion);
        Assert.IsFalse(head.Contains("Accept", StringComparison.Ordinal));
        Assert.IsFalse(head.Contains("Sec-WebSocket-Version", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Format_HeaderEndingInSemicolon_SendsItEmpty()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "ws://h:81/");
        diagnostics.Arrange("headers", "X-Empty;");

        string head = Format("ws://h:81/", new HttpRequestOptions { Headers = ["X-Empty;"] });

        diagnostics.Act("upgrade request head", head);
        string expectedPart = "Sec-WebSocket-Key: " + Key + "\r\nX-Empty:\r\nConnection: Upgrade\r\n";
        diagnostics.Assert("head contains", expectedPart, head);
        StringAssert.Contains(head, "Sec-WebSocket-Key: " + Key + "\r\nX-Empty:\r\nConnection: Upgrade\r\n");
    }

    [TestMethod]
    public void Format_UpgradeHeader_ReplacesCurlsUpgradeAfterTheKey()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "ws://h:81/");
        diagnostics.Arrange("headers", "Upgrade: other");

        string head = Format("ws://h:81/", new HttpRequestOptions { Headers = ["Upgrade: other"] });

        diagnostics.Act("upgrade request head", head);
        string expectedPart = "Accept: */*\r\nSec-WebSocket-Version: 13\r\nSec-WebSocket-Key: " + Key + "\r\nUpgrade: other\r\n";
        diagnostics.Assert("head contains", expectedPart, head);
        StringAssert.Contains(head, "Accept: */*\r\nSec-WebSocket-Version: 13\r\nSec-WebSocket-Key: " + Key + "\r\nUpgrade: other\r\n");
        bool hasWebsocket = head.Contains("websocket", StringComparison.Ordinal);
        diagnostics.Assert("head contains websocket", false, hasWebsocket);
        Assert.IsFalse(head.Contains("websocket", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Format_EmptyConnectionHeader_KeepsCurlsConnectionUpgrade()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "ws://h:81/");
        diagnostics.Arrange("headers", "Connection:");

        string head = Format("ws://h:81/", new HttpRequestOptions { Headers = ["Connection:"] });

        diagnostics.Act("upgrade request head", head);
        string expectedEnd = "Sec-WebSocket-Key: " + Key + "\r\nConnection: Upgrade\r\n\r\n";
        diagnostics.Assert("head ends with", expectedEnd, head);
        StringAssert.EndsWith(head, "Sec-WebSocket-Key: " + Key + "\r\nConnection: Upgrade\r\n\r\n");
    }

    [TestMethod]
    public void Format_TwoConnectionHeaders_KeepsTheFirstValue()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "ws://h:81/");
        diagnostics.Arrange("headers", "Connection: a, Connection: b");

        string head = Format("ws://h:81/", new HttpRequestOptions { Headers = ["Connection: a", "Connection: b"] });

        diagnostics.Act("upgrade request head", head);
        const string expectedEnd = "Connection: a, Upgrade\r\n\r\n";
        diagnostics.Assert("head ends with", expectedEnd, head);
        StringAssert.EndsWith(head, expectedEnd);
    }

    [TestMethod]
    public void Format_NonAsciiPathAndQuery_PercentEncodesThePathAndSendsTheQueryAsUtf8()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "ws://h:81/é?q=é");

        byte[] head = WsUpgradeRequestFormatter.Format(CurlUrl.Parse("ws://h:81/é?q=é"), new HttpRequestOptions(), "GET", Key, null);

        diagnostics.Bytes("upgrade request head", head);
        diagnostics.Act("head length", head.Length);
        byte[] expected = Encoding.ASCII.GetBytes("GET /%C3%A9?q=").Concat(new byte[] { 0xC3, 0xA9 }).Concat(Encoding.ASCII.GetBytes(" HTTP/1.1\r\n")).ToArray();
        byte[] actualStart = head.Take(27).ToArray();
        diagnostics.Diff("request line bytes", expected, actualStart);
        CollectionAssert.AreEqual(
            Encoding.ASCII.GetBytes("GET /%C3%A9?q=").Concat(new byte[] { 0xC3, 0xA9 }).Concat(Encoding.ASCII.GetBytes(" HTTP/1.1\r\n")).ToArray(),
            head.Take(27).ToArray());
    }

    [TestMethod]
    public void Format_AgentInTheCommandLineEncoding_SendsItsBytes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var options = new HttpRequestOptions { UserAgent = "é", CommandLineTextEncoding = Encoding.UTF8 };
        diagnostics.Arrange("user agent", "é");
        diagnostics.Arrange("command line encoding", "UTF-8");

        string head = Format("ws://h:81/", options);

        diagnostics.Act("upgrade request head", head);
        const string expectedPart = "User-Agent: Ã©\r\n";
        diagnostics.Assert("head contains", expectedPart, head);
        StringAssert.Contains(head, expectedPart);
    }

    [TestMethod]
    public void RequestTarget_UrlWithoutQuery_IsThePath()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "ws://h/a/b");

        string target = WsUpgradeRequestFormatter.RequestTarget(CurlUrl.Parse("ws://h/a/b"));

        diagnostics.Act("request target", target);
        diagnostics.Assert("request target", "/a/b", target);
        Assert.AreEqual("/a/b", target);
    }

    private static string Format(string url, HttpRequestOptions options, string method = "GET", string? authorization = null) =>
        Encoding.Latin1.GetString(WsUpgradeRequestFormatter.Format(CurlUrl.Parse(url), options, method, Key, authorization));

    private static int CountOf(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;
}
