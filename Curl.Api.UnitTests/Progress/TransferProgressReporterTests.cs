namespace Curl.Api.Progress;

/// <summary>The phase <see cref="TransferProgressReporter"/> reads from a transfer's byte counts.</summary>
[TestClass]
public sealed class TransferProgressReporterTests
{
    [TestMethod]
    [DataRow(0L, 0L, 0L, CurlTransferPhase.Connecting)]
    [DataRow(0L, 0L, 1L, CurlTransferPhase.Uploading)]
    [DataRow(10L, 0L, 0L, CurlTransferPhase.Downloading)]
    [DataRow(0L, 1L, 5L, CurlTransferPhase.Downloading)]
    public void PhaseOf_ByteCounts_GivesTheDirectionTheBytesMove(long downloadTotal, long downloadNow, long uploadNow, CurlTransferPhase expected) =>
        Assert.AreEqual(expected, TransferProgressReporter.PhaseOf(downloadTotal, downloadNow, uploadNow));
}
