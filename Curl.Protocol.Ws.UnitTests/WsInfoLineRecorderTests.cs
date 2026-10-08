using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins <see cref="WsInfoLineRecorder" />: it keeps the info lines, in order, and drops every
/// other event.
/// </summary>
[TestClass]
public sealed class WsInfoLineRecorderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ReportInfo_SeveralLines_KeepsThemInOrder()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WsInfoLineRecorder recorder = new();
        diagnostics.Arrange("reported info lines", "a, b");

        recorder.ReportInfo("a");
        recorder.ReportInfo("b");

        diagnostics.Act("recorded lines", string.Join(", ", recorder.Lines));
        diagnostics.Assert("recorded lines", "a, b", string.Join(", ", recorder.Lines));
        CollectionAssert.AreEqual(new[] { "a", "b" }, recorder.Lines.ToArray());
    }

    [TestMethod]
    public void OtherEvents_Reported_KeepNoLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        WsInfoLineRecorder recorder = new();
        diagnostics.Arrange("reported events", "opened, reused, tls handshake, tls data, request header, response header, data sent, data received");

        recorder.ReportConnectionOpened(null!);
        recorder.ReportConnectionReused(null!);
        recorder.ReportTlsHandshake(null!);
        recorder.ReportTlsData([1], sent: true);
        recorder.ReportRequestHeader([1]);
        recorder.ReportResponseHeader([1]);
        recorder.ReportDataSent([1]);
        recorder.ReportDataReceived([1]);

        diagnostics.Act("recorded line count", recorder.Lines.Count);
        diagnostics.Assert("recorded line count", 0, recorder.Lines.Count);
        Assert.IsEmpty(recorder.Lines);
    }
}
