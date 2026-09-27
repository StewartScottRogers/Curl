namespace Curl.Core;

/// <summary>
/// Pins <see cref="TransferRetryWarning" /> to the lines curl 8.21.0 (mingw, Schannel)
/// printed on 2026-09-26; the commands are in BL-208's notes.
/// </summary>
[TestClass]
public sealed class TransferRetryWarningTests
{
    [TestMethod]
    [DataRow(1000, 4L, "Warning: Problem : HTTP error. Retrying in 1 second. 4 retries left.")]
    [DataRow(2000, 3L, "Warning: Problem : HTTP error. Retrying in 2 seconds. 3 retries left.")]
    [DataRow(8000, 1L, "Warning: Problem : HTTP error. Retrying in 8 seconds. 1 retry left.")]
    [DataRow(1500, 2L, "Warning: Problem : HTTP error. Retrying in 1.500 seconds. 2 retries left.")]
    [DataRow(21600000, 2147483647L, "Warning: Problem : HTTP error. Retrying in 21600 seconds. 2147483647 retries left.")]
    public void For_HttpError_MatchesMeasuredLine(int milliseconds, long retriesLeft, string expected) =>
        Assert.AreEqual(expected, TransferRetryWarning.For(TransferRetryReason.HttpError, TimeSpan.FromMilliseconds(milliseconds), retriesLeft));

    [TestMethod]
    [DataRow(1000, 2L, "Warning: Problem : timeout. Retrying in 1 second. 2 retries left.")]
    [DataRow(2000, 1L, "Warning: Problem : timeout. Retrying in 2 seconds. 1 retry left.")]
    public void For_Timeout_MatchesMeasuredLine(int milliseconds, long retriesLeft, string expected) =>
        Assert.AreEqual(expected, TransferRetryWarning.For(TransferRetryReason.Timeout, TimeSpan.FromMilliseconds(milliseconds), retriesLeft));
}
