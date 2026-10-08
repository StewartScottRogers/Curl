using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins the status lines curl 8.21.0 accepts and refuses in the reply to an upgrade request,
/// measured on 2026-09-28 with <c>Record-CurlExchange.ps1</c> (BL-580).
/// </summary>
[TestClass]
public sealed class WsStatusLineTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("HTTP/1.1 101 Switching Protocols", 101)]
    [DataRow("HTTP/1.1 101", 101)]
    [DataRow("HTTP/1.0 101 Sw", 101)]
    [DataRow("HTTP/1.1\t401 Unauthorized", 401)]
    [DataRow("http/1.1 101 Sw", 200)]
    public void ParseStatusCode_AcceptedLine_ReturnsTheCode(string line, int statusCode)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("status line", line);

        int actual = WsStatusLine.ParseStatusCode(line);

        diagnostics.Act("status code", actual);
        diagnostics.Assert("status code", statusCode, actual);
        Assert.AreEqual(statusCode, actual);
    }

    [TestMethod]
    [DataRow("garbage", "Received HTTP/0.9 when not allowed")]
    [DataRow("HTTP/", "Unsupported HTTP version in response")]
    [DataRow("HTTP/2 101 Sw", "Unsupported HTTP version (2.0) in response")]
    [DataRow("HTTP/3 101 Sw", "Unsupported HTTP version (3.0) in response")]
    [DataRow("HTTP/9 200 x", "Unsupported HTTP version in response")]
    [DataRow("HTTP/1.2 101 Sw", "Unsupported HTTP/1 subversion in response")]
    [DataRow("HTTP/1.1 1x1 Sw", "Unsupported HTTP/1 subversion in response")]
    [DataRow("HTTP/1.1-101 Sw", "Unsupported HTTP/1 subversion in response")]
    [DataRow("HTTP/1.1 10", "Unsupported HTTP/1 subversion in response")]
    [DataRow("HTTP/1.1 099 Sw", "Unsupported response code in HTTP response")]
    public void ParseStatusCode_RefusedLine_FailsWithExit1(string line, string message)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("status line", line);

        WsTransferException failure = Assert.ThrowsExactly<WsTransferException>(() => WsStatusLine.ParseStatusCode(line));

        diagnostics.Act("failure", $"{failure.GetType().Name}: {failure.ExitCode} ({failure.Message})");
        diagnostics.Assert("exit code", CurlExitCode.UnsupportedProtocol, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, failure.ExitCode);
        diagnostics.Assert("message", message, failure.Message);
        Assert.AreEqual(message, failure.Message);
    }

    [TestMethod]
    [DataRow("", true)]
    [DataRow("ht", true)]
    [DataRow("HTTP/1.1 101", true)]
    [DataRow("x", false)]
    [DataRow("HTX", false)]
    public void CanBegin_ReceivedBytes_MatchesTheStartOfHttp(string received, bool canBegin)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] bytes = System.Text.Encoding.Latin1.GetBytes(received);
        diagnostics.Bytes("received bytes", bytes);
        diagnostics.Arrange("received", received);

        bool actual = WsStatusLine.CanBegin(bytes);

        diagnostics.Act("can begin", actual);
        diagnostics.Assert("can begin", canBegin, actual);
        Assert.AreEqual(canBegin, WsStatusLine.CanBegin(System.Text.Encoding.Latin1.GetBytes(received)));
    }
}
