using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins <see cref="WsInfoLineRecorder" />: it keeps the info lines, in order, and drops every
/// other event.
/// </summary>
[TestClass]
public sealed class WsInfoLineRecorderTests
{
    [TestMethod]
    public void ReportInfo_SeveralLines_KeepsThemInOrder()
    {
        WsInfoLineRecorder recorder = new();

        recorder.ReportInfo("a");
        recorder.ReportInfo("b");

        CollectionAssert.AreEqual(new[] { "a", "b" }, recorder.Lines.ToArray());
    }

    [TestMethod]
    public void OtherEvents_Reported_KeepNoLine()
    {
        WsInfoLineRecorder recorder = new();

        recorder.ReportConnectionOpened(null!);
        recorder.ReportConnectionReused(null!);
        recorder.ReportTlsHandshake(null!);
        recorder.ReportTlsData([1], sent: true);
        recorder.ReportRequestHeader([1]);
        recorder.ReportResponseHeader([1]);
        recorder.ReportDataSent([1]);
        recorder.ReportDataReceived([1]);

        Assert.IsEmpty(recorder.Lines);
    }
}
