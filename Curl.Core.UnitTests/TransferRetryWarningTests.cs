using Curl.Testing;

namespace Curl.Core;

/// <summary>
/// Pins <see cref="TransferRetryWarning" /> to the lines curl 8.21.0 (mingw, Schannel)
/// printed on 2026-09-26 and 2026-09-27; the commands are in BL-208's and BL-317's notes.
/// </summary>
[TestClass]
public sealed class TransferRetryWarningTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(1000, 4L, "Warning: Problem : HTTP error. Retrying in 1 second. 4 retries left.")]
    [DataRow(2000, 3L, "Warning: Problem : HTTP error. Retrying in 2 seconds. 3 retries left.")]
    [DataRow(8000, 1L, "Warning: Problem : HTTP error. Retrying in 8 seconds. 1 retry left.")]
    [DataRow(1500, 2L, "Warning: Problem : HTTP error. Retrying in 1.500 seconds. 2 retries left.")]
    [DataRow(21600000, 2147483647L, "Warning: Problem : HTTP error. Retrying in 21600 seconds. 2147483647 retries left.")]
    public void For_HttpError_MatchesMeasuredLine(int milliseconds, long retriesLeft, string expected) =>
        Assert.AreEqual(expected, Warn(TransferRetryReason.HttpError, milliseconds, retriesLeft, expected));

    [TestMethod]
    [DataRow(1000, 2L, "Warning: Problem : timeout. Retrying in 1 second. 2 retries left.")]
    [DataRow(2000, 1L, "Warning: Problem : timeout. Retrying in 2 seconds. 1 retry left.")]
    public void For_Timeout_MatchesMeasuredLine(int milliseconds, long retriesLeft, string expected) =>
        Assert.AreEqual(expected, Warn(TransferRetryReason.Timeout, milliseconds, retriesLeft, expected));

    [TestMethod]
    [DataRow(1000, 2L, "Warning: Problem : connection refused. Retrying in 1 second. 2 retries left.")]
    [DataRow(2000, 1L, "Warning: Problem : connection refused. Retrying in 2 seconds. 1 retry left.")]
    public void For_ConnectionRefused_MatchesMeasuredLine(int milliseconds, long retriesLeft, string expected) =>
        Assert.AreEqual(expected, Warn(TransferRetryReason.ConnectionRefused, milliseconds, retriesLeft, expected));

    [TestMethod]
    [DataRow(1000, 2L, "Warning: Problem : FTP error. Retrying in 1 second. 2 retries left.")]
    [DataRow(2000, 1L, "Warning: Problem : FTP error. Retrying in 2 seconds. 1 retry left.")]
    public void For_FtpError_MatchesMeasuredLine(int milliseconds, long retriesLeft, string expected) =>
        Assert.AreEqual(expected, Warn(TransferRetryReason.FtpError, milliseconds, retriesLeft, expected));

    [TestMethod]
    [DataRow(1000, 2L, "Warning: Problem (retrying all errors). Retrying in 1 second. 2 retries left.")]
    [DataRow(2000, 1L, "Warning: Problem (retrying all errors). Retrying in 2 seconds. 1 retry left.")]
    public void For_AllErrors_MatchesMeasuredLine(int milliseconds, long retriesLeft, string expected) =>
        Assert.AreEqual(expected, Warn(TransferRetryReason.AllErrors, milliseconds, retriesLeft, expected));

    private string Warn(TransferRetryReason reason, int milliseconds, long retriesLeft, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("reason", reason);
        diagnostics.Arrange("delay ms", milliseconds);
        diagnostics.Arrange("retries left", retriesLeft);

        var line = TransferRetryWarning.For(reason, TimeSpan.FromMilliseconds(milliseconds), retriesLeft);

        diagnostics.Act("warning", line);
        diagnostics.Diff("warning", expected, line);
        return line;
    }
}
