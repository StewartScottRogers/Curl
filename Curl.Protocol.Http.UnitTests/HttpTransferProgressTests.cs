using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

[TestClass]
public sealed class HttpTransferProgressTests
{
    [TestMethod]
    public void ReportTransferStarted_Twice_PassesOnlyTheFirst()
    {
        RecordingTransferProgress sink = new();
        HttpTransferProgress progress = new(sink);

        progress.ReportTransferStarted();
        progress.ReportTransferStarted();

        CollectionAssert.AreEqual(new[] { "started" }, sink.Reports.ToArray());
    }

    [TestMethod]
    public void ReportTransferDone_PassesItOn()
    {
        RecordingTransferProgress sink = new();
        HttpTransferProgress progress = new(sink);

        progress.ReportTransferDone();

        CollectionAssert.AreEqual(new[] { "done" }, sink.Reports.ToArray());
    }

    [TestMethod]
    public void ReportDownloaded_BelowACountAlreadyReported_IsDropped()
    {
        RecordingTransferProgress sink = new();
        HttpTransferProgress progress = new(sink);

        progress.ReportDownloaded(5, 10);
        progress.ReportDownloaded(3, 10);
        progress.ReportDownloaded(5, 10);
        progress.ReportDownloaded(10, 10);

        CollectionAssert.AreEqual(new[] { "down 5/10", "down 5/10", "down 10/10" }, sink.Reports.ToArray());
    }

    [TestMethod]
    public void ReportUploaded_BelowACountAlreadyReported_IsDropped()
    {
        RecordingTransferProgress sink = new();
        HttpTransferProgress progress = new(sink);

        progress.ReportUploaded(5, null);
        progress.ReportUploaded(0, null);
        progress.ReportUploaded(7, null);

        CollectionAssert.AreEqual(new[] { "up 5/?", "up 7/?" }, sink.Reports.ToArray());
    }
}
