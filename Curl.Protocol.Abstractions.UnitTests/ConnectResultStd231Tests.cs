namespace Curl.Protocol.Abstractions;

/// <summary>Pins <see cref="ConnectResult.IsTcpConnectedBeforeFailure" />, which a failed transfer's <c>%{num_connects}</c> counts (STD-231).</summary>
[TestClass]
public sealed class ConnectResultStd231Tests
{
    [TestMethod]
    public void Failed_ByDefault_SaysNoTcpConnectionWasOpen()
    {
        var result = ConnectResult.Failed(CurlExitCode.OperationTimedOut, "Connection timed out after 2000 milliseconds");

        Assert.IsFalse(result.IsTcpConnectedBeforeFailure);
    }

    [TestMethod]
    public void WithTcpConnectedBeforeFailure_True_CopiesTheResultWithTheFlagSet()
    {
        var result = ConnectResult.Failed(CurlExitCode.OperationTimedOut, "Connection timed out after 2000 milliseconds", null, connectionNumber: 3);

        var copy = result.WithTcpConnectedBeforeFailure(true);

        Assert.IsTrue(copy.IsTcpConnectedBeforeFailure);
        Assert.IsFalse(result.IsTcpConnectedBeforeFailure);
        Assert.AreEqual(3, copy.ConnectionNumber);
        Assert.AreEqual(result.ErrorMessage, copy.ErrorMessage);
    }
}
