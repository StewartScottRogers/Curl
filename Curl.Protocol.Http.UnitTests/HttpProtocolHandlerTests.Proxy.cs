using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Drives <see cref="HttpProtocolHandler" /> through a forward proxy with
/// <see cref="OriginAndProxyAuthenticator" />, <see cref="TurnTakingConnection" /> and
/// <see cref="QueueConnector" />, never a socket. Every request is what curl 8.21.0 sent a
/// loopback proxy; the commands are in the BL-183 Notes. Each exchange is replayed with 1-byte
/// reads and with one read.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string ProxyOkHead = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\n";

    private static readonly ProxyEndpoint LoopbackProxy = new(ProxyKind.Http, "127.0.0.1", 18183, new NetworkCredential("u", "p"));

    /// <summary>
    /// Measured: <c>curl -x http://127.0.0.1:18183 -U u:p "http://Example.com/a/b?c=d"</c>
    /// connects to the proxy and sends the absolute form with <c>Proxy-Authorization</c> after
    /// <c>Host</c> and <c>Proxy-Connection: Keep-Alive</c> last.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_HttpThroughProxy_SendsAbsoluteFormToTheProxy()
    {
        const string expected = "GET http://Example.com/a/b?c=d HTTP/1.1\r\nHost: Example.com\r\nProxy-Authorization: Basic dTpw\r\n"
            + "User-Agent: curl/8.21.0\r\nAccept: */*\r\nProxy-Connection: Keep-Alive\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, ProxyOkHead + "ok");
            QueueConnector connector = QueueConnector.For(connection);
            OriginAndProxyAuthenticator authenticator = new(null, null, "Basic dTpw");
            MemoryStream output = new();
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("proxy", "http://127.0.0.1:18183, credential u:p");

            TransferResult result = await new HttpProtocolHandler(connector, authenticator)
                .ExecuteAsync(ProxyContext("http://Example.com/a/b?c=d", output, new HttpRequestOptions { ForwardProxy = LoopbackProxy }));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Diagnostics.Diff("request written", OneLine(expected), OneLine(connection.Written));
            Assert.AreEqual(expected, connection.Written, $"Chunk size {chunkSize}");
            Diagnostics.Assert("connect target", "127.0.0.1:18183 forward proxy", connector.Targets.Single());
            Assert.AreEqual("ok", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(new ConnectTarget("127.0.0.1", 18183, false) { PoolScheme = "http", IsForwardProxy = true }, connector.Targets.Single(), $"Chunk size {chunkSize}");
            Assert.IsTrue(result.Report!.UsedProxy, $"Chunk size {chunkSize}");
            HttpAuthRequest proxyRequest = authenticator.Calls.Single(call => call.Request.IsProxy).Request;
            Assert.AreEqual(
                new HttpAuthRequest("GET", CurlUrl.Parse("http://127.0.0.1:18183/"), "/a/b?c=d", LoopbackProxy.Credential, null, HttpAuthSchemes.Basic, true),
                proxyRequest with { Events = NoTransferEvents.Instance },
                $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: <c>curl -sS -x http://127.0.0.1:18332 ftp://example.com/f.txt</c> forwards the
    /// <c>ftp</c> URL to the proxy as an HTTP GET with <c>:21</c> on <c>Host</c>, and the proxy's
    /// <c>200</c> body <c>hello</c> is the output, exit 0 (BL-330 Notes, ADR-0056).
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_FtpThroughProxy_ForwardsAGetAndWritesTheProxysBody()
    {
        const string expected = "GET ftp://example.com/f.txt HTTP/1.1\r\nHost: example.com:21\r\nUser-Agent: curl/8.21.0\r\n"
            + "Accept: */*\r\nProxy-Connection: Keep-Alive\r\n\r\n";
        ProxyEndpoint proxy = new(ProxyKind.Http, "127.0.0.1", 18332, null);
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello");
            QueueConnector connector = QueueConnector.For(connection);
            MemoryStream output = new();
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("url", "ftp://example.com/f.txt via http://127.0.0.1:18332");

            TransferResult result = await new HttpProtocolHandler(connector, new OriginAndProxyAuthenticator(null, null, null))
                .ExecuteAsync(ProxyContext("ftp://example.com/f.txt", output, new HttpRequestOptions { ForwardProxy = proxy }));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Diagnostics.Diff("request written", OneLine(expected), OneLine(connection.Written));
            Assert.AreEqual(expected, connection.Written, $"Chunk size {chunkSize}");
            Diagnostics.Assert("output", "hello", Latin1(output.ToArray()));
            Assert.AreEqual("hello", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(new ConnectTarget("127.0.0.1", 18332, false) { PoolScheme = "http", IsForwardProxy = true }, connector.Targets.Single(), $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: <c>curl -x http://127.0.0.1:18296 --proxy-header "X-P: 1" -H "X-A: 1"
    /// http://example.com/</c> sends the <c>--proxy-header</c> value after the <c>-H</c> value
    /// (BL-296 Notes).
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_HttpThroughProxyWithProxyHeaders_SendsThemAfterTheCustomHeaders()
    {
        const string expected = "GET http://example.com/ HTTP/1.1\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Proxy-Connection: Keep-Alive\r\nX-A: 1\r\nX-P: 1\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, ProxyOkHead + "ok");
            QueueConnector connector = QueueConnector.For(connection);
            HttpRequestOptions options = new() { ForwardProxy = LoopbackProxy, Headers = ["X-A: 1"], ProxyHeaders = ["X-P: 1"] };
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("headers", "-H X-A: 1, --proxy-header X-P: 1");

            TransferResult result = await new HttpProtocolHandler(connector, new OriginAndProxyAuthenticator(null, null, null))
                .ExecuteAsync(ProxyContext("http://example.com/", new MemoryStream(), options));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Diagnostics.Diff("request written", OneLine(expected), OneLine(connection.Written));
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(expected, connection.Written, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: <c>curl -x http://127.0.0.1:18183 -U u:p -u a:b
    /// "http://x:y@EXample.com:80/A%20b?q#frag"</c> sends <c>Proxy-Authorization</c> before
    /// <c>Authorization</c>, and drops the user information, the default port and the fragment
    /// from the absolute form.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ProxyAndOriginCredentials_SendsProxyAuthorizationFirst()
    {
        const string expected = "GET http://EXample.com/A%20b?q HTTP/1.1\r\nHost: EXample.com\r\nProxy-Authorization: Basic dTpw\r\n"
            + "Authorization: Basic YTpi\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nProxy-Connection: Keep-Alive\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, ProxyOkHead + "ok");
            OriginAndProxyAuthenticator authenticator = new("Basic YTpi", null, "Basic dTpw");
            TransferContext context = new()
            {
                Url = CurlUrl.Parse("http://x:y@EXample.com:80/A%20b?q#frag"),
                Output = new MemoryStream(),
                Credentials = new NetworkCredential("a", "b"),
                Http = new HttpRequestOptions { ForwardProxy = LoopbackProxy },
            };
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("url", context.Url);
            Diagnostics.Arrange("credentials", "origin a:b, proxy u:p");

            TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(context);

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Diagnostics.Diff("request written", OneLine(expected), OneLine(connection.Written));
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(expected, connection.Written, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: <c>curl -x http://127.0.0.1:18183 -u a:b -d xy http://example.com/</c> sends
    /// the body headers after <c>Proxy-Connection</c>, and no <c>Proxy-Authorization</c> when
    /// the proxy has no credential.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_PostThroughProxyWithoutProxyCredential_SendsNoProxyAuthorization()
    {
        const string expected = "POST http://example.com/ HTTP/1.1\r\nHost: example.com\r\nAuthorization: Basic YTpi\r\n"
            + "User-Agent: curl/8.21.0\r\nAccept: */*\r\nProxy-Connection: Keep-Alive\r\nContent-Length: 2\r\n"
            + "Content-Type: application/x-www-form-urlencoded\r\n\r\nxy";
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, ProxyOkHead + "ok");
            OriginAndProxyAuthenticator authenticator = new("Basic YTpi", null, null);
            HttpRequestOptions options = new()
            {
                ForwardProxy = LoopbackProxy with { Credential = null },
                Body = new BytesBody("xy"u8.ToArray(), "application/x-www-form-urlencoded"),
            };
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("request", "POST xy, origin a:b, proxy without credential");

            TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator)
                .ExecuteAsync(ProxyContext("http://example.com/", new MemoryStream(), options));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Diagnostics.Diff("request written", OneLine(expected), OneLine(connection.Written));
            Assert.AreEqual(expected, connection.Written, $"Chunk size {chunkSize}");
            Diagnostics.Assert("proxy credential asked about", null, authenticator.Calls.Single(call => call.Request.IsProxy).Request.Credential);
            Assert.IsNull(authenticator.Calls.Single(call => call.Request.IsProxy).Request.Credential, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: <c>curl --proxy1.0 127.0.0.1:18183 -I http://example.com/h</c> forwards the
    /// request as an HTTP/1.1 proxy does; the HTTP/1.0 kind changes only CONNECT.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_Http10Proxy_ForwardsLikeAnHttp11Proxy()
    {
        const string expected = "HEAD http://example.com/h HTTP/1.1\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Proxy-Connection: Keep-Alive\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, ProxyOkHead);
            QueueConnector connector = QueueConnector.For(connection);
            HttpRequestOptions options = new() { ForwardProxy = new ProxyEndpoint(ProxyKind.Http10, "127.0.0.1", 18183, null) };
            TransferContext context = new() { Url = CurlUrl.Parse("http://example.com/h"), Output = new MemoryStream(), NoBody = true, Http = options };
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("proxy", "HTTP/1.0 proxy 127.0.0.1:18183, -I");

            TransferResult result = await Handler(connector).ExecuteAsync(context);

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Diagnostics.Diff("request written", OneLine(expected), OneLine(connection.Written));
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(expected, connection.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual(new ConnectTarget("127.0.0.1", 18183, false) { PoolScheme = "http", IsForwardProxy = true }, connector.Targets.Single(), $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// An HTTPS proxy is forwarded to like an HTTP one, over a TLS connection to the proxy.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_HttpsProxyForHttpUrl_ConnectsToTheProxyWithTls()
    {
        TurnTakingConnection connection = new(65536, ProxyOkHead + "ok");
        QueueConnector connector = QueueConnector.For(connection);
        HttpRequestOptions options = new() { ForwardProxy = new ProxyEndpoint(ProxyKind.Https, "proxy.example", 443, null) };
        Diagnostics.Arrange("proxy", "https://proxy.example:443");

        TransferResult result = await Handler(connector).ExecuteAsync(ProxyContext("http://example.com/", new MemoryStream(), options));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("connect target", "proxy.example:443 with TLS", connector.Targets.Single());
        Diagnostics.Act("request written", OneLine(connection.Written));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(new ConnectTarget("proxy.example", 443, true) { PoolScheme = "http", IsForwardProxy = true }, connector.Targets.Single());
        Assert.StartsWith("GET http://example.com/ HTTP/1.1\r\n", connection.Written);
    }

    /// <summary>
    /// Measured: <c>curl -s -x http://127.0.0.1:18183 -U u:p --digest -u u:p
    /// http://127.0.0.1:18184/a</c> answers the 401 with a second request that still carries
    /// the absolute form, <c>Proxy-Authorization</c> and <c>Proxy-Connection</c>.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_AuthenticationRetryThroughProxy_KeepsTheProxyHeaders()
    {
        const string first = "GET http://127.0.0.1:18184/a HTTP/1.1\r\nHost: 127.0.0.1:18184\r\nProxy-Authorization: Basic dTpw\r\n"
            + "User-Agent: curl/8.21.0\r\nAccept: */*\r\nProxy-Connection: Keep-Alive\r\n\r\n";
        const string second = "GET http://127.0.0.1:18184/a HTTP/1.1\r\nHost: 127.0.0.1:18184\r\nProxy-Authorization: Basic dTpw\r\n"
            + "Authorization: " + DigestValue + "\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nProxy-Connection: Keep-Alive\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, ChallengeHead + "nope", ProxyOkHead + "ok");
            OriginAndProxyAuthenticator authenticator = new(null, DigestValue, "Basic dTpw");
            MemoryStream output = new();
            TransferContext context = ProxyContext("http://127.0.0.1:18184/a", output, new HttpRequestOptions { ForwardProxy = LoopbackProxy });
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("scripted responses", "401 Digest challenge, 200 ok; proxy u:p");

            TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(context);

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Diagnostics.Diff("requests written", OneLine(first + second), OneLine(connection.Written));
            Diagnostics.Assert("output", "ok", Latin1(output.ToArray()));
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(first + second, connection.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual("ok", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// An <c>https</c> URL, <c>-p</c>, or a SOCKS proxy is tunnelled through by the connector:
    /// the handler asks it for the origin with <see cref="ConnectTarget.Proxy" /> set and sends
    /// the origin form, with no proxy header, no <c>--proxy-header</c> value and no proxy
    /// authorization asked for.
    /// </summary>
    [TestMethod]
    [DataRow("https://example.com/a?b", ProxyKind.Http, false, "example.com", 443, true, DisplayName = "https URL via HTTP proxy")]
    [DataRow("http://example.com/a?b", ProxyKind.Http, true, "example.com", 80, false, DisplayName = "-p with http URL")]
    [DataRow("http://example.com/a?b", ProxyKind.Socks5, false, "example.com", 80, false, DisplayName = "SOCKS5 proxy")]
    [DataRow("http://example.com/a?b", ProxyKind.Socks4, false, "example.com", 80, false, DisplayName = "SOCKS4 proxy")]
    public async Task ExecuteAsync_TunnelledProxy_PassesTheProxyToTheConnectorAndSendsOriginForm(
        string url,
        ProxyKind kind,
        bool proxyTunnel,
        string host,
        int port,
        bool useTls)
    {
        const string expected = "GET /a?b HTTP/1.1\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            ProxyEndpoint proxy = new(kind, "127.0.0.1", 18183, new NetworkCredential("u", "p"));
            TurnTakingConnection connection = new(chunkSize, ProxyOkHead + "ok");
            QueueConnector connector = QueueConnector.For(connection);
            OriginAndProxyAuthenticator authenticator = new(null, null, "Basic dTpw");
            HttpRequestOptions options = new() { ForwardProxy = proxy, ProxyTunnel = proxyTunnel, ProxyHeaders = ["X-P: 1"] };
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("url", url);
            Diagnostics.Arrange("proxy", $"{kind}, tunnel {proxyTunnel}");

            TransferResult result = await new HttpProtocolHandler(connector, authenticator)
                .ExecuteAsync(ProxyContext(url, new MemoryStream(), options));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Diagnostics.Diff("request written", OneLine(expected), OneLine(connection.Written));
            Diagnostics.Assert("connect target", $"{host}:{port} tls {useTls}", connector.Targets.Single());
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(expected, connection.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual(new ConnectTarget(host, port, useTls) { Proxy = proxy, PoolScheme = useTls ? "https" : "http" }, connector.Targets.Single(), $"Chunk size {chunkSize}");
            Assert.IsFalse(authenticator.Calls.Any(call => call.Request.IsProxy), $"Chunk size {chunkSize}");
            Assert.IsTrue(result.Report!.UsedProxy, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured (BL-1074): through a CONNECT tunnel or a SOCKS proxy, curl 8.21.0 names the origin,
    /// not the proxy, in its last <c>-v</c> line: <c>-p -x http://127.0.0.1:18536
    /// http://example.invalid:8080/</c> ends <c>* Connection #0 to host example.invalid:8080 left
    /// intact</c>, and <c>--socks4</c>, <c>--socks5</c> and <c>--socks5-hostname</c> the same.
    /// </summary>
    [TestMethod]
    [DataRow(ProxyKind.Http, true, DisplayName = "-p through an HTTP proxy")]
    [DataRow(ProxyKind.Socks4, false, DisplayName = "SOCKS4 proxy")]
    [DataRow(ProxyKind.Socks5, false, DisplayName = "SOCKS5 proxy")]
    [DataRow(ProxyKind.Socks5Hostname, false, DisplayName = "SOCKS5h proxy")]
    public async Task ExecuteAsync_KeptAliveThroughATunnelledProxy_ReportsTheOriginLeftIntact(ProxyKind kind, bool proxyTunnel)
    {
        RecordingTransferEvents events = new();
        HttpRequestOptions options = new() { ForwardProxy = new ProxyEndpoint(kind, "127.0.0.1", 18536, null), ProxyTunnel = proxyTunnel };
        TransferContext context = new() { Url = CurlUrl.Parse("http://example.invalid:8080/"), Output = new MemoryStream(), Http = options, Events = events };
        Diagnostics.Arrange("proxy", $"{kind}, tunnel {proxyTunnel}");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(new TurnTakingConnection(65536, ProxyOkHead + "ok")), new OriginAndProxyAuthenticator(null, null, null))
            .ExecuteAsync(context);

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("last info line", "Connection #0 to host example.invalid:8080 left intact", events.Info[^1]);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("Connection #0 to host example.invalid:8080 left intact", events.Info[^1]);
    }

    /// <summary>
    /// Measured: <c>curl -s -w "%{proxy_used}" http://127.0.0.1:18081/</c> prints <c>0</c> for
    /// a direct transfer (BL-302 Notes).
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_WithoutProxy_ReportsNoProxyUsed()
    {
        TurnTakingConnection connection = new(int.MaxValue, ProxyOkHead + "ok");
        Diagnostics.Arrange("proxy", "(none)");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new OriginAndProxyAuthenticator(null, null, null))
            .ExecuteAsync(ProxyContext("http://127.0.0.1:18081/", new MemoryStream(), new HttpRequestOptions()));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("proxy used", false, result.Report!.UsedProxy);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsFalse(result.Report!.UsedProxy);
    }

    /// <summary>
    /// Measured: <c>curl -s -x http://127.0.0.1:1 -w "%{proxy_used} %{exitcode}"
    /// http://example.test/</c> prints <c>1 7</c>, and the same through <c>socks5://127.0.0.1:1</c>:
    /// the proxy counts as used even when connecting to it fails (BL-302 Notes).
    /// </summary>
    [TestMethod]
    [DataRow(ProxyKind.Http, DisplayName = "forwarding HTTP proxy")]
    [DataRow(ProxyKind.Socks5, DisplayName = "tunnelling SOCKS5 proxy")]
    public async Task ExecuteAsync_ProxyConnectFails_ReportsTheProxyUsed(ProxyKind kind)
    {
        const string message = "Failed to connect to 127.0.0.1 port 1 after 0 ms: Could not connect to server";
        QueueConnector connector = new(ConnectResult.Failed(CurlExitCode.CouldntConnect, message));
        HttpRequestOptions options = new() { ForwardProxy = new ProxyEndpoint(kind, "127.0.0.1", 1, null) };
        Diagnostics.Arrange("proxy", $"{kind} 127.0.0.1:1, connect fails");

        TransferResult result = await new HttpProtocolHandler(connector, new OriginAndProxyAuthenticator(null, null, null))
            .ExecuteAsync(ProxyContext("http://example.test/", new MemoryStream(), options));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Diagnostics.Assert("error message", message, result.ErrorMessage);
        Diagnostics.Assert("proxy used", true, result.Report!.UsedProxy);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        Assert.IsTrue(result.Report!.UsedProxy);
    }

    [TestMethod]
    [DataRow(ProxyKind.Http, "::1", "http://[::1]:18183/", DisplayName = "an IPv6 literal goes in brackets")]
    [DataRow(ProxyKind.Http, "[::1]", "http://[::1]:18183/", DisplayName = "a bracketed IPv6 literal stays as it is")]
    [DataRow(ProxyKind.Https, "proxy.example", "https://proxy.example:18183/", DisplayName = "an HTTPS proxy's URL is https")]
    public async Task ExecuteAsync_ProxyCredential_AsksTheAuthenticatorAboutTheProxysOwnUrl(ProxyKind kind, string host, string expectedUrl)
    {
        ScriptedAuthenticator authenticator = new("Basic dTpw", null);
        HttpRequestOptions options = new() { ForwardProxy = new ProxyEndpoint(kind, host, 18183, new NetworkCredential("u", "p")) };
        Diagnostics.Arrange("proxy", $"{kind} {host}:18183, credential u:p");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(new TurnTakingConnection(65536, ProxyOkHead + "ok")), authenticator)
            .ExecuteAsync(ProxyContext("http://example.com/", new MemoryStream(), options));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("proxy url asked about", expectedUrl, authenticator.Calls.Single(call => call.Request.IsProxy).Request.Url.OriginalString);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(expectedUrl, authenticator.Calls.Single(call => call.Request.IsProxy).Request.Url.OriginalString);
    }

    private static TransferContext ProxyContext(string url, Stream output, HttpRequestOptions options) =>
        new() { Url = CurlUrl.Parse(url), Output = output, Http = options };
}
