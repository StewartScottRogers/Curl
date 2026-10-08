using System.Net;
using System.Net.Sockets;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins the CONNECT request bytes to those curl 8.21.0 sent to a loopback proxy (the
/// commands are in BL-212's Notes) and reads the proxy's reply as curl does.
/// </summary>
[TestClass]
public sealed class HttpProxyTunnelTests
{
    private static readonly ProxyEndpoint HttpProxy = new(ProxyKind.Http, "127.0.0.1", 3128, null);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void BuildConnectRequest_ForAnHttpProxy_MatchesCurl()
    {
        // curl -p -x 127.0.0.1:18261 http://example.com/
        ArrangeRequest("example.com:80", HttpProxy, "default", null);

        var request = HttpProxyTunnel.BuildConnectRequest(
            "example.com", 80,
            HttpProxy,
            HttpProxyTunnelOptions.Default, null);

        AssertRequest("CONNECT example.com:80 HTTP/1.1\r\nHost: example.com:80\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n", request);
        Assert.AreEqual(
            "CONNECT example.com:80 HTTP/1.1\r\nHost: example.com:80\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n",
            Encoding.Latin1.GetString(request));
    }

    [TestMethod]
    public void BuildConnectRequest_WithAProxyAuthorization_SendsItBeforeUserAgent()
    {
        // curl -x http://127.0.0.1:18262 -U user:p@ss https://example.com:8443/path
        ArrangeRequest("example.com:8443", HttpProxy, "default", "Basic dXNlcjpwQHNz");

        var request = HttpProxyTunnel.BuildConnectRequest(
            "example.com", 8443,
            HttpProxy,
            HttpProxyTunnelOptions.Default, "Basic dXNlcjpwQHNz");

        AssertRequest("CONNECT example.com:8443 HTTP/1.1\r\nHost: example.com:8443\r\nProxy-Authorization: Basic dXNlcjpwQHNz\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n", request);
        Assert.AreEqual(
            "CONNECT example.com:8443 HTTP/1.1\r\nHost: example.com:8443\r\nProxy-Authorization: Basic dXNlcjpwQHNz\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n",
            Encoding.Latin1.GetString(request));
    }

    [TestMethod]
    public void BuildConnectRequest_WithWindows1252_SendsTheProxyHeaderInWindows1252()
    {
        // Measured with curl 8.21.0 (mingw, Windows-1252): € is sent as the byte 80 (BL-447).
        var windows1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;
        ArrangeRequest("example.com:80", HttpProxy, "proxy header \"X-A: €\", command-line encoding Windows-1252", null);

        var request = HttpProxyTunnel.BuildConnectRequest(
            "example.com", 80,
            HttpProxy,
            HttpProxyTunnelOptions.Default with { ProxyHeaders = ["X-A: €"], CommandLineTextEncoding = windows1252 }, null);

        ActRequest(request);
        Diagnostics.Assert("holds byte 0x80", true, request.Contains((byte)0x80));
        Diagnostics.Assert("holds the header", true, Encoding.Latin1.GetString(request).Contains("\r\nX-A: \u0080\r\n", StringComparison.Ordinal));
        CollectionAssert.IsSubsetOf(new byte[] { 0x80 }, request);
        StringAssert.Contains(Encoding.Latin1.GetString(request), "\r\nX-A: \u0080\r\n");
    }

    [TestMethod]
    public void BuildConnectRequest_WithUtf8_SendsTheProxyHeaderAndUserAgentInUtf8()
    {
        ArrangeRequest("example.com:80", HttpProxy, "user agent \"é\", proxy header \"X-A: é\", UTF-8", null);

        var request = HttpProxyTunnel.BuildConnectRequest(
            "example.com", 80,
            HttpProxy,
            new HttpProxyTunnelOptions("é", Encoding.UTF8) { ProxyHeaders = ["X-A: é"], CommandLineTextEncoding = Encoding.UTF8 }, null);

        AssertRequest("CONNECT example.com:80 HTTP/1.1\r\nHost: example.com:80\r\nUser-Agent: Ã©\r\nProxy-Connection: Keep-Alive\r\nX-A: Ã©\r\n\r\n", request);
        Assert.AreEqual(
            "CONNECT example.com:80 HTTP/1.1\r\nHost: example.com:80\r\nUser-Agent: Ã©\r\nProxy-Connection: Keep-Alive\r\nX-A: Ã©\r\n\r\n",
            Encoding.Latin1.GetString(request));
    }

    [TestMethod]
    public void BuildConnectRequest_ByDefault_SendsTheProxyHeaderInLatin1()
    {
        ArrangeRequest("example.com:80", HttpProxy, "proxy header \"X-A: é\", default encoding", null);

        var request = HttpProxyTunnel.BuildConnectRequest(
            "example.com", 80,
            HttpProxy,
            HttpProxyTunnelOptions.Default with { ProxyHeaders = ["X-A: é"] }, null);

        ActRequest(request);
        Diagnostics.Assert("holds the header", true, Encoding.Latin1.GetString(request).Contains("\r\nX-A: é\r\n", StringComparison.Ordinal));
        StringAssert.Contains(Encoding.Latin1.GetString(request), "\r\nX-A: é\r\n");
    }

    [TestMethod]
    public void BuildConnectRequest_ForAnIPv6Target_BracketsTheAddress()
    {
        // curl -p -x 127.0.0.1:18263 http://[::1]:81/
        ArrangeRequest("::1 port 81", HttpProxy, "default", null);

        var request = HttpProxyTunnel.BuildConnectRequest(
            "::1", 81,
            HttpProxy,
            HttpProxyTunnelOptions.Default, null);

        AssertRequest("CONNECT [::1]:81 HTTP/1.1\r\nHost: [::1]:81\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n", request);
        Assert.AreEqual(
            "CONNECT [::1]:81 HTTP/1.1\r\nHost: [::1]:81\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n",
            Encoding.Latin1.GetString(request));
    }

    [TestMethod]
    public void BuildConnectRequest_ForAnAlreadyBracketedTarget_LeavesItAsItIs()
    {
        ArrangeRequest("[::1] port 81", HttpProxy, "default", null);

        var request = HttpProxyTunnel.BuildConnectRequest(
            "[::1]", 81,
            HttpProxy,
            HttpProxyTunnelOptions.Default, null);

        ActRequest(request);
        Diagnostics.Assert("starts with", true, Encoding.Latin1.GetString(request).StartsWith("CONNECT [::1]:81 HTTP/1.1\r\n", StringComparison.Ordinal));
        StringAssert.StartsWith(Encoding.Latin1.GetString(request), "CONNECT [::1]:81 HTTP/1.1\r\n");
    }

    [TestMethod]
    public void BuildConnectRequest_ForAnHttp10Proxy_UsesHttp10()
    {
        // curl --proxy1.0 127.0.0.1:18264 -p http://example.com/
        ArrangeRequest("example.com:80", HttpProxy with { Kind = ProxyKind.Http10 }, "default", null);

        var request = HttpProxyTunnel.BuildConnectRequest(
            "example.com", 80,
            HttpProxy with { Kind = ProxyKind.Http10 },
            HttpProxyTunnelOptions.Default, null);

        AssertRequest("CONNECT example.com:80 HTTP/1.0\r\nHost: example.com:80\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n", request);
        Assert.AreEqual(
            "CONNECT example.com:80 HTTP/1.0\r\nHost: example.com:80\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n",
            Encoding.Latin1.GetString(request));
    }

    [TestMethod]
    public void BuildConnectRequest_WithACustomUserAgent_SendsIt()
    {
        // curl -p -x 127.0.0.1:18266 -A Agent/1 http://example.com/
        ArrangeRequest("example.com:80", HttpProxy, "user agent Agent/1", null);

        var request = HttpProxyTunnel.BuildConnectRequest(
            "example.com", 80,
            HttpProxy,
            HttpProxyTunnelOptions.Default with { UserAgent = "Agent/1" }, null);

        AssertRequest("CONNECT example.com:80 HTTP/1.1\r\nHost: example.com:80\r\nUser-Agent: Agent/1\r\nProxy-Connection: Keep-Alive\r\n\r\n", request);
        Assert.AreEqual(
            "CONNECT example.com:80 HTTP/1.1\r\nHost: example.com:80\r\nUser-Agent: Agent/1\r\nProxy-Connection: Keep-Alive\r\n\r\n",
            Encoding.Latin1.GetString(request));
    }

    [TestMethod]
    public void BuildConnectRequest_WithNoUserAgent_OmitsTheHeader()
    {
        ArrangeRequest("example.com:80", HttpProxy, "no user agent", null);

        var request = HttpProxyTunnel.BuildConnectRequest(
            "example.com", 80,
            HttpProxy,
            HttpProxyTunnelOptions.Default with { UserAgent = null }, null);

        AssertRequest("CONNECT example.com:80 HTTP/1.1\r\nHost: example.com:80\r\nProxy-Connection: Keep-Alive\r\n\r\n", request);
        Assert.AreEqual(
            "CONNECT example.com:80 HTTP/1.1\r\nHost: example.com:80\r\nProxy-Connection: Keep-Alive\r\n\r\n",
            Encoding.Latin1.GetString(request));
    }

    // Each expected request is the one curl 8.21.0 sent with
    // curl -s -x http://127.0.0.1:<port> -p <proxy headers> http://example.com/ (BL-347 Notes).
    [TestMethod]
    [DataRow(new[] { "X-P: 1" }, "Host: example.com:80\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\nX-P: 1\r\n")]
    [DataRow(new[] { "User-Agent: x" }, "Host: example.com:80\r\nProxy-Connection: Keep-Alive\r\nUser-Agent: x\r\n")]
    [DataRow(new[] { "Host: h" }, "User-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\nHost: h\r\n")]
    [DataRow(new[] { "Proxy-Connection: close" }, "Host: example.com:80\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: close\r\n")]
    [DataRow(new[] { "User-Agent:", "X-E;", "Proxy-Connection;" }, "Host: example.com:80\r\nX-E: \r\nProxy-Connection: \r\n")]
    [DataRow(new[] { "X-P:   spaced  ", "X-T:\tt", "X-N:1" }, "Host: example.com:80\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\nX-P: spaced  \r\nX-T: t\r\nX-N: 1\r\n")]
    [DataRow(new[] { "X-F; junk", "Bogus", ": novalue", "X-G:" }, "Host: example.com:80\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n")]
    [DataRow(new[] { "X-D: 1", "x-d: 2", "user-agent: lower" }, "Host: example.com:80\r\nProxy-Connection: Keep-Alive\r\nX-D: 1\r\nx-d: 2\r\nuser-agent: lower\r\n")]
    [DataRow(new[] { "Host;", "Content-Length: 5", "Connection: close" }, "User-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\nHost: \r\nContent-Length: 5\r\nConnection: close\r\n")]
    [DataRow(new[] { "Host; junk", "User-Agent", "Proxy-Connection;x" }, "User-Agent: curl/8.21.0\r\n")]
    [DataRow(new[] { "X-S;: v", "X-C:v;w", "X-W: " }, "Host: example.com:80\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\nX-C: v;w\r\n")]
    public void BuildConnectRequest_WithProxyHeaders_MatchesCurl(string[] proxyHeaders, string expectedHeaders)
    {
        ArrangeRequest("example.com:80", HttpProxy, "proxy headers " + string.Join(" | ", proxyHeaders), null);

        var request = HttpProxyTunnel.BuildConnectRequest(
            "example.com", 80,
            HttpProxy,
            HttpProxyTunnelOptions.Default with { ProxyHeaders = proxyHeaders }, null);

        AssertRequest($"CONNECT example.com:80 HTTP/1.1\r\n{expectedHeaders}\r\n", request);
        Assert.AreEqual($"CONNECT example.com:80 HTTP/1.1\r\n{expectedHeaders}\r\n", Encoding.Latin1.GetString(request));
    }

    [TestMethod]
    public void BuildConnectRequest_WithAProxyAuthorizationProxyHeader_ReplacesTheCredential()
    {
        // curl -s -x http://127.0.0.1:18341 -p -U u:p --proxy-header "Proxy-Authorization: Z" --proxy-header "X-B: 2" http://example.com/
        var proxy = HttpProxy with { Credential = new NetworkCredential("u", "p") };
        ArrangeRequest("example.com:80", proxy, "proxy headers Proxy-Authorization: Z | X-B: 2", "Basic dTpw");

        var request = HttpProxyTunnel.BuildConnectRequest(
            "example.com", 80,
            proxy,
            HttpProxyTunnelOptions.Default with { ProxyHeaders = ["Proxy-Authorization: Z", "X-B: 2"] }, "Basic dTpw");

        AssertRequest("CONNECT example.com:80 HTTP/1.1\r\nHost: example.com:80\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\nProxy-Authorization: Z\r\nX-B: 2\r\n\r\n", request);
        Assert.AreEqual(
            "CONNECT example.com:80 HTTP/1.1\r\nHost: example.com:80\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\nProxy-Authorization: Z\r\nX-B: 2\r\n\r\n",
            Encoding.Latin1.GetString(request));
    }

    [TestMethod]
    public void BuildConnectRequest_WithEmptyProxyHeadersForCurlsOwnHeaders_SendsOnlyTheOthers()
    {
        // curl -s -x http://127.0.0.1:18403 -p -U u:p --proxy-header Host: --proxy-header Proxy-Authorization: --proxy-header Proxy-Connection: http://example.com/
        var proxy = HttpProxy with { Credential = new NetworkCredential("u", "p") };
        ArrangeRequest("example.com:80", proxy, "proxy headers Host: | Proxy-Authorization: | Proxy-Connection:", "Basic dTpw");

        var request = HttpProxyTunnel.BuildConnectRequest(
            "example.com", 80,
            proxy,
            HttpProxyTunnelOptions.Default with { ProxyHeaders = ["Host:", "Proxy-Authorization:", "Proxy-Connection:"] }, "Basic dTpw");

        AssertRequest("CONNECT example.com:80 HTTP/1.1\r\nUser-Agent: curl/8.21.0\r\n\r\n", request);
        Assert.AreEqual("CONNECT example.com:80 HTTP/1.1\r\nUser-Agent: curl/8.21.0\r\n\r\n", Encoding.Latin1.GetString(request));
    }

    [TestMethod]
    public void Default_ProxyHeaders_IsEmpty()
    {
        Diagnostics.Arrange("options", "HttpProxyTunnelOptions.Default");

        var proxyHeaders = HttpProxyTunnelOptions.Default.ProxyHeaders;

        Diagnostics.Act("proxy headers", proxyHeaders.Count);
        Diagnostics.Assert("proxy headers", 0, proxyHeaders.Count);
        Assert.IsEmpty(HttpProxyTunnelOptions.Default.ProxyHeaders);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 407 Proxy Authentication Required\r\nContent-Length: 0\r\n\r\n", 407)]
    [DataRow("HTTP/1.0 200 Connection established\r\n\r\n", 200)]
    [DataRow("HTTP/1.1 200\r\n\r\n", 200)]
    [DataRow("HTTP/1.1 299 Odd\r\nX-A: b\r\n\r\n", 299)]
    [DataRow("HTTP/1.1 200 OK\n\n", 200)]
    [DataRow("HTTP/1.1 200 OK\r\nX: a\rb\n\r\n", 200)]
    [DataRow("garbage\r\n\r\n", 0)]
    [DataRow("HTTP/1.1\r\nX: 200\r\n\r\n", 0)]
    [DataRow("HTTP/1.1 2000 OK\r\n\r\n", 0)]
    [DataRow("HTTP/1.1 2x0 Odd\r\n\r\n", 0)]
    [DataRow("HTTP/1.1 20\r\n\r\n", 0)]
    [DataRow("FTP/1.1 200 OK\r\n\r\n", 0)]
    public async Task ReadReplyAsync_ReturnsTheStatusCodeOrZeroWhenTheReplyIsNotHttp(string reply, int expected)
    {
        // Measured: "HTTP/1.1 2000 OK" and "HTTP/1.1\r\nX: 200" are both "response 0" to curl.
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(reply));
        ArrangeReply(reply);

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        ActReply(result);
        Diagnostics.Assert("status code", expected, result.StatusCode);
        Diagnostics.Assert("failure message", "null", result.FailureMessage ?? "null");
        Assert.AreEqual(expected, result.StatusCode);
        Assert.IsNull(result.FailureMessage);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 Connection established\r\nX-Proxy: yes\r\n\r\n")]
    [DataRow("HTTP/1.0 200 OK\n\n")]
    public async Task ReadReplyAsync_ReturnsTheHeadExactlyAsReadAndNothingAfterIt(string head)
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(head + "HTTP/1.1 200 OK\r\n"));
        ArrangeReply(head + "HTTP/1.1 200 OK\r\n");

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        ActReply(result);
        Diagnostics.Diff("head", head, Encoding.Latin1.GetString(result.Head.Span));
        Assert.AreEqual(head, Encoding.Latin1.GetString(result.Head.Span));
    }

    [TestMethod]
    [DataRow(SocketError.ConnectionReset)]
    [DataRow(SocketError.ConnectionAborted)]
    public async Task ReadReplyAsync_WhenTheReadFailsBeforeAnyReplyByte_ReturnsTheRecvFailure(SocketError socketError)
    {
        // BL-1449: curl 8.21.0's socket filter words the failed read as curl: (56) Recv failure: Connection was reset.
        var failure = new IOException("Unable to read data from the transport connection.", new SocketException((int)socketError));
        var connection = new ScriptedConnection([]) { ReadException = failure };
        Diagnostics.Arrange("read", $"fails at once with an IOException over SocketError.{socketError}");

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        ActReply(result);
        Diagnostics.Assert("failure exit code", CurlExitCode.RecvError, result.FailureExitCode);
        Diagnostics.Assert("failure message", CurlSocketErrorText.ReceiveFailure(failure), result.FailureMessage);
        Diagnostics.Assert("head is empty", true, result.Head.IsEmpty);
        Assert.AreEqual(CurlExitCode.RecvError, result.FailureExitCode);
        Assert.AreEqual(CurlSocketErrorText.ReceiveFailure(failure), result.FailureMessage);
        StringAssert.StartsWith(result.FailureMessage, "Recv failure: ");
        Assert.IsTrue(result.Head.IsEmpty);
    }

    [TestMethod]
    public async Task ReadReplyAsync_WhenTheReadFailsWithoutASocketError_LetsTheExceptionEscape()
    {
        var connection = new ScriptedConnection([]) { ReadException = new IOException("The stream was closed.") };
        Diagnostics.Arrange("read", "fails at once with an IOException and no SocketException");

        var exception = await Assert.ThrowsExactlyAsync<IOException>(
            async () => await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None));

        Diagnostics.Act("exception", $"{exception.GetType().Name}: {exception.Message}");
        Diagnostics.Assert("exception type", nameof(IOException), exception.GetType().Name);
    }

    [TestMethod]
    public async Task ReadReplyAsync_WhenTheReadFailsAfterPartOfTheHead_ReturnsProxyConnectAborted()
    {
        // Measured with Record-CurlExchange.ps1 -ResetAfterResponse: curl: (56) Proxy CONNECT aborted.
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\n"))
        {
            ExceptionAfterScript = new IOException("Unable to read data from the transport connection.", new SocketException((int)SocketError.ConnectionReset)),
        };
        ArrangeReply("HTTP/1.1 200 OK\r\n");
        Diagnostics.Arrange("after the reply", "the read fails with ConnectionReset");

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        ActReply(result);
        Diagnostics.Assert("failure exit code", CurlExitCode.RecvError, result.FailureExitCode);
        Diagnostics.Assert("failure message", "Proxy CONNECT aborted", result.FailureMessage);
        Diagnostics.Assert("head is empty", true, result.Head.IsEmpty);
        Assert.AreEqual(CurlExitCode.RecvError, result.FailureExitCode);
        Assert.AreEqual("Proxy CONNECT aborted", result.FailureMessage);
        Assert.IsTrue(result.Head.IsEmpty);
    }

    [TestMethod]
    public async Task ReadReplyAsync_WhenTheProxyClosesBeforeTheHeadEnds_ReturnsNoHead()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\n"));
        ArrangeReply("HTTP/1.1 200 OK\r\n");

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        ActReply(result);
        Diagnostics.Assert("failure message", "Proxy CONNECT aborted", result.FailureMessage);
        Diagnostics.Assert("head is empty", true, result.Head.IsEmpty);
        Assert.AreEqual("Proxy CONNECT aborted", result.FailureMessage);
        Assert.IsTrue(result.Head.IsEmpty);
    }

    [TestMethod]
    public async Task ReadReplyAsync_ReturnsEveryProxyAuthenticateValueInOrder()
    {
        const string Reply = "HTTP/1.1 407 Proxy Authentication Required\r\nproxy-authenticate:  Digest realm=\"r\", nonce=\"abc\"\t\r\nX-A: b\r\nProxy-Authenticate: Basic realm=\"r\"\r\n\r\n";
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(Reply));
        ArrangeReply(Reply);

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        ActReply(result);
        Diagnostics.Assert("Proxy-Authenticate", "Digest realm=\"r\", nonce=\"abc\" | Basic realm=\"r\"", string.Join(" | ", result.ProxyAuthenticate));
        CollectionAssert.AreEqual(new[] { "Digest realm=\"r\", nonce=\"abc\"", "Basic realm=\"r\"" }, result.ProxyAuthenticate.ToArray());
    }

    [TestMethod]
    [DataRow("", 0L, true)]
    [DataRow("Content-Length: 12\r\n", 12L, true)]
    [DataRow("Content-Length:  12 \r\n", 12L, true)]
    [DataRow("Content-Length: 12\r\nContent-Length: 7x\r\n", 7L, true)]
    [DataRow("Connection: keep-alive\r\n", 0L, true)]
    [DataRow("Connection: close\r\n", 0L, false)]
    [DataRow("Proxy-Connection: Keep-Alive, Close\r\n", 0L, false)]
    [DataRow("Transfer-Encoding: gzip, chunked\r\n", 0L, true)]
    [DataRow("Transfer-Encoding: gzip\r\n", 0L, true)]
    [DataRow(": close\r\nno colon\r\n", 0L, true)]
    public async Task ReadReplyAsync_ReadsTheBodyLengthAndWhetherTheConnectionIsReusable(string fields, long contentLength, bool reusable)
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes($"HTTP/1.1 407 Proxy Authentication Required\r\n{fields}\r\n"));
        ArrangeReply($"HTTP/1.1 407 Proxy Authentication Required\r\n{fields}\r\n");

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        ActReply(result);
        Diagnostics.Assert("content length", contentLength, result.ContentLength);
        Diagnostics.Assert("leaves the connection reusable", reusable, result.LeavesConnectionReusable);
        Diagnostics.Assert("Proxy-Authenticate values", 0, result.ProxyAuthenticate.Count);
        Assert.AreEqual(contentLength, result.ContentLength);
        Assert.AreEqual(reusable, result.LeavesConnectionReusable);
        Assert.IsEmpty(result.ProxyAuthenticate);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 407 Proxy Auth\r\n", "Content-Length: abc\r\n", false, DisplayName = "a 407, measured")]
    [DataRow("HTTP/1.1 407 Proxy Auth\r\nContent-Length: 3\r\n", "content-length: -1\r\n", false, DisplayName = "a sign")]
    [DataRow("HTTP/1.1 403 No\r\n", "Content-Length: 99999999999999999999\r\n", false, DisplayName = "an overflow")]
    [DataRow("HTTP/1.1 101 Switching\r\n", "Content-Length: abc\r\n", false, DisplayName = "a 101 to CONNECT")]
    [DataRow("HTTP/1.1 302 Found\r\n", "Content-Length:\r\n", true, DisplayName = "empty, to CONNECT-UDP")]
    public async Task ReadReplyAsync_WhenAContentLengthTheStatusDoesNotIgnoreIsNotANumber_FailsWithExit8AtItsLine(string before, string field, bool forConnectUdp)
    {
        // curl 8.21.0's lib/cf-h1-proxy.c: failf "Unsupported Content-Length value", CURLE_WEIRD_SERVER_REPLY.
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(before + field + "X-After: 1\r\n\r\nbody"));
        ArrangeReply(before + field + "X-After: 1\r\n\r\nbody");
        Diagnostics.Arrange("for CONNECT-UDP", forConnectUdp);

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, forConnectUdp, CancellationToken.None);

        ActReply(result);
        Diagnostics.Assert("failure exit code", CurlExitCode.WeirdServerReply, result.FailureExitCode);
        Diagnostics.Assert("failure message", "Unsupported Content-Length value", result.FailureMessage);
        Diagnostics.Assert("status code", 0, result.StatusCode);
        Diagnostics.Diff("head", before + field, Encoding.Latin1.GetString(result.Head.Span));
        Diagnostics.Assert("opens the tunnel", false, result.OpensTunnel);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.FailureExitCode);
        Assert.AreEqual("Unsupported Content-Length value", result.FailureMessage);
        Assert.AreEqual(0, result.StatusCode);
        Assert.AreEqual(before + field, Encoding.Latin1.GetString(result.Head.Span));
        Assert.IsFalse(result.OpensTunnel);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 Connection established\r\nContent-Length: 5\r\n\r\n", false, DisplayName = "a 200, measured")]
    [DataRow("HTTP/1.1 200 Connection established\r\nTransfer-Encoding: chunked\r\n\r\n", false, DisplayName = "a chunked 200, measured")]
    [DataRow("HTTP/1.1 299 Fine\r\nContent-Length: 5\r\n\r\n", false, DisplayName = "a 299, measured")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: abc\r\n\r\n", false, DisplayName = "a 200 not a number")]
    [DataRow("HTTP/1.1 101 Switching Protocols\r\nContent-Length: abc\r\n\r\n", true, DisplayName = "a 101 to CONNECT-UDP")]
    public async Task ReadReplyAsync_WhenTheStatusIgnoresTheBodyFields_OpensTheTunnelAndReadsNoBody(string head, bool forConnectUdp)
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(head + "tunnel"));
        ArrangeReply(head + "tunnel");
        Diagnostics.Arrange("for CONNECT-UDP", forConnectUdp);

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, forConnectUdp, CancellationToken.None);

        ActReply(result);
        Diagnostics.Act("unread bytes", connection.UnreadCount);
        Diagnostics.Assert("failure message", "null", result.FailureMessage ?? "null");
        Diagnostics.Assert("opens a UDP tunnel", true, result.OpensUdpTunnel);
        Diagnostics.Assert("content length", 0L, result.ContentLength);
        Diagnostics.Diff("head", head, Encoding.Latin1.GetString(result.Head.Span));
        Diagnostics.Assert("unread bytes", "tunnel".Length, connection.UnreadCount);
        Assert.IsNull(result.FailureMessage);
        Assert.IsTrue(result.OpensUdpTunnel);
        Assert.AreEqual(0L, result.ContentLength);
        Assert.AreEqual(head, Encoding.Latin1.GetString(result.Head.Span));
        Assert.AreEqual("tunnel".Length, connection.UnreadCount);
    }

    [TestMethod]
    public void FailureExitCode_OfAFailedReply_IsRecvError()
    {
        Diagnostics.Arrange("reply", "Failed(\"Proxy CONNECT aborted\")");

        var exitCode = HttpProxyTunnelReply.Failed("Proxy CONNECT aborted").FailureExitCode;

        Diagnostics.Act("failure exit code", exitCode);
        Diagnostics.Assert("failure exit code", CurlExitCode.RecvError, exitCode);
        Assert.AreEqual(CurlExitCode.RecvError, HttpProxyTunnelReply.Failed("Proxy CONNECT aborted").FailureExitCode);
    }

    [TestMethod]
    public void ProxyAuthenticate_OfADefaultReply_IsEmpty()
    {
        Diagnostics.Arrange("reply", "default(HttpProxyTunnelReply)");

        var proxyAuthenticate = default(HttpProxyTunnelReply).ProxyAuthenticate;

        Diagnostics.Act("Proxy-Authenticate values", proxyAuthenticate.Count);
        Diagnostics.Assert("Proxy-Authenticate values", 0, proxyAuthenticate.Count);
        Assert.IsEmpty(default(HttpProxyTunnelReply).ProxyAuthenticate);
    }

    [TestMethod]
    public async Task DiscardBodyAsync_ReadsExactlyTheBody()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(new string('a', 5000) + "next"));
        Diagnostics.Arrange("script", "5000 'a's, then \"next\"");
        Diagnostics.Arrange("body length", 5000);

        var discarded = await HttpProxyTunnel.DiscardBodyAsync(connection, 5000, CancellationToken.None);

        Diagnostics.Act("discarded", discarded);
        Diagnostics.Act("unread bytes", connection.UnreadCount);
        Diagnostics.Assert("discarded", true, discarded);
        Diagnostics.Assert("unread bytes", "next".Length, connection.UnreadCount);
        Assert.IsTrue(discarded);
        Assert.AreEqual("next".Length, connection.UnreadCount);
    }

    [TestMethod]
    public async Task DiscardBodyAsync_WhenTheProxyClosesFirst_ReturnsFalse()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes("abc"));
        Diagnostics.Arrange("script", "\"abc\", then the proxy closes");
        Diagnostics.Arrange("body length", 4);

        var discarded = await HttpProxyTunnel.DiscardBodyAsync(connection, 4, CancellationToken.None);

        Diagnostics.Act("discarded", discarded);
        Diagnostics.Assert("discarded", false, discarded);
        Assert.IsFalse(discarded);
    }

    [TestMethod]
    public async Task DiscardBodyAsync_WhenReadingFails_ReturnsFalse()
    {
        var connection = new ScriptedConnection([]) { ReadException = new IOException("reset") };
        Diagnostics.Arrange("read", "fails at once with IOException \"reset\"");
        Diagnostics.Arrange("body length", 1);

        var discarded = await HttpProxyTunnel.DiscardBodyAsync(connection, 1, CancellationToken.None);

        Diagnostics.Act("discarded", discarded);
        Diagnostics.Assert("discarded", false, discarded);
        Assert.IsFalse(discarded);
    }

    [TestMethod]
    [DataRow("Transfer-Encoding: chunked\r\n", true)]
    [DataRow("Transfer-Encoding: gzip, Chunked\r\nContent-Length: 3\r\n", true)]
    [DataRow("Transfer-Encoding: gzip\r\n", false)]
    [DataRow("", false)]
    public async Task ReadReplyAsync_ReadsWhetherTheBodyIsChunked(string fields, bool chunked)
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes($"HTTP/1.1 407 Proxy Authentication Required\r\n{fields}\r\n"));
        ArrangeReply($"HTTP/1.1 407 Proxy Authentication Required\r\n{fields}\r\n");

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        ActReply(result);
        Diagnostics.Assert("chunked", chunked, result.IsChunked);
        Assert.AreEqual(chunked, result.IsChunked);
    }

    [TestMethod]
    [DataRow("5;ext=1\r\nhello\r\n3\r\nabc\r\n0\r\nX-Trailer: t\r\n\r\n")]
    [DataRow("5 ;e\r\nhello\r\n0\r\n\r\n")]
    [DataRow("5\nhello\n0\n\n")]
    [DataRow("A\r\n0123456789\r\r\n0\r\nA: b\nB: c\r\n\r\n")]
    [DataRow("10\r\n0123456789abcdef\r\n0\r\n\n")]
    [DataRow("0\r\n\r\n")]
    public async Task DiscardChunkedBodyAsync_ReadsExactlyTheChunkedBody(string body)
    {
        // Measured: curl 8.21.0 ignores each of these 407 bodies and sends the next CONNECT on the same connection.
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(body + "HTTP/1.1 200 Connection established\r\n\r\n"));
        Diagnostics.Bytes("chunked body", Encoding.Latin1.GetBytes(body));
        Diagnostics.Arrange("after the body", "HTTP/1.1 200 Connection established");

        var failure = await HttpProxyTunnel.DiscardChunkedBodyAsync(connection, CancellationToken.None);

        Diagnostics.Act("failure", failure ?? "null");
        Diagnostics.Act("unread bytes", connection.UnreadCount);
        Diagnostics.Assert("failure", "null", failure ?? "null");
        Diagnostics.Assert("unread bytes", "HTTP/1.1 200 Connection established\r\n\r\n".Length, connection.UnreadCount);
        Assert.IsNull(failure);
        Assert.AreEqual("HTTP/1.1 200 Connection established\r\n\r\n".Length, connection.UnreadCount);
    }

    [TestMethod]
    [DataRow("zz\r\nhello\r\n0\r\n\r\n", "chunk hex-length char not a hex digit: 0x7a")]
    [DataRow("11111111111111111\r\nx\r\n0\r\n\r\n", "chunk hex-length longer than 16")]
    [DataRow("ffffffffffffffff\r\nx\r\n0\r\n\r\n", "invalid chunk size: 'ffffffffffffffff'")]
    [DataRow("5\r\nhelloX\r\n0\r\n\r\n", "Failure when receiving data from the peer")]
    [DataRow("5\r\nhello\rX0\r\n\r\n", "Failure when receiving data from the peer")]
    [DataRow("0\r\nA: b\rX\r\n\r\n", "Failure when receiving data from the peer")]
    [DataRow("0\r\n\rX", "Failure when receiving data from the peer")]
    [DataRow("5\r\nhel", "Proxy CONNECT aborted")]
    public async Task DiscardChunkedBodyAsync_ForAMalformedOrCutShortBody_ReturnsCurlsMessage(string body, string expected)
    {
        // Measured: curl 8.21.0 exits 56 with each of these messages.
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(body));
        Diagnostics.Bytes("chunked body", Encoding.Latin1.GetBytes(body));
        Diagnostics.Arrange("after the body", "the proxy closes");

        var failure = await HttpProxyTunnel.DiscardChunkedBodyAsync(connection, CancellationToken.None);

        Diagnostics.Act("failure", failure ?? "null");
        Diagnostics.Assert("failure", expected, failure ?? "null");
        Assert.AreEqual(expected, failure);
    }

    [TestMethod]
    public async Task DiscardChunkedBodyAsync_WhenReadingFails_ReturnsProxyConnectAborted()
    {
        var connection = new ScriptedConnection([]) { ReadException = new IOException("reset") };
        Diagnostics.Arrange("read", "fails at once with IOException \"reset\"");

        var failure = await HttpProxyTunnel.DiscardChunkedBodyAsync(connection, CancellationToken.None);

        Diagnostics.Act("failure", failure ?? "null");
        Diagnostics.Assert("failure", "Proxy CONNECT aborted", failure ?? "null");
        Assert.AreEqual("Proxy CONNECT aborted", failure);
    }

    [TestMethod]
    [DataRow("0\r\n\r\n", true, null)]
    [DataRow("zz", false, "chunk hex-length char not a hex digit: 0x7a")]
    public void Accept_AfterTheBodyIsCompleteOrMalformed_KeepsItSo(string body, bool complete, string? malformedMessage)
    {
        var chunkedBody = new HttpProxyTunnelChunkedBody();
        Diagnostics.Arrange("body accepted first", body.Length + " bytes");
        Diagnostics.Bytes("body accepted first", Encoding.Latin1.GetBytes(body));
        foreach (var value in Encoding.Latin1.GetBytes(body))
        {
            chunkedBody.Accept(value);
        }

        var accepted = chunkedBody.Accept((byte)'5');

        Diagnostics.Act("accepts '5' afterwards", accepted);
        Diagnostics.Act("complete", chunkedBody.IsComplete);
        Diagnostics.Act("malformed message", chunkedBody.MalformedMessage ?? "null");
        Diagnostics.Assert("accepts '5' afterwards", false, accepted);
        Diagnostics.Assert("complete", complete, chunkedBody.IsComplete);
        Diagnostics.Assert("malformed message", malformedMessage ?? "null", chunkedBody.MalformedMessage ?? "null");
        Assert.IsFalse(accepted);
        Assert.AreEqual(complete, chunkedBody.IsComplete);
        Assert.AreEqual(malformedMessage, chunkedBody.MalformedMessage);
    }

    [TestMethod]
    public void Default_ProxyAuthSchemes_IsBasicAndProxyAuthenticator_IsNull()
    {
        Diagnostics.Arrange("options", "HttpProxyTunnelOptions.Default");

        var options = HttpProxyTunnelOptions.Default;

        Diagnostics.Act("proxy auth schemes", options.ProxyAuthSchemes);
        Diagnostics.Act("proxy authenticator is null", options.ProxyAuthenticator is null);
        Diagnostics.Assert("proxy auth schemes", HttpAuthSchemes.Basic, options.ProxyAuthSchemes);
        Diagnostics.Assert("proxy authenticator is null", true, options.ProxyAuthenticator is null);
        Assert.AreEqual(HttpAuthSchemes.Basic, HttpProxyTunnelOptions.Default.ProxyAuthSchemes);
        Assert.IsNull(HttpProxyTunnelOptions.Default.ProxyAuthenticator);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("HTTP/1.1 200 Connection established\r\nX-A: b")]
    public async Task ReadReplyAsync_WhenTheProxyClosesBeforeTheHeaderBlockEnds_ReportsProxyConnectAborted(string reply)
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(reply));
        ArrangeReply(reply);

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        ActReply(result);
        Diagnostics.Assert("reply", Describe(HttpProxyTunnelReply.Failed("Proxy CONNECT aborted")), Describe(result));
        Assert.AreEqual(HttpProxyTunnelReply.Failed("Proxy CONNECT aborted"), result);
    }

    [TestMethod]
    [DataRow(16376, "", "Proxy CONNECT aborted")]
    [DataRow(16377, "", "CONNECT response too large")]
    [DataRow(16374, "\r\n", "Proxy CONNECT aborted")]
    [DataRow(16375, "\r\n", "CONNECT response too large")]
    public async Task ReadReplyAsync_WhenALineReaches16384Bytes_ReportsConnectResponseTooLarge(int padding, string lineEnd, string expected)
    {
        // Measured: "HTTP/1.1 200 OK\r\nX-Pad: " + padding 'a's + lineEnd, then the proxy closes.
        var reply = "HTTP/1.1 200 OK\r\nX-Pad: " + new string('a', padding) + lineEnd;
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(reply));
        ArrangeReply(reply);

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        ActReply(result);
        Diagnostics.Assert("reply", Describe(HttpProxyTunnelReply.Failed(expected)), Describe(result));
        Assert.AreEqual(HttpProxyTunnelReply.Failed(expected), result);
    }

    [TestMethod]
    public async Task ReadReplyAsync_WhenTheHeaderBlockPasses307200Bytes_ReportsTooLargeResponseHeaders()
    {
        // Measured: 1200 lines of "X-Pad: " + 1000 'a's + CRLF after the status line ->
        // curl: (56) Too large response headers: 307762 > 307200
        var line = "X-Pad: " + new string('a', 1000) + "\r\n";
        var reply = "HTTP/1.1 200 OK\r\n" + string.Concat(Enumerable.Repeat(line, 1200));
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(reply));
        ArrangeReply(reply);

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        ActReply(result);
        Diagnostics.Assert("reply", Describe(HttpProxyTunnelReply.Failed("Too large response headers: 307762 > 307200")), Describe(result));
        Assert.AreEqual(HttpProxyTunnelReply.Failed("Too large response headers: 307762 > 307200"), result);
    }

    [TestMethod]
    [DataRow(438, "Proxy CONNECT aborted")]
    [DataRow(439, "Too large response headers: 307201 > 307200")]
    public async Task ReadReplyAsync_WhenALineEndsTheHeaderBlockAt307200Bytes_KeepsReading(int lastPadding, string expected)
    {
        // 17 status bytes + 304 lines of 1009 bytes + a last line of 9 + lastPadding bytes:
        // exactly 307200 bytes is still within the limit, one more is over it.
        var line = "X-Pad: " + new string('a', 1000) + "\r\n";
        var reply = "HTTP/1.1 200 OK\r\n" + string.Concat(Enumerable.Repeat(line, 304))
            + "X-Pad: " + new string('a', lastPadding) + "\r\n";
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(reply));
        ArrangeReply(reply);

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        ActReply(result);
        Diagnostics.Assert("reply", Describe(HttpProxyTunnelReply.Failed(expected)), Describe(result));
        Assert.AreEqual(HttpProxyTunnelReply.Failed(expected), result);
    }

    [TestMethod]
    public async Task ReadReplyAsync_LeavesTheBytesAfterTheHeaderBlockUnread()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\n\r\ntunnel"));
        ArrangeReply("HTTP/1.1 200 OK\r\n\r\ntunnel");

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        ActReply(result);
        Diagnostics.Act("unread bytes", connection.UnreadCount);
        Diagnostics.Assert("unread bytes", "tunnel".Length, connection.UnreadCount);
        Assert.AreEqual("tunnel".Length, connection.UnreadCount);
    }

    [TestMethod]
    public void BuildConnectUdpRequest_ForAnHttpProxy_MatchesCurl822()
    {
        // curl 8.22.0 --http3-only -x http://127.0.0.1:18942 https://example.com/ (BL-942 Notes).
        var proxy = new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 18942, null);
        ArrangeRequest("example.com:443 over CONNECT-UDP", proxy, "user agent curl/8.22.0", null);

        var request = HttpProxyTunnel.BuildConnectUdpRequest("example.com", 443, proxy, HttpProxyTunnelOptions.Default with { UserAgent = "curl/8.22.0" }, null);

        AssertRequest(
            "GET http://127.0.0.1:18942/.well-known/masque/udp/example.com/443/ HTTP/1.1\r\n"
                + "Host: 127.0.0.1:18942\r\nUser-Agent: curl/8.22.0\r\nProxy-Connection: Keep-Alive\r\n"
                + "Connection: Upgrade\r\nUpgrade: connect-udp\r\nCapsule-Protocol: ?1\r\n\r\n",
            request);
        Assert.AreEqual(
            "GET http://127.0.0.1:18942/.well-known/masque/udp/example.com/443/ HTTP/1.1\r\n"
                + "Host: 127.0.0.1:18942\r\nUser-Agent: curl/8.22.0\r\nProxy-Connection: Keep-Alive\r\n"
                + "Connection: Upgrade\r\nUpgrade: connect-udp\r\nCapsule-Protocol: ?1\r\n\r\n",
            Encoding.Latin1.GetString(request));
    }

    [TestMethod]
    public void BuildConnectUdpRequest_ThroughAnHttp10ProxyToAnIpv6Literal_MatchesCurl822()
    {
        // curl 8.22.0 --http3-only --proxy1.0 127.0.0.1:18942 https://[::1]:8443/ -U u:p
        // --proxy-header "X-P: q" (BL-942 Notes): the colons percent-encoded, the header last.
        var proxy = new ProxyEndpoint(ProxyKind.Http10, "127.0.0.1", 18942, null);
        ArrangeRequest("[::1]:8443 over CONNECT-UDP", proxy, "user agent curl/8.22.0, proxy header X-P: q", "Basic dTpw");

        var request = HttpProxyTunnel.BuildConnectUdpRequest(
            "[::1]", 8443,
            proxy,
            HttpProxyTunnelOptions.Default with { UserAgent = "curl/8.22.0", ProxyHeaders = ["X-P: q"] },
            "Basic dTpw");

        AssertRequest(
            "GET http://127.0.0.1:18942/.well-known/masque/udp/%3A%3A1/8443/ HTTP/1.0\r\n"
                + "Host: 127.0.0.1:18942\r\nProxy-Authorization: Basic dTpw\r\nUser-Agent: curl/8.22.0\r\nProxy-Connection: Keep-Alive\r\n"
                + "Connection: Upgrade\r\nUpgrade: connect-udp\r\nCapsule-Protocol: ?1\r\nX-P: q\r\n\r\n",
            request);
        Assert.AreEqual(
            "GET http://127.0.0.1:18942/.well-known/masque/udp/%3A%3A1/8443/ HTTP/1.0\r\n"
                + "Host: 127.0.0.1:18942\r\nProxy-Authorization: Basic dTpw\r\nUser-Agent: curl/8.22.0\r\nProxy-Connection: Keep-Alive\r\n"
                + "Connection: Upgrade\r\nUpgrade: connect-udp\r\nCapsule-Protocol: ?1\r\nX-P: q\r\n\r\n",
            Encoding.Latin1.GetString(request));
    }

    [TestMethod]
    public void BuildConnectUdpRequest_ThroughAnHttpsProxy_NamesItWithHttps()
    {
        // curl 8.22.0 -x https://localhost:18942 --proxy-insecure (BL-942 Notes).
        var proxy = new ProxyEndpoint(ProxyKind.Https, "localhost", 18942, null);
        ArrangeRequest("example.com:443 over CONNECT-UDP", proxy, "default", null);

        var request = HttpProxyTunnel.BuildConnectUdpRequest("example.com", 443, proxy, HttpProxyTunnelOptions.Default, null);

        ActRequest(request);
        Diagnostics.Assert(
            "starts with",
            true,
            Encoding.Latin1.GetString(request).StartsWith("GET https://localhost:18942/.well-known/masque/udp/example.com/443/ HTTP/1.1\r\nHost: localhost:18942\r\n", StringComparison.Ordinal));
        StringAssert.StartsWith(Encoding.Latin1.GetString(request), "GET https://localhost:18942/.well-known/masque/udp/example.com/443/ HTTP/1.1\r\nHost: localhost:18942\r\n");
    }

    [TestMethod]
    [DataRow("HTTP/1.1 101 Switching Protocols\r\n\r\n", true)]
    [DataRow("HTTP/1.1 200 OK\r\n\r\n", true)]
    [DataRow("HTTP/1.1 100 Continue\r\n\r\n", false)]
    [DataRow("HTTP/1.1 403 Forbidden\r\n\r\n", false)]
    public async Task OpensUdpTunnel_Takes101And2xx(string reply, bool opens)
    {
        ArrangeReply(reply);

        var result = await HttpProxyTunnel.ReadReplyAsync(new ScriptedConnection(Encoding.Latin1.GetBytes(reply)), CancellationToken.None);

        ActReply(result);
        Diagnostics.Assert("opens a UDP tunnel", opens, result.OpensUdpTunnel);
        Assert.AreEqual(opens, result.OpensUdpTunnel);
    }

    [TestMethod]
    public void OpensUdpTunnel_ForAReplyCurlGivesUpOn_IsFalse()
    {
        Diagnostics.Arrange("replies", "Failed(\"Proxy CONNECT aborted\"), and status 101 with that failure message");

        var failed = HttpProxyTunnelReply.Failed("Proxy CONNECT aborted").OpensUdpTunnel;
        var failed101 = new HttpProxyTunnelReply(101, "Proxy CONNECT aborted").OpensUdpTunnel;

        Diagnostics.Act("failed reply opens a UDP tunnel", failed);
        Diagnostics.Act("failed 101 opens a UDP tunnel", failed101);
        Diagnostics.Assert("failed reply opens a UDP tunnel", false, failed);
        Diagnostics.Assert("failed 101 opens a UDP tunnel", false, failed101);
        Assert.IsFalse(HttpProxyTunnelReply.Failed("Proxy CONNECT aborted").OpensUdpTunnel);
        Assert.IsFalse(new HttpProxyTunnelReply(101, "Proxy CONNECT aborted").OpensUdpTunnel);
    }

    private static string Describe(HttpProxyTunnelReply reply) =>
        $"status {reply.StatusCode}, failure {reply.FailureExitCode} \"{reply.FailureMessage ?? "null"}\", head {reply.Head.Length} bytes, "
            + $"content length {reply.ContentLength}, chunked {reply.IsChunked}, reusable {reply.LeavesConnectionReusable}, Proxy-Authenticate {reply.ProxyAuthenticate.Count}";

    private void ArrangeRequest(string target, ProxyEndpoint proxy, string options, string? proxyAuthorization)
    {
        Diagnostics.Arrange("target", target);
        Diagnostics.Arrange("proxy", $"{proxy.Kind} {proxy.Host}:{proxy.Port}{(proxy.Credential is null ? string.Empty : ", with a credential")}");
        Diagnostics.Arrange("options", options);
        Diagnostics.Arrange("proxy authorization", proxyAuthorization ?? "none");
    }

    private void ActRequest(byte[] request)
    {
        Diagnostics.Bytes("request", request);
        Diagnostics.Act("request length", request.Length);
    }

    private void AssertRequest(string expected, byte[] request)
    {
        ActRequest(request);
        Diagnostics.Diff("request", Encoding.Latin1.GetBytes(expected), request);
    }

    private void ArrangeReply(string reply)
    {
        Diagnostics.Arrange("proxy reply length", reply.Length);
        Diagnostics.Bytes("proxy reply", Encoding.Latin1.GetBytes(reply));
    }

    private void ActReply(HttpProxyTunnelReply reply)
    {
        Diagnostics.Act("reply", Describe(reply));
        Diagnostics.Bytes("head", reply.Head.Span);
    }
}
