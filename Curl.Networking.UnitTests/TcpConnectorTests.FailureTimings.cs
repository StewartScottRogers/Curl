using System.Net;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins that a connect failing after the TCP connect still reports the timings taken before it,
/// so <c>%{time_namelookup}</c> and <c>%{time_connect}</c> are not zero, as curl 8.22.0 prints
/// them (fitness finding STD-145, BL-5464).
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_WhenTheTlsHandshakeFails_KeepsTheNameLookupAndConnectTimings()
    {
        var failure = ConnectResult.Failed(CurlExitCode.PeerFailedVerification, "certificate refused");
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider { FailureToReturn = failure }, new SteppingTimeProvider(100));

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 443, UseTls: true));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
        Assert.IsNotNull(result.Timings);
        Assert.IsTrue(result.Timings.NameResolved > result.Timings.Started, "time_namelookup is not zero.");
        Assert.IsNotNull(result.Timings.Connected);
        Assert.AreEqual(1L, result.ConnectionNumber + 1);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheProxyRefusesTheConnect_KeepsTheNameLookupAndConnectTimings()
    {
        var proxyConnection = new ScriptedConnection(Encoding.Latin1.GetBytes("HTTP/1.1 403 Forbidden\r\nContent-Length: 3\r\n\r\nno\n"));
        var dialer = new FakeTcpDialer { DialOutcome = _ => proxyConnection };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), new SteppingTimeProvider(100));

        var result = await ConnectLoggedAsync(connector, PlainTarget);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsNotNull(result.Timings);
        Assert.IsTrue(result.Timings.NameResolved > result.Timings.Started, "time_namelookup is not zero.");
        Assert.IsNotNull(result.Timings.Connected);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughAProxyWhoseTunnelStalls_KeepsTheNameLookupTimingAndNoConnectTiming()
    {
        var time = new ManualTimeProvider();
        var connection = new StallingConnection { OnStalled = () => time.Advance(2000) };
        var dialer = new FakeTcpDialer { DialOutcome = _ => connection };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), time, connectTimeout: OneSecond);
        var target = new ConnectTarget("example.com", 80, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Http, "proxy.example", 3128, null) };

        var result = await ConnectLoggedAsync(connector, target);

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.IsNotNull(result.Timings);
        Assert.IsNotNull(result.Timings.NameResolved);
        Assert.IsNull(result.Timings.Connected);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughASocksProxyWhoseHandshakeStalls_KeepsTheNameLookupTimingAndNoConnectTiming()
    {
        var time = new ManualTimeProvider();
        var connection = new StallingConnection { OnStalled = () => time.Advance(2000) };
        var dialer = new FakeTcpDialer { DialOutcome = _ => connection };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), time, connectTimeout: OneSecond);
        var target = new ConnectTarget("example.com", 80, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Socks5, "proxy.example", 1080, null) };

        var result = await ConnectLoggedAsync(connector, target);

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.IsNotNull(result.Timings);
        Assert.IsNotNull(result.Timings.NameResolved);
        Assert.IsNull(result.Timings.Connected);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughAProxyWhoseTunnelStallsAndTheCallerCancels_CarriesTheTimingsOnTheCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var connection = new StallingConnection { OnStalled = cancellation.Cancel };
        var dialer = new FakeTcpDialer { DialOutcome = _ => connection };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), new ManualTimeProvider(), connectTimeout: OneSecond);
        var target = new ConnectTarget("example.com", 80, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Socks5, "proxy.example", 1080, null) };

        var canceled = await Assert.ThrowsAsync<OperationCanceledException>(async () => await connector.ConnectAsync(target, cancellation.Token));

        var timings = canceled.Data[ConnectTimings.CarriedOnCancellationKey] as ConnectTimings;
        Assert.IsNotNull(timings);
        Assert.IsNotNull(timings.NameResolved);
        Assert.IsNull(timings.Connected);
    }
}
