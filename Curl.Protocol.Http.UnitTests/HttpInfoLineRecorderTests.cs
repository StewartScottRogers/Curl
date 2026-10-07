using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpInfoLineRecorder" />: it keeps the info lines, in order, and drops every
/// other event.
/// </summary>
[TestClass]
public sealed class HttpInfoLineRecorderTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ReportInfo_SeveralLines_KeepsThemInOrder()
    {
        HttpInfoLineRecorder recorder = new();
        Diagnostics.Arrange("info lines", "a, b");

        recorder.ReportInfo("a");
        recorder.ReportInfo("b");

        Diagnostics.Act("kept", string.Join(", ", recorder.Lines));
        Diagnostics.Assert("kept", "a, b", string.Join(", ", recorder.Lines));
        CollectionAssert.AreEqual(new[] { "a", "b" }, recorder.Lines.ToArray());
    }

    [TestMethod]
    public void OtherEvents_Reported_KeepNoLine()
    {
        HttpInfoLineRecorder recorder = new();
        Diagnostics.Arrange("events", "every event but info");

        recorder.ReportConnectionOpened(null!);
        recorder.ReportConnectionReused(null!);
        recorder.ReportTlsHandshake(null!);
        recorder.ReportTlsData([1], sent: true);
        recorder.ReportRequestHeader([1]);
        recorder.ReportResponseHeader([1]);
        recorder.ReportDataSent([1]);
        recorder.ReportDataReceived([1]);

        Diagnostics.Act("kept", recorder.Lines.Count);
        Diagnostics.Assert("kept", 0, recorder.Lines.Count);
        Assert.IsEmpty(recorder.Lines);
    }
}
