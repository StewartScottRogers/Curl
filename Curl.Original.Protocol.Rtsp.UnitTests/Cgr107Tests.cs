using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Rtsp.Fakes;

namespace Curl.Original.Protocol.Rtsp;

/// <summary>
/// CGR-107 (BL-3960): <c>%{num_headers}</c> and <c>%{http_version}</c> for RTSP. Measured with
/// Record-CurlExchange.ps1 against curl 8.22.0 (Schannel), 2026-10-28:
/// <c>curl -sS -w "%{num_headers} %{http_version} %header{cseq}" rtsp://127.0.0.1:PORT/m</c>
/// answered <c>RTSP/1.0 200 OK</c> with <c>CSeq</c> and <c>Content-Length</c> prints <c>2 1.1 </c>,
/// and with <c>CSeq</c>, <c>Session</c>, <c>X-A</c> and <c>Content-Length</c> prints <c>4 1.1 </c>:
/// every header line counts, but <c>%header{}</c> finds none of them.
/// </summary>
[TestClass]
public sealed class Cgr107Tests
{
    [TestMethod]
    public async Task ExecuteAsync_Reply_ReportsEachHeaderLineAsAPseudoHeaderAndHttp11()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nSession: abc\r\nX-A:  1 \r\nContent-Length: 0\r\n\r\n"));
        var handler = new RtspProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), new RecordingAuthenticator());

        TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("rtsp://127.0.0.1:47950/m"), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(HttpVersion.Version11, result.Report!.HttpVersion);
        Assert.IsEmpty(result.Report.ResponseHeaders);
        CollectionAssert.AreEqual(
            new KeyValuePair<string, string>[] { new("CSeq", "1"), new("Session", "abc"), new("X-A", "1"), new("Content-Length", "0") },
            result.Report.PseudoHeaders.ToArray());
    }
}
