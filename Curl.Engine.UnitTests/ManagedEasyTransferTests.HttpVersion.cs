using Curl.Protocol.Abstractions;

namespace Curl.Engine;

public sealed partial class ManagedEasyTransferTests
{
    [TestMethod]
    public void HttpVersionOf_FailedWithNoReportedVersion_IsTheLastStatusLines()
    {
        // easytest lowspeed (exit 28) and get-follow/rftp (exit 7 on the ftp hop): libcurl 8.22.0 answers 2 (BL-3338).
        Assert.AreEqual(2L, ManagedEasyTransfer.HttpVersionOf(0, new TransferResult(CurlExitCode.OperationTimedOut, 10), 2));
        Assert.AreEqual(2L, ManagedEasyTransfer.HttpVersionOf(0, new TransferResult(CurlExitCode.CouldntConnect, 0), 2));
    }

    [TestMethod]
    public void HttpVersionOf_ReportedVersionOrSuccess_IsTheReported()
    {
        Assert.AreEqual(3L, ManagedEasyTransfer.HttpVersionOf(3, new TransferResult(CurlExitCode.OperationTimedOut, 0), 2));
        Assert.AreEqual(0L, ManagedEasyTransfer.HttpVersionOf(0, new TransferResult(CurlExitCode.Ok, 0), 2));
    }
}
