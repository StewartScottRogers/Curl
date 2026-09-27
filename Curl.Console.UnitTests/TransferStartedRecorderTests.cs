namespace Curl.Console;

[TestClass]
public sealed class TransferStartedRecorderTests
{
    [TestMethod]
    public void HasTransferStarted_NothingReported_IsFalse()
    {
        Assert.IsFalse(new TransferStartedRecorder().HasTransferStarted);
    }

    [TestMethod]
    public void HasTransferStarted_AfterReportTransferStarted_IsTrue()
    {
        TransferStartedRecorder recorder = new();

        recorder.ReportTransferStarted();

        Assert.IsTrue(recorder.HasTransferStarted);
    }

    [TestMethod]
    public void HasTransferStarted_AfterOnlyByteReports_IsFalse()
    {
        TransferStartedRecorder recorder = new();

        recorder.ReportDownloaded(10, 20);
        recorder.ReportUploaded(5, null);

        Assert.IsFalse(recorder.HasTransferStarted);
    }
}
