using System.Net;
using System.Net.Sockets;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="TcpConnector" /> through an HTTP proxy with fakes: the proxy is
/// resolved and dialed, CONNECT is sent, and each reply ends as curl 8.21.0 ends it
/// (measured; the commands are in BL-212's Notes).
/// </summary>
public sealed partial class TcpConnectorTests
{
    private static readonly IPAddress ProxyAddress = IPAddress.Parse("192.0.2.10");

    private static readonly ProxyEndpoint HttpProxy = new(ProxyKind.Http, "proxy.example", 3128, null);

    private static readonly ConnectTarget PlainTarget = new("example.com", 80, UseTls: false) { Proxy = HttpProxy };

    [TestMethod]
    public async Task ConnectAsync_WhenTheProxyDoesNotResolve_FailsWithCouldntResolveProxy()
    {
        // curl -x no-such-proxy.invalid:3128 http://example.com/ -> curl: (5) Could not resolve proxy: no-such-proxy.invalid
        var resolver = new FakeDnsResolver();
        var dialer = new FakeTcpDialer();
        var connector = new TcpConnector(resolver, dialer, new FakeTlsProvider(), new ManualTimeProvider());

        var result = await ConnectLoggedAsync(connector, PlainTarget);

        Diagnostics.Assert("exit code", CurlExitCode.CouldntResolveProxy, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntResolveProxy, result.ExitCode);
        Assert.AreEqual("Could not resolve proxy: proxy.example", result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "proxy.example" }, resolver.ResolvedHosts);
        Assert.IsEmpty(dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenAProxyOf300BytesDoesNotResolve_FailsWithCouldntResolveProxyCutTo255Characters()
    {
        // curl -x http://<300 a's>:3128 http://example.com/ -> curl: (5) Could not resolve proxy: <first 230 a's>
        // (measured 2026-09-27 against curl 8.21.0, Schannel, BL-377).
        var proxyHost = new string('a', 300);
        var connector = new TcpConnector(new SystemDnsResolver(), new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider());

        var result = await ConnectLoggedAsync(
            connector,
            PlainTarget with { Proxy = new ProxyEndpoint(ProxyKind.Http, proxyHost, 3128, null) });

        Diagnostics.Assert("exit code", CurlExitCode.CouldntResolveProxy, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntResolveProxy, result.ExitCode);
        Assert.AreEqual("Could not resolve proxy: " + new string('a', 230), result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheProxyRefuses_FailsWithCouldntConnectNamingTargetAndProxy()
    {
        // curl -p -x localhost:1 http://example.com:8080/ ->
        // curl: (7) Failed to connect to example.com:8080 over proxy localhost after 2268 ms: Could not connect to server
        var timeProvider = new ManualTimeProvider();
        var dialer = new FakeTcpDialer
        {
            DialOutcome = _ =>
            {
                timeProvider.Advance(2268);
                throw new SocketException((int)SocketError.ConnectionRefused);
            },
        };
        var connector = new TcpConnector(new FakeDnsResolver(ProxyAddress), dialer, new FakeTlsProvider(), timeProvider);

        var result = await ConnectLoggedAsync(
            connector,
            new ConnectTarget("example.com", 8080, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Http, "localhost", 1, null) });

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(
            "Failed to connect to example.com:8080 over proxy localhost after 2268 ms: Could not connect to server",
            result.ErrorMessage);
        Assert.IsTrue(result.IsConnectionRefused);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(ProxyAddress, 1) }, dialer.DialedEndPoints);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 407 Proxy Authentication Required\r\nContent-Length: 0\r\n\r\n", 407)]
    [DataRow("HTTP/1.1 403 Forbidden\r\n\r\n", 403)]
    [DataRow("HTTP/1.1 300 Odd\r\n\r\n", 300)]
    [DataRow("HTTP/1.1 100 Continue\r\n\r\n", 100)]
    public async Task ConnectAsync_WhenTheProxyAnswersOutside2xx_FailsWithCouldntConnectAndDisposesTheConnection(string reply, int statusCode)
    {
        // curl -p -x 127.0.0.1:18261 http://example.com/ answered 407 -> curl: (7) CONNECT tunnel failed, response 407
        var proxyConnection = new ScriptedConnection(Encoding.Latin1.GetBytes(reply));
        var connector = CreateProxyConnector(proxyConnection, new FakeTlsProvider());

        var result = await ConnectLoggedAsync(connector, PlainTarget);

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual($"CONNECT tunnel failed, response {statusCode}", result.ErrorMessage);
        Assert.IsNull(result.Connection);
        Assert.IsTrue(proxyConnection.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheProxyClosesBeforeItsHeaderBlockEnds_FailsWithRecvError()
    {
        // curl -p -x 127.0.0.1:18268 http://example.com/ answered nothing -> curl: (56) Proxy CONNECT aborted
        var proxyConnection = new ScriptedConnection([]);
        var connector = CreateProxyConnector(proxyConnection, new FakeTlsProvider());

        var result = await ConnectLoggedAsync(connector, PlainTarget);

        Diagnostics.Assert("exit code", CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Proxy CONNECT aborted", result.ErrorMessage);
        Assert.IsTrue(proxyConnection.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenA407ContentLengthIsNotANumber_FailsWithWeirdServerReplyAfterItsLine()
    {
        // Measured (BL-1399): "HTTP/1.1 407 Proxy Auth\r\nContent-Length: abc\r\n\r\n" ->
        // "< Content-Length: abc", "* Unsupported Content-Length value", exit 8.
        var events = new RecordingTransferEvents();
        var proxyConnection = new ScriptedConnection(Encoding.Latin1.GetBytes("HTTP/1.1 407 Proxy Auth\r\nContent-Length: abc\r\nX-After: 1\r\n\r\n"));
        var connector = CreateProxyConnector(proxyConnection, new FakeTlsProvider());

        var result = await ConnectLoggedAsync(connector, PlainTarget with { Events = events });

        Diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("Unsupported Content-Length value", result.ErrorMessage);
        Assert.IsTrue(proxyConnection.IsDisposed);
        CollectionAssert.AreEqual(
            new[] { "< HTTP/1.1 407 Proxy Auth", "< Content-Length: abc", "* Unsupported Content-Length value" },
            events.Transcript.SkipWhile(line => !line.StartsWith("< ", StringComparison.Ordinal)).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheReplyIsTooLarge_FailsWithRecvErrorAndDisposesTheConnection()
    {
        var proxyConnection = new ScriptedConnection(Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nX-Pad: " + new string('a', 16377)));
        var connector = CreateProxyConnector(proxyConnection, new FakeTlsProvider());

        var result = await ConnectLoggedAsync(connector, PlainTarget);

        Diagnostics.Assert("exit code", CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("CONNECT response too large", result.ErrorMessage);
        Assert.IsTrue(proxyConnection.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenReadingTheReplyThrows_DisposesTheConnectionAndRethrows()
    {
        var proxyConnection = new ScriptedConnection([]) { ReadException = new IOException("reset") };
        var connector = CreateProxyConnector(proxyConnection, new FakeTlsProvider());

        var exception = await Assert.ThrowsExactlyAsync<IOException>(
            async () => await ConnectLoggedAsync(connector, PlainTarget));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("proxy connection disposed", true, proxyConnection.IsDisposed);
        Assert.IsTrue(proxyConnection.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenAnIPv6TargetsProxyRefuses_NamesTheTargetWithoutBrackets()
    {
        // curl -p -x localhost:1 http://[::1]:8080/ ->
        // curl: (7) Failed to connect to ::1:8080 over proxy localhost after 2239 ms: Could not connect to server
        var connector = new TcpConnector(new FakeDnsResolver(ProxyAddress), new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider());

        var result = await ConnectLoggedAsync(
            connector,
            new ConnectTarget("::1", 8080, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Http, "localhost", 1, null) });

        Diagnostics.Assert("error message", "Failed to connect to ::1:8080 over proxy localhost after 0 ms: Could not connect to server", result.ErrorMessage);
        Assert.AreEqual(
            "Failed to connect to ::1:8080 over proxy localhost after 0 ms: Could not connect to server",
            result.ErrorMessage);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheProxyAnswers2xx_ReturnsTheTunnelWithTheStatusCode()
    {
        var proxyConnection = new ScriptedConnection(Encoding.Latin1.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n"));
        var dialer = new FakeTcpDialer { DialOutcome = _ => proxyConnection };
        var tlsProvider = new FakeTlsProvider();
        var connector = new TcpConnector(new FakeDnsResolver(ProxyAddress), dialer, tlsProvider, new ManualTimeProvider());

        var result = await ConnectLoggedAsync(connector, PlainTarget);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(proxyConnection, result.Connection);
        Assert.AreEqual(200, result.ProxyConnectResponseCode);
        Assert.IsFalse(proxyConnection.IsDisposed);
        Assert.AreEqual(1, proxyConnection.FlushCount);
        Assert.AreEqual(0, tlsProvider.HandshakeCount);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(ProxyAddress, 3128) }, dialer.DialedEndPoints);
        Assert.AreEqual(
            "CONNECT example.com:80 HTTP/1.1\r\nHost: example.com:80\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n",
            Encoding.Latin1.GetString([.. proxyConnection.Written]));
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughATunnel_ReportsNoMappedDestinationSoTheOriginIsNamedLeftIntact()
    {
        // curl -v -p -x http://127.0.0.1:18536 http://example.invalid:8080/ ->
        // * Connection #0 to host example.invalid:8080 left intact (the origin, not the proxy; BL-1074)
        var proxyConnection = new ScriptedConnection(Encoding.Latin1.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n"));
        var connector = CreateProxyConnector(proxyConnection, new FakeTlsProvider());

        var result = await ConnectLoggedAsync(connector, PlainTarget);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.MappedHost);
        Assert.AreEqual(0, result.MappedPort);
    }

    [TestMethod]
    public async Task ConnectAsync_WithTunnelOptions_SendsTheirUserAgent()
    {
        var proxyConnection = new ScriptedConnection(Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\n\r\n"));
        var connector = new TcpConnector(
            new FakeDnsResolver(ProxyAddress),
            new FakeTcpDialer { DialOutcome = _ => proxyConnection },
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            new HttpProxyTunnelOptions("Agent/1", Encoding.UTF8));

        await ConnectLoggedAsync(connector, PlainTarget);

        Diagnostics.Assert("User-Agent header sent", true, Encoding.Latin1.GetString([.. proxyConnection.Written]).Contains("\r\nUser-Agent: Agent/1\r\n", StringComparison.Ordinal));
        StringAssert.Contains(Encoding.Latin1.GetString([.. proxyConnection.Written]), "\r\nUser-Agent: Agent/1\r\n");
    }

    [TestMethod]
    public async Task ConnectAsync_WithUseTls_RunsTlsOverTheTunnelForTheTargetHost()
    {
        var proxyConnection = new ScriptedConnection(Encoding.Latin1.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n"));
        var tlsProvider = new FakeTlsProvider();
        var connector = CreateProxyConnector(proxyConnection, tlsProvider);

        var result = await ConnectLoggedAsync(
            connector,
            new ConnectTarget("example.com", 443, UseTls: true) { Proxy = HttpProxy });

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(tlsProvider.SecuredConnection, result.Connection);
        Assert.AreEqual(200, result.ProxyConnectResponseCode);
        Assert.AreSame(proxyConnection, tlsProvider.ReceivedPlaintext);
        Assert.AreEqual("example.com", tlsProvider.ReceivedTargetHost);
        StringAssert.StartsWith(Encoding.Latin1.GetString([.. proxyConnection.Written]), "CONNECT example.com:443 HTTP/1.1\r\n");
    }

    [TestMethod]
    public async Task ConnectAsync_WithUseTls_WhenTheHandshakeOverTheTunnelFails_ReturnsTheProvidersFailure()
    {
        var failure = ConnectResult.Failed(CurlExitCode.SslConnectError, "x");
        var proxyConnection = new ScriptedConnection(Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\n\r\n"));
        var connector = CreateProxyConnector(proxyConnection, new FakeTlsProvider { FailureToReturn = failure });

        var result = await ConnectLoggedAsync(
            connector,
            new ConnectTarget("example.com", 443, UseTls: true) { Proxy = HttpProxy });

        Diagnostics.Assert("exit code", failure.ExitCode, result.ExitCode);
        Assert.AreEqual(failure.ExitCode, result.ExitCode);
        Assert.AreEqual(failure.ErrorMessage, result.ErrorMessage);
    }

    private static TcpConnector CreateProxyConnector(ScriptedConnection proxyConnection, FakeTlsProvider tlsProvider) =>
        new(
            new FakeDnsResolver(ProxyAddress),
            new FakeTcpDialer { DialOutcome = _ => proxyConnection },
            tlsProvider,
            new ManualTimeProvider());
}
