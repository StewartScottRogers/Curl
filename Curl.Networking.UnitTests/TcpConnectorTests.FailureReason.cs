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

    private static TcpConnector CreateExplainedConnector(ReasoningDnsResolver resolver, FakeTcpDialer dialer) =>
        new(resolver, dialer, new FakeTlsProvider(), new ManualTimeProvider());
}
