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

    [TestMethod]
    public void WithEndPointsBeforeFailure_CopiesTheResultWithTheEndPoints()
    {
        var result = ConnectResult.Failed(CurlExitCode.OperationTimedOut, "Connection timed out after 2000 milliseconds");
        var local = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 50000);
        var remote = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 28950);

        var copy = result.WithEndPointsBeforeFailure(local, remote);

        Assert.AreEqual(local, copy.LocalEndPointBeforeFailure);
        Assert.AreEqual(remote, copy.RemoteEndPointBeforeFailure);
        Assert.IsNull(result.RemoteEndPointBeforeFailure);
    }

    [TestMethod]
    public void CarriedBy_ACancellationCarryingAResult_GivesIt()
    {
        var carried = ConnectResult.Failed(CurlExitCode.OperationTimedOut, "x");
        var canceled = new OperationCanceledException();
        canceled.Data[ConnectResult.TcpConnectedOnCancellationKey] = carried;

        Assert.AreSame(carried, ConnectResult.CarriedBy(canceled));
        Assert.IsNull(ConnectResult.CarriedBy(new OperationCanceledException()));
    }
}
