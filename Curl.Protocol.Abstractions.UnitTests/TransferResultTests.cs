namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins how <see cref="TransferResult" />'s factories fill in
/// <see cref="TransferResult.SourceLastWriteTimeUtc" />, the value <c>-R</c>/<c>--remote-time</c>
/// is applied from.
/// </summary>
[TestClass]
public sealed class TransferResultTests
{
    private static readonly DateTimeOffset SourceTime =
        new(2026, 6, 24, 12, 34, 56, TimeSpan.Zero);

    [TestMethod]
    public void Success_WithoutATimestamp_LeavesSourceLastWriteTimeUtcNull()
    {
        var result = TransferResult.Success(10);

        Assert.IsNull(result.SourceLastWriteTimeUtc);
        Assert.AreEqual(10L, result.BytesTransferred);
        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    public void Success_WithATimestamp_CarriesIt()
    {
        var result = TransferResult.Success(10, SourceTime);

        Assert.AreEqual(SourceTime, result.SourceLastWriteTimeUtc);
        Assert.AreEqual(10L, result.BytesTransferred);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public void Failure_Always_LeavesSourceLastWriteTimeUtcNull()
    {
        var result = TransferResult.Failure(CurlExitCode.ReadError, "failed");

        Assert.IsNull(result.SourceLastWriteTimeUtc);
        Assert.IsFalse(result.IsSuccess);
    }

    [TestMethod]
    public void Failure_WithABytesTransferredCount_ReportsIt()
    {
        var result = TransferResult.Failure(CurlExitCode.WriteError, "x", 5);

        Assert.AreEqual(5L, result.BytesTransferred);
    }

    [TestMethod]
    public void Failure_WithoutABytesTransferredCount_ReportsZero()
    {
        var result = TransferResult.Failure(CurlExitCode.WriteError, "x");

        Assert.AreEqual(0L, result.BytesTransferred);
    }

    [TestMethod]
    public void Constructor_WithThreePositionalValues_LeavesSourceLastWriteTimeUtcNull()
    {
        var result = new TransferResult(CurlExitCode.Ok, 5, null);

        Assert.IsNull(result.SourceLastWriteTimeUtc);
    }
}
