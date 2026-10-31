using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ws.Fakes;

namespace Curl.Original.Protocol.Ws;

/// <summary>
/// CGR-107 (BL-3960): <c>%{num_headers}</c> for a ws upgrade. Measured with
/// Record-CurlExchange.ps1 against curl 8.22.0 (Schannel), 2026-10-28:
/// <c>curl -sS -o NUL -w "%{num_headers} %{http_version} %header{upgrade}" ws://127.0.0.1:PORT/x</c>
/// answered with a 101 carrying <c>Upgrade</c>, <c>Connection</c> and <c>Sec-WebSocket-Accept</c>
/// prints <c>3 1.1 </c>: the three lines count, but <c>%header{}</c> finds none of them.
/// </summary>
[TestClass]
public sealed class Cgr107Tests
{
    private const string Head101 =
        "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=\r\n\r\n";

    [TestMethod]
    public async Task ExecuteAsync_SwitchingProtocols_ReportsEachHeaderLineAsAPseudoHeader()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(Head101 + "\x88\x02\x03\xe8"));
        var handler = new WsProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), new RecordingAuthenticator(), new FixedRandomSource());

        TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ws://127.0.0.1:47901/x"), Output = new MemoryStream() });

        Assert.AreEqual(HttpVersion.Version11, result.Report!.HttpVersion);
        Assert.IsEmpty(result.Report.ResponseHeaders);
        CollectionAssert.AreEqual(
            new KeyValuePair<string, string>[] { new("Upgrade", "websocket"), new("Connection", "Upgrade"), new("Sec-WebSocket-Accept", "s3pPLMBiTxaQ9kYGzzhZRbK+xOo=") },
            result.Report.PseudoHeaders.ToArray());
    }

    [TestMethod]
    public void HeaderLines_LineWithoutColon_IsANameWithAnEmptyValue()
    {
        KeyValuePair<string, string>[] lines = WsProtocolHandler.HeaderLines(Encoding.Latin1.GetBytes("HTTP/1.1 101 OK\r\nOdd\r\nA:  b \r\n\r\nC: d\r\n"));

        CollectionAssert.AreEqual(new KeyValuePair<string, string>[] { new("Odd", string.Empty), new("A", "b") }, lines);
    }
}
