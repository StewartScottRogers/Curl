namespace Curl.Extension.GrpcWeb;

[TestClass]
public sealed class GrpcWebStatusTests
{
    private static readonly TransferResult Received = TransferResult.Success(42) with { Report = new TransferReport { ResponseCode = 200 } };

    [TestMethod]
    public void Check_StatusZero_LeavesTheResult()
    {
        Assert.AreSame(Received, GrpcWebStatus.Check([new("grpc-status", "0")], Received));
    }

    [TestMethod]
    public void Check_ANonZeroStatusWithAMessage_FailsWithExit22AndTheDecodedMessage()
    {
        TransferResult result = GrpcWebStatus.Check([new("Grpc-Status", " 5 "), new("grpc-message", "no%20such%20user")], Received);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("--grpc-web: the call failed with grpc-status 5: no such user", result.ErrorMessage);
        Assert.AreSame(Received.Report, result.Report);
        Assert.AreEqual(42, result.BytesTransferred);
    }

    [TestMethod]
    public void Check_ANonZeroStatusWithoutAMessage_FailsWithExit22()
    {
        TransferResult result = GrpcWebStatus.Check([new("grpc-status", "13")], Received);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("--grpc-web: the call failed with grpc-status 13", result.ErrorMessage);
    }

    [TestMethod]
    public void Check_NoStatus_FailsWithExit8()
    {
        TransferResult result = GrpcWebStatus.Check([new("content-type", "application/grpc-web")], Received);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        StringAssert.Contains(result.ErrorMessage, "no grpc-status");
    }
}
