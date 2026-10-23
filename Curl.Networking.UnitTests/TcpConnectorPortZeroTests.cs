using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Connects to port 0, which no server listens on: curl 8.22.0 fails each one with exit 7 and
/// <c>Could not connect to server</c>, directly, through an HTTP forward proxy and through a
/// tunnelling proxy, and Curl dials no socket for it (measured 2026-10-22, BL-3172 Notes).
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    [DataRow("127.0.0.1", false, DisplayName = "http, IPv4 literal")]
    [DataRow("localhost", false, DisplayName = "http, host name")]
    [DataRow("::1", false, DisplayName = "http, IPv6 literal")]
    [DataRow("127.0.0.1", true, DisplayName = "https")]
    public async Task ConnectAsync_ToPortZero_FailsWithExit7WithoutDialling(string host, bool useTls)
    {
        // curl -sS http://127.0.0.1:0/ (and localhost, [::1], https://, ftp://; 8.22.0) ->
        // curl: (7) Failed to connect to 127.0.0.1:0 after 0 ms: Could not connect to server
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = CreateConnector(new FakeDnsResolver(IPAddress.IPv6Loopback, Loopback), dialer, new FakeTlsProvider());

        var result = await ConnectLoggedAsync(connector, new ConnectTarget(host, 0, useTls));

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual($"Failed to connect to {host}:0 after 0 ms: Could not connect to server", result.ErrorMessage);
        Assert.IsEmpty(dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_ToAForwardProxyOnPortZero_FailsWithExit7OverProxy()
    {
        // curl -sS -x http://127.0.0.1:0 http://example.test/ (8.22.0) ->
        // curl: (7) Failed to connect to 127.0.0.1:0 over proxy 127.0.0.1 after 0 ms: Could not connect to server
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = CreateConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider());

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 0, UseTls: false) { IsForwardProxy = true });

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to 127.0.0.1:0 over proxy 127.0.0.1 after 0 ms: Could not connect to server", result.ErrorMessage);
        Assert.IsEmpty(dialer.DialedEndPoints);
    }

    [TestMethod]
    [DataRow(ProxyKind.Socks5, DisplayName = "SOCKS5 proxy")]
    [DataRow(ProxyKind.Http, DisplayName = "HTTP tunnelling proxy")]
    public async Task ConnectAsync_ThroughAProxyOnPortZero_FailsWithExit7NamingTheTarget(ProxyKind kind)
    {
        // curl -sS -x socks5://127.0.0.1:0 http://example.test/ (8.22.0) ->
        // curl: (7) Failed to connect to example.test:80 over proxy 127.0.0.1 after 0 ms: Could not connect to server
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = CreateConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider());
        var target = new ConnectTarget("example.test", 80, UseTls: false) { Proxy = new ProxyEndpoint(kind, "127.0.0.1", 0, null) };

        var result = await ConnectLoggedAsync(connector, target);

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to example.test:80 over proxy 127.0.0.1 after 0 ms: Could not connect to server", result.ErrorMessage);
        Assert.IsEmpty(dialer.DialedEndPoints);
    }
}
