using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

/// <summary>
/// <c>%{size_header}</c> through a CONNECT tunnel counts the proxy's CONNECT reply heads before
/// the response's, as curl 8.21.0 does, <c>--suppress-connect-headers</c> or not (upstream
/// test1288, BL-2010).
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    [DataRow(0L, DisplayName = "no CONNECT")]
    [DataRow(61L, DisplayName = "one CONNECT reply, test1288's")]
    [DataRow(152L, DisplayName = "a 407 then a 200")]
    public async Task ExecuteAsync_ThroughATunnelWhoseReplyHeadsHeldNBytes_AddsThemToTheHeaderSize(long connectHeadBytes)
    {
        const string response = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";
        long responseHeadBytes = response.Length - 2;
        Diagnostics.Arrange("proxy connect header bytes", connectHeadBytes);
        QueueConnector connector = new(ConnectResult.Connected(Connection(response, 65536), null, proxyConnectHeaderBytes: connectHeadBytes));

        TransferResult result = await Handler(connector).ExecuteAsync(ReuseContext(NoTransferEvents.Instance));

        WriteResult(result);
        Diagnostics.Assert("header size", connectHeadBytes + responseHeadBytes, result.Report?.HeaderSize);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(connectHeadBytes + responseHeadBytes, result.Report!.HeaderSize);
    }
}
