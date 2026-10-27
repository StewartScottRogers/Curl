using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// CGR-084: <c>%{num_connects}</c> counts the connection to the proxy after the proxy refused the tunnel - a
/// <c>CONNECT</c> answered 403 or 407, or a SOCKS handshake that failed - as curl 8.22.0 reports 1.
/// </summary>
[TestClass]
public sealed class Cgr084Tests
{
    [TestMethod]
    [DataRow(403L)]
    [DataRow(407L)]
    public void NumConnectsOf_ConnectRefusedByTheProxy_CountsTheProxyConnection(long connectCode) =>
        Assert.AreEqual(1, ManagedEasyTransfer.NumConnectsOf(new TransferInfo { HttpConnectCode = connectCode }, new TransferResult(CurlExitCode.CouldntConnect, 0), new EasyOptions(), "http://example.test/"));

    [TestMethod]
    public void NumConnectsOf_SocksHandshakeFailed_CountsTheProxyConnection() =>
        Assert.AreEqual(1, ManagedEasyTransfer.NumConnectsOf(new TransferInfo { ProxyError = 4 }, new TransferResult(CurlExitCode.Proxy, 0), new EasyOptions(), "http://127.0.0.1:8080/x"));

    [TestMethod]
    public void NumConnectsOf_ProxyConnectRefusedWithNoProxyAnswer_IsZero() =>
        Assert.AreEqual(0, ManagedEasyTransfer.NumConnectsOf(new TransferInfo(), new TransferResult(CurlExitCode.CouldntConnect, 0), new EasyOptions(), "http://example.test/"));
}
