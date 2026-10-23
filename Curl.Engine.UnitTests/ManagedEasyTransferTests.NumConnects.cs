using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Api.Options.Network;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

public sealed partial class ManagedEasyTransferTests
{
    private static readonly TransferResult OkResult = new(CurlExitCode.Ok, 0);

    private static readonly TransferResult TimedOutWithNoReport = new(CurlExitCode.OperationTimedOut, 0);

    private static EasyOptions NetworkOf(EasyNetworkOptions network) => new() { Network = network };

    [TestMethod]
    public void NumConnectsOf_ReportCountsAConnection_KeepsIt() =>
        Assert.AreEqual(2, ManagedEasyTransfer.NumConnectsOf(new TransferInfo { NumConnects = 2 }, OkResult, new EasyOptions(), "http://h/"));

    [TestMethod]
    public void NumConnectsOf_PlainReusedTransfer_IsZero() =>
        Assert.AreEqual(0, ManagedEasyTransfer.NumConnectsOf(new TransferInfo(), OkResult, new EasyOptions(), "http://h/"));

    [TestMethod]
    public void NumConnectsOf_FreshConnectThatSucceeded_IsOne() =>
        Assert.AreEqual(1, ManagedEasyTransfer.NumConnectsOf(new TransferInfo(), OkResult, NetworkOf(new EasyNetworkOptions { FreshConnect = true }), "http://h/"));

    [TestMethod]
    public void NumConnectsOf_ForbidReuseThatSucceeded_IsOne() =>
        Assert.AreEqual(1, ManagedEasyTransfer.NumConnectsOf(new TransferInfo(), OkResult, NetworkOf(new EasyNetworkOptions { ForbidReuse = true }), "http://h/"));

    [TestMethod]
    public void NumConnectsOf_FreshConnectThatFailed_IsZero() =>
        Assert.AreEqual(0, ManagedEasyTransfer.NumConnectsOf(new TransferInfo(), new TransferResult(CurlExitCode.CouldntConnect, 0), NetworkOf(new EasyNetworkOptions { FreshConnect = true }), "http://h/"));

    [TestMethod]
    public void NumConnectsOf_WatchdogCutOffAfterTheHead_IsOne() =>
        Assert.AreEqual(1, ManagedEasyTransfer.NumConnectsOf(new TransferInfo { ResponseCode = 200 }, TimedOutWithNoReport, new EasyOptions(), "http://h/"));

    [TestMethod]
    public void NumConnectsOf_TimeoutBeforeAnyHead_CountsTheAttemptedConnection() =>
        Assert.AreEqual(1, ManagedEasyTransfer.NumConnectsOf(new TransferInfo(), TimedOutWithNoReport, new EasyOptions(), "http://h/"));

    [TestMethod]
    public void NumConnectsOf_TftpFailure_IsOne() =>
        Assert.AreEqual(1, ManagedEasyTransfer.NumConnectsOf(new TransferInfo(), new TransferResult(CurlExitCode.OperationTimedOut, 0), new EasyOptions(), "TFTP://h/x"));

    [TestMethod]
    public void NumConnectsOf_TimeoutWithAReportThatCountsNone_IsZero() =>
        Assert.AreEqual(0, ManagedEasyTransfer.NumConnectsOf(new TransferInfo(), TimedOutWithNoReport with { Report = new TransferReport() }, new EasyOptions(), "http://h/"));

    [TestMethod]
    public void NumConnectsOf_TlsHandshakeTimeout_IsOne() =>
        Assert.AreEqual(1, ManagedEasyTransfer.NumConnectsOf(new TransferInfo(), TimedOutWithNoReport, new EasyOptions(), "https://127.0.0.1:18080/echo"));
}
