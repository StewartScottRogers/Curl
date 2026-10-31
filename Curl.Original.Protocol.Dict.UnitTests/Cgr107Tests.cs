using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Dict.Fakes;

namespace Curl.Original.Protocol.Dict;

/// <summary>
/// Pins CGR-107 (BL-3959): a dict transfer reports every request byte it sent as
/// <c>%{size_request}</c>. Real curl 8.22.0 on Windows was measured with
/// <c>Record-CurlExchange.ps1</c> on 2026-10-28: <c>curl -s -o NUL -w %{size_request}
/// dict://127.0.0.1:PORT/d:word</c> prints 44, the bytes of <c>CLIENT libcurl 8.22.0</c>,
/// <c>DEFINE ! word</c> and <c>QUIT</c> with their CRLFs. The bytes are ASCII, so the case
/// runs the same on every platform.
/// </summary>
[TestClass]
public sealed class Cgr107Tests
{
    [TestMethod]
    public async Task ExecuteAsync_Define_ReportsTheRequestBytesAsRequestSize()
    {
        var handler = new DictProtocolHandler(new RecordingConnector(ConnectResult.Connected(new ScriptedConnection("220 hi\r\n"u8.ToArray()))));
        var context = new TransferContext { Url = CurlUrl.Parse("dict://127.0.0.1:21001/d:word"), Output = new MemoryStream() };

        TransferResult result = await handler.ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(44, result.Report!.RequestSize);
        Assert.AreEqual(8, result.Report.DownloadSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoBody_StillReportsTheRequestBytesAsRequestSize()
    {
        var handler = new DictProtocolHandler(new RecordingConnector(ConnectResult.Connected(new ScriptedConnection())));
        var context = new TransferContext { Url = CurlUrl.Parse("dict://127.0.0.1:21001/d:word"), Output = new MemoryStream(), NoBody = true };

        TransferResult result = await handler.ExecuteAsync(context);

        Assert.AreEqual(44, result.Report!.RequestSize);
    }
}
