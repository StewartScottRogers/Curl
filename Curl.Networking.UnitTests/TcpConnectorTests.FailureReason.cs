using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins how <see cref="TcpConnector" /> reports a resolver's failure reason, as curl 8.22.0's
/// c-ares build prints it with <c>--dns-servers</c> (measured, BL-643 and BL-694).
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_AHostTheResolverExplains_AddsTheReason()
    {
        // curl --dns-servers <silent> http://bl694.example:1/ -> curl: (6) Could not resolve host: bl694.example (Timeout while contacting DNS servers)
        var connector = CreateExplainedConnector(new ReasoningDnsResolver(new DnsResolution([], DnsLookupFailure.Timeout)), new FakeTcpDialer());

        var result = await connector.ConnectAsync(new ConnectTarget("bl694.example", 1, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: bl694.example (Timeout while contacting DNS servers)", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_ABadDnsConfiguration_FailsWithExit43()
    {
        // curl --dns-servers bogus http://bl643.example:1/ -> curl: (43) Error 43 resolving bl643.example:1
        var connector = CreateExplainedConnector(new ReasoningDnsResolver(new DnsResolution([], DnsLookupFailure.BadConfiguration)), new FakeTcpDialer());

        var result = await connector.ConnectAsync(new ConnectTarget("bl643.example", 1, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.BadFunctionArgument, result.ExitCode);
        Assert.AreEqual("Error 43 resolving bl643.example:1", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_AProxyTheResolverExplains_AddsTheReason()
    {
        var connector = CreateExplainedConnector(new ReasoningDnsResolver(new DnsResolution([], DnsLookupFailure.NotFound)), new FakeTcpDialer());
        var target = new ConnectTarget("example.com", 80, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Http, "bar", 47500, null) };

        var result = await connector.ConnectAsync(target, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveProxy, result.ExitCode);
        Assert.AreEqual("Could not resolve proxy: bar (Domain name not found)", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_AHostTheExplainingResolverResolves_DialsItsAddress()
    {
        var dialer = new FakeTcpDialer();
        var connector = CreateExplainedConnector(new ReasoningDnsResolver(new DnsResolution([Loopback], DnsLookupFailure.None)), dialer);

        await connector.ConnectAsync(new ConnectTarget("bl694.example", 47500, UseTls: false), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { new IPEndPoint(Loopback, 47500) }, dialer.DialedEndPoints);
    }

    [TestMethod]
    [DataRow(ProxyKind.Socks4)]
    [DataRow(ProxyKind.Socks5)]
    public async Task ConnectAsync_ThroughALocallyResolvingSocksProxyToAHostTheResolverExplains_AddsTheReason(ProxyKind kind)
    {
        // curl -x socks5://172.26.96.1:15380 --dns-servers <NXDOMAIN> http://bl829.example:1/ ->
        // curl: (6) Could not resolve host: bl829.example (Domain name not found), socks4 alike (BL-829)
        var (result, proxyConnection) = await ConnectThroughExplainedSocksAsync(kind, DnsLookupFailure.NotFound);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: bl829.example (Domain name not found)", result.ErrorMessage);
        Assert.IsTrue(proxyConnection.IsDisposed);
    }

    [TestMethod]
    [DataRow(ProxyKind.Socks4)]
    [DataRow(ProxyKind.Socks5)]
    public async Task ConnectAsync_ThroughALocallyResolvingSocksProxyWithABadDnsConfiguration_FailsWithExit43(ProxyKind kind)
    {
        // curl -x socks4://172.26.96.1:15380 --dns-servers bogus http://bl829.example:1/ ->
        // curl: (43) Error 43 resolving bl829.example:1, socks5 alike (BL-829)
        var (result, proxyConnection) = await ConnectThroughExplainedSocksAsync(kind, DnsLookupFailure.BadConfiguration);

        Assert.AreEqual(CurlExitCode.BadFunctionArgument, result.ExitCode);
        Assert.AreEqual("Error 43 resolving bl829.example:1", result.ErrorMessage);
        Assert.IsTrue(proxyConnection.IsDisposed);
    }

    private static async Task<(ConnectResult Result, ScriptedConnection ProxyConnection)> ConnectThroughExplainedSocksAsync(ProxyKind kind, DnsLookupFailure failure)
    {
        var proxyConnection = new ScriptedConnection([0x05, 0x00]);
        var connector = new TcpConnector(
            new ReasoningDnsResolver(new DnsResolution([], failure)),
            new FakeTcpDialer { DialOutcome = _ => proxyConnection },
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            resolveOverrides: ResolveOverrides.Parse(["socks.example:1080:192.0.2.10"]));

        var result = await connector.ConnectAsync(
            new ConnectTarget("bl829.example", 1, UseTls: false) { Proxy = new ProxyEndpoint(kind, "socks.example", 1080, null) },
            CancellationToken.None);
        return (result, proxyConnection);
    }

    private static TcpConnector CreateExplainedConnector(ReasoningDnsResolver resolver, FakeTcpDialer dialer) =>
        new(resolver, dialer, new FakeTlsProvider(), new ManualTimeProvider());
}
