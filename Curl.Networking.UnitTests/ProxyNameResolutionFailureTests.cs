using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="TcpConnector" /> to exit 5 <c>Could not resolve proxy: &lt;name&gt;</c> when the proxy's
/// own name does not resolve, for every proxy form, and to exit 6 <c>Could not resolve host:</c> when only
/// the target's does, as curl 8.22.0 (Schannel) reports them (measured on 2026-10-22 with
/// <c>Record-CurlExchange.ps1</c>, BL-3168 Notes).
/// </summary>
[TestClass]
public sealed class ProxyNameResolutionFailureTests
{
    private static readonly IPAddress WorkingAddress = IPAddress.Parse("192.0.2.30");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ConnectAsync_ToAForwardProxyThatDoesNotResolve_IsCouldntResolveProxy()
    {
        // curl -sS -x badproxy.invalid http://x/ -> curl: (5) Could not resolve proxy: badproxy.invalid
        var result = await ConnectAsync(new ConnectTarget("badproxy.invalid", 1080, UseTls: false) { PoolScheme = "http", IsForwardProxy = true });

        AssertProxyNotResolved(result);
    }

    [TestMethod]
    public async Task ConnectAsync_ToAnHttp10ForwardProxyThatDoesNotResolve_IsCouldntResolveProxy()
    {
        // curl -sS --proxy1.0 badproxy.invalid http://x/ -> curl: (5) Could not resolve proxy: badproxy.invalid
        var result = await ConnectAsync(new ConnectTarget("badproxy.invalid", 1080, UseTls: false) { PoolScheme = "http", IsForwardProxy = true });

        AssertProxyNotResolved(result);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughASocks5ProxyThatDoesNotResolve_IsCouldntResolveProxy()
    {
        // curl -sS --socks5 badproxy.invalid http://x/ -> curl: (5) Could not resolve proxy: badproxy.invalid
        var result = await ConnectAsync(new ConnectTarget("x", 80, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Socks5, "badproxy.invalid", 1080, null) });

        AssertProxyNotResolved(result);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughAPreProxyThatDoesNotResolve_IsCouldntResolveProxy()
    {
        // curl -sS --preproxy socks5://badproxy.invalid -x http://127.0.0.1:1 http://x/ -> curl: (5) Could not resolve proxy: badproxy.invalid
        var preProxy = new ProxyEndpoint(ProxyKind.Socks5, "badproxy.invalid", 1080, null);

        var result = await ConnectAsync(new ConnectTarget("127.0.0.1", 1, UseTls: false) { PoolScheme = "http", IsForwardProxy = true }, preProxy);

        AssertProxyNotResolved(result);
    }

    [TestMethod]
    public async Task ConnectAsync_ToATargetThatDoesNotResolveBehindAWorkingSocks4Proxy_IsCouldntResolveHost()
    {
        // curl -x socks4://127.0.0.1:19080 http://nosuch.invalid:8080/ -> curl: (6) Could not resolve host: nosuch.invalid
        // (and curl 8.22.0 -x http://127.0.0.1:1 --noproxy x.invalid http://x.invalid/ -> curl: (6) Could not resolve host: x.invalid)
        var proxyConnection = new ScriptedConnection([]);
        var resolver = new FakeDnsResolver(WorkingAddress) { HostsWithNoAddress = new HashSet<string> { "nosuch.invalid" } };
        var connector = new TcpConnector(resolver, new FakeTcpDialer { DialOutcome = _ => proxyConnection }, new FakeTlsProvider(), new ManualTimeProvider());
        var target = new ConnectTarget("nosuch.invalid", 8080, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Socks4, "proxy.example", 1080, null) };
        Diagnostics.Arrange("target", target);

        var result = await connector.ConnectAsync(target, CancellationToken.None);

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("message", result.ErrorMessage ?? string.Empty);
        Diagnostics.Assert("exit code", CurlExitCode.CouldntResolveHost, result.ExitCode);
        Diagnostics.Diff("message", "Could not resolve host: nosuch.invalid", result.ErrorMessage ?? string.Empty);
        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: nosuch.invalid", result.ErrorMessage);
    }

    private async Task<ConnectResult> ConnectAsync(ConnectTarget target, ProxyEndpoint? preProxy = null)
    {
        Diagnostics.Arrange("target", target);
        Diagnostics.Arrange("pre-proxy", preProxy);
        var resolver = new FakeDnsResolver(WorkingAddress) { HostsWithNoAddress = new HashSet<string> { "badproxy.invalid" } };
        var connector = new TcpConnector(resolver, new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider(), preProxy: preProxy);

        var result = await connector.ConnectAsync(target, CancellationToken.None);

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("message", result.ErrorMessage ?? string.Empty);
        return result;
    }

    private void AssertProxyNotResolved(ConnectResult result)
    {
        Diagnostics.Assert("exit code", CurlExitCode.CouldntResolveProxy, result.ExitCode);
        Diagnostics.Diff("message", "Could not resolve proxy: badproxy.invalid", result.ErrorMessage ?? string.Empty);
        Assert.AreEqual(CurlExitCode.CouldntResolveProxy, result.ExitCode);
        Assert.AreEqual("Could not resolve proxy: badproxy.invalid", result.ErrorMessage);
    }
}
