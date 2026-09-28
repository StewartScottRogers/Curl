using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins the status lines curl 8.21.0 accepts and refuses in the reply to an upgrade request,
/// measured on 2026-09-28 with <c>Record-CurlExchange.ps1</c> (BL-580).
/// </summary>
[TestClass]
public sealed class WsStatusLineTests
{
    [TestMethod]
    [DataRow("HTTP/1.1 101 Switching Protocols", 101)]
    [DataRow("HTTP/1.1 101", 101)]
    [DataRow("HTTP/1.0 101 Sw", 101)]
    [DataRow("HTTP/1.1\t401 Unauthorized", 401)]
    [DataRow("http/1.1 101 Sw", 200)]
    public void ParseStatusCode_AcceptedLine_ReturnsTheCode(string line, int statusCode)
    {
        Assert.AreEqual(statusCode, WsStatusLine.ParseStatusCode(line));
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
        WsTransferException failure = Assert.ThrowsExactly<WsTransferException>(() => WsStatusLine.ParseStatusCode(line));

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, failure.ExitCode);
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
        Assert.AreEqual(canBegin, WsStatusLine.CanBegin(System.Text.Encoding.Latin1.GetBytes(received)));
    }
}
