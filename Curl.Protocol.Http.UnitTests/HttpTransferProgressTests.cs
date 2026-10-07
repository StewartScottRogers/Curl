using Curl.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Http;

[TestClass]
public sealed class HttpTransferProgressTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ReportTransferStarted_Twice_PassesOnlyTheFirst()
    {
        RecordingTransferProgress sink = new();
        HttpTransferProgress progress = new(sink);
        Diagnostics.Arrange("calls", "ReportTransferStarted twice");

        progress.ReportTransferStarted();
        progress.ReportTransferStarted();

        Diagnostics.Act("reports", string.Join(" | ", sink.Reports));
        Diagnostics.Assert("reports", "started", string.Join(" | ", sink.Reports));
        CollectionAssert.AreEqual(new[] { "started" }, sink.Reports.ToArray());
    }

    [TestMethod]
    public void ReportTransferDone_PassesItOn()
    {
        RecordingTransferProgress sink = new();
        HttpTransferProgress progress = new(sink);
        Diagnostics.Arrange("calls", "ReportTransferDone once");

        progress.ReportTransferDone();

        Diagnostics.Act("reports", string.Join(" | ", sink.Reports));
        Diagnostics.Assert("reports", "done", string.Join(" | ", sink.Reports));
        CollectionAssert.AreEqual(new[] { "done" }, sink.Reports.ToArray());
    }

    [TestMethod]
    public void ReportDownloaded_BelowACountAlreadyReported_IsDropped()
    {
        RecordingTransferProgress sink = new();
        HttpTransferProgress progress = new(sink);
        Diagnostics.Arrange("downloaded counts", "5, 3, 5, 10 of 10");

        progress.ReportDownloaded(5, 10);
        progress.ReportDownloaded(3, 10);
        progress.ReportDownloaded(5, 10);
        progress.ReportDownloaded(10, 10);

        Diagnostics.Act("reports", string.Join(" | ", sink.Reports));
        Diagnostics.Assert("reports", "down 5/10 | down 5/10 | down 10/10", string.Join(" | ", sink.Reports));
        CollectionAssert.AreEqual(new[] { "down 5/10", "down 5/10", "down 10/10" }, sink.Reports.ToArray());
    }

    [TestMethod]
    public void ReportUploaded_BelowACountAlreadyReported_IsDropped()
    {
        RecordingTransferProgress sink = new();
        HttpTransferProgress progress = new(sink);
        Diagnostics.Arrange("uploaded counts", "5, 0, 7 of unknown");

        progress.ReportUploaded(5, null);
        progress.ReportUploaded(0, null);
        progress.ReportUploaded(7, null);

        Diagnostics.Act("reports", string.Join(" | ", sink.Reports));
        Diagnostics.Assert("reports", "up 5/? | up 7/?", string.Join(" | ", sink.Reports));
        CollectionAssert.AreEqual(new[] { "up 5/?", "up 7/?" }, sink.Reports.ToArray());
    }
}
