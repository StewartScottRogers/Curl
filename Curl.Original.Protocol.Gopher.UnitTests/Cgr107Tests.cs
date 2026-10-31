using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Gopher.Fakes;

namespace Curl.Original.Protocol.Gopher;

/// <summary>
/// Pins CGR-107 (BL-3959): a gopher transfer reports the selector and CRLF it sent as
/// <c>%{size_request}</c>. Real curl 8.22.0 on Windows was measured with
/// <c>Record-CurlExchange.ps1</c> on 2026-10-28: <c>curl -s -o NUL -w %{size_request}
/// gopher://127.0.0.1:PORT/1/x</c> sends <c>/x</c> and CRLF and prints 4. The bytes are
/// ASCII, so the case runs the same on every platform.
/// </summary>
[TestClass]
public sealed class Cgr107Tests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecuteAsync_Selector_ReportsTheSelectorAndLineEndAsRequestSize(bool noBody)
    {
        ScriptedConnection connection = new("hello\r\n"u8.ToArray());
        var context = new TransferContext { Url = CurlUrl.Parse("gopher://127.0.0.1:21001/1/x"), Output = new MemoryStream(), NoBody = noBody };

        TransferResult result = await new GopherProtocolHandler(FakeConnector.For(connection)).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(4, result.Report!.RequestSize);
        Assert.AreEqual(noBody ? 0 : 7, result.Report.DownloadSize);
    }
}
