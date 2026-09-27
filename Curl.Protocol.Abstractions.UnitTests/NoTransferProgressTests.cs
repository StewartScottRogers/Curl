namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins that every member of <see cref="NoTransferProgress" /> does nothing (ADR-0045).
/// </summary>
[TestClass]
public sealed class NoTransferProgressTests
{
    [TestMethod]
    public void EveryMember_Called_DoesNotThrow()
    {
        ITransferProgress progress = NoTransferProgress.Instance;

        progress.ReportTransferStarted();
        progress.ReportDownloaded(10, 200000);
        progress.ReportDownloaded(10, null);
        progress.ReportUploaded(10, 200000);
        progress.ReportUploaded(10, null);
    }
}
