using System.Text;
using Curl.Testing;

namespace Curl.Conformance;

/// <summary>
/// Shows that a case's proxy tunnel and HAProxy PROXY line are written by Curl's own
/// <c>TcpConnector</c> code, not by the <c>sws</c> stand-in: curl runs as the command composes it,
/// and only the final dial reaches the in-memory server through <see cref="InMemoryServerTcpDialer"/>
/// (ADR-0460).
/// </summary>
[TestClass]
public sealed class InMemoryServerTcpDialerTests
{
    private const string Reply =
        "<reply>\n<data>\nHTTP/1.1 200 OK\nContent-Length: 3\n\nhi\n</data>\n"
        + "<connect>\nHTTP/1.1 200 Mighty fine indeed\n\n</connect>\n</reply>\n";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ProxyTunnel_SendsCurlsConnectRequestToTheProxyStandIn()
    {
        SwsHttpServerConnector server = new(ParsedTestCase.From(Reply));

        int exitCode = await RunCurlAsync(server, "-p", "-x", $"http://127.0.0.1:{SwsHttpServerConnector.ProxyPort}", "http://tunnel.example:8990/1");

        Assert.AreEqual(0, exitCode);
        string connect = Observe("proxy bytes", "CONNECT tunnel.example:8990 HTTP/1.1\r\n...", Text(server.ProxyReceivedBytes));
        Assert.IsTrue(connect.StartsWith("CONNECT tunnel.example:8990 HTTP/1.1\r\nHost: tunnel.example:8990\r\n", StringComparison.Ordinal), connect);
        string tunnelled = Observe("tunnelled bytes", "GET /1 HTTP/1.1\r\n...", Text(server.ReceivedBytes));
        Assert.IsTrue(tunnelled.StartsWith("GET /1 HTTP/1.1\r\nHost: tunnel.example:8990\r\n", StringComparison.Ordinal), tunnelled);
    }

    [TestMethod]
    public async Task HaproxyProtocol_WritesTheProxyLineFirstOnTheConnection()
    {
        SwsHttpServerConnector server = new(ParsedTestCase.From(Reply));

        int exitCode = await RunCurlAsync(server, "--haproxy-protocol", "http://127.0.0.1:8990/1");

        Assert.AreEqual(0, exitCode);
        string received = Observe("received bytes", "PROXY TCP4 127.0.0.1 127.0.0.1 ...", Text(server.ReceivedBytes));
        Assert.IsTrue(received.StartsWith("PROXY TCP4 127.0.0.1 127.0.0.1 ", StringComparison.Ordinal), received);
        Assert.IsTrue(received.Contains(" 8990\r\nGET /1 HTTP/1.1\r\n", StringComparison.Ordinal), received);
    }

    [TestMethod]
    public async Task ProxyTunnelWithHaproxyProtocol_WritesTheProxyLineInsideTheTunnel()
    {
        SwsHttpServerConnector server = new(ParsedTestCase.From(Reply));

        int exitCode = await RunCurlAsync(server, "-p", "--haproxy-protocol", "-x", $"http://127.0.0.1:{SwsHttpServerConnector.ProxyPort}", "http://tunnel.example:8990/1");

        Assert.AreEqual(0, exitCode);
        string proxyBytes = Observe("proxy bytes", "CONNECT tunnel.example:8990 HTTP/1.1\r\n...", Text(server.ProxyReceivedBytes));
        Assert.IsTrue(proxyBytes.StartsWith("CONNECT tunnel.example:8990 HTTP/1.1\r\n", StringComparison.Ordinal), proxyBytes);
        string tunnelled = Observe("tunnelled bytes", "PROXY TCP4 ...\r\nGET /1 HTTP/1.1\r\n...", Text(server.ReceivedBytes));
        Assert.IsTrue(tunnelled.StartsWith("PROXY TCP4 127.0.0.1 127.0.0.1 ", StringComparison.Ordinal), tunnelled);
        Assert.IsTrue(tunnelled.Contains("\r\nGET /1 HTTP/1.1\r\n", StringComparison.Ordinal), tunnelled);
    }

    private static async Task<int> RunCurlAsync(SwsHttpServerConnector server, params string[] arguments)
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        using MemoryStream standardInput = new();
        return await UpstreamConformanceTests.RunCurlAsync(
            new UpstreamCurlInvocation(arguments, standardOutput, standardError, standardInput, server, new UnreachableDatagramConnector()));
    }

    // Writes the ACT, ASSERT and DIFF lines for a string the test is about to assert, and returns it.
    private string Observe(string label, string expected, string actual)
    {
        TestDiagnostics diagnostics = Diagnostics;
        diagnostics.Act(label, actual);
        diagnostics.Assert(label, expected, actual);
        diagnostics.Diff(label, expected, actual);
        return actual;
    }

    private static string Text(ReadOnlyMemory<byte> bytes) => Encoding.Latin1.GetString(bytes.Span);
}
