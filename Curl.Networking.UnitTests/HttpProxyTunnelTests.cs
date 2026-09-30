using System.Net;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins the CONNECT request bytes to those curl 8.21.0 sent to a loopback proxy (the
/// commands are in BL-212's Notes) and reads the proxy's reply as curl does.
/// </summary>
[TestClass]
public sealed class HttpProxyTunnelTests
{
    private static readonly ProxyEndpoint HttpProxy = new(ProxyKind.Http, "127.0.0.1", 3128, null);

    [TestMethod]
    public void BuildConnectRequest_ForAnHttpProxy_MatchesCurl()
    {
        // curl -p -x 127.0.0.1:18261 http://example.com/
        var request = HttpProxyTunnel.BuildConnectRequest(
            "example.com", 80,
            HttpProxy,
            HttpProxyTunnelOptions.Default, null);

        Assert.AreEqual(
            "CONNECT example.com:80 HTTP/1.1\r\nHost: example.com:80\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n",
            Encoding.Latin1.GetString(request));
    }

    [TestMethod]
    public void BuildConnectRequest_WithAProxyAuthorization_SendsItBeforeUserAgent()
    {
        // curl -x http://127.0.0.1:18262 -U user:p@ss https://example.com:8443/path
        var request = HttpProxyTunnel.BuildConnectRequest(
            "example.com", 8443,
            HttpProxy,
            HttpProxyTunnelOptions.Default, "Basic dXNlcjpwQHNz");

        Assert.AreEqual(
            "CONNECT example.com:8443 HTTP/1.1\r\nHost: example.com:8443\r\nProxy-Authorization: Basic dXNlcjpwQHNz\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n",
            Encoding.Latin1.GetString(request));
    }

    [TestMethod]
    public void BuildConnectRequest_WithWindows1252_SendsTheProxyHeaderInWindows1252()
    {
        // Measured with curl 8.21.0 (mingw, Windows-1252): € is sent as the byte 80 (BL-447).
        var windows1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

        var request = HttpProxyTunnel.BuildConnectRequest(
            "example.com", 80,
            HttpProxy,
            HttpProxyTunnelOptions.Default with { ProxyHeaders = ["X-A: €"], CommandLineTextEncoding = windows1252 }, null);

        CollectionAssert.IsSubsetOf(new byte[] { 0x80 }, request);
        StringAssert.Contains(Encoding.Latin1.GetString(request), "\r\nX-A: \u0080\r\n");
    }

    [TestMethod]
    public void BuildConnectRequest_WithUtf8_SendsTheProxyHeaderAndUserAgentInUtf8()
    {
        var request = HttpProxyTunnel.BuildConnectRequest(
            "example.com", 80,
            HttpProxy,
            new HttpProxyTunnelOptions("é", Encoding.UTF8) { ProxyHeaders = ["X-A: é"], CommandLineTextEncoding = Encoding.UTF8 }, null);

        Assert.AreEqual(
            "CONNECT example.com:80 HTTP/1.1\r\nHost: example.com:80\r\nUser-Agent: Ã©\r\nProxy-Connection: Keep-Alive\r\nX-A: Ã©\r\n\r\n",
            Encoding.Latin1.GetString(request));
    }

    [TestMethod]
    public void BuildConnectRequest_ByDefault_SendsTheProxyHeaderInLatin1()
    {
        var request = HttpProxyTunnel.BuildConnectRequest(
            "example.com", 80,
            HttpProxy,
            HttpProxyTunnelOptions.Default with { ProxyHeaders = ["X-A: é"] }, null);

        StringAssert.Contains(Encoding.Latin1.GetString(request), "\r\nX-A: é\r\n");
    }

    [TestMethod]
    public void BuildConnectRequest_ForAnIPv6Target_BracketsTheAddress()
    {
        // curl -p -x 127.0.0.1:18263 http://[::1]:81/
        var request = HttpProxyTunnel.BuildConnectRequest(
            "::1", 81,
            HttpProxy,
            HttpProxyTunnelOptions.Default, null);

        Assert.AreEqual(
            "CONNECT [::1]:81 HTTP/1.1\r\nHost: [::1]:81\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n",
            Encoding.Latin1.GetString(request));
    }

    [TestMethod]
    public void BuildConnectRequest_ForAnAlreadyBracketedTarget_LeavesItAsItIs()
    {
        var request = HttpProxyTunnel.BuildConnectRequest(
            "[::1]", 81,
            HttpProxy,
            HttpProxyTunnelOptions.Default, null);

        StringAssert.StartsWith(Encoding.Latin1.GetString(request), "CONNECT [::1]:81 HTTP/1.1\r\n");
    }

    [TestMethod]
    public void BuildConnectRequest_ForAnHttp10Proxy_UsesHttp10()
    {
        // curl --proxy1.0 127.0.0.1:18264 -p http://example.com/
        var request = HttpProxyTunnel.BuildConnectRequest(
            "example.com", 80,
            HttpProxy with { Kind = ProxyKind.Http10 },
            HttpProxyTunnelOptions.Default, null);

        Assert.AreEqual(
            "CONNECT example.com:80 HTTP/1.0\r\nHost: example.com:80\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n",
            Encoding.Latin1.GetString(request));
    }

    [TestMethod]
    public void BuildConnectRequest_WithACustomUserAgent_SendsIt()
    {
        // curl -p -x 127.0.0.1:18266 -A Agent/1 http://example.com/
        var request = HttpProxyTunnel.BuildConnectRequest(
            "example.com", 80,
            HttpProxy,
            HttpProxyTunnelOptions.Default with { UserAgent = "Agent/1" }, null);

        Assert.AreEqual(
            "CONNECT example.com:80 HTTP/1.1\r\nHost: example.com:80\r\nUser-Agent: Agent/1\r\nProxy-Connection: Keep-Alive\r\n\r\n",
            Encoding.Latin1.GetString(request));
    }

    [TestMethod]
    public void BuildConnectRequest_WithNoUserAgent_OmitsTheHeader()
    {
        var request = HttpProxyTunnel.BuildConnectRequest(
            "example.com", 80,
            HttpProxy,
            HttpProxyTunnelOptions.Default with { UserAgent = null }, null);

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
        var request = HttpProxyTunnel.BuildConnectRequest(
            "example.com", 80,
            HttpProxy,
            HttpProxyTunnelOptions.Default with { ProxyHeaders = proxyHeaders }, null);

        Assert.AreEqual($"CONNECT example.com:80 HTTP/1.1\r\n{expectedHeaders}\r\n", Encoding.Latin1.GetString(request));
    }

    [TestMethod]
    public void BuildConnectRequest_WithAProxyAuthorizationProxyHeader_ReplacesTheCredential()
    {
        // curl -s -x http://127.0.0.1:18341 -p -U u:p --proxy-header "Proxy-Authorization: Z" --proxy-header "X-B: 2" http://example.com/
        var proxy = HttpProxy with { Credential = new NetworkCredential("u", "p") };

        var request = HttpProxyTunnel.BuildConnectRequest(
            "example.com", 80,
            proxy,
            HttpProxyTunnelOptions.Default with { ProxyHeaders = ["Proxy-Authorization: Z", "X-B: 2"] }, "Basic dTpw");

        Assert.AreEqual(
            "CONNECT example.com:80 HTTP/1.1\r\nHost: example.com:80\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\nProxy-Authorization: Z\r\nX-B: 2\r\n\r\n",
            Encoding.Latin1.GetString(request));
    }

    [TestMethod]
    public void BuildConnectRequest_WithEmptyProxyHeadersForCurlsOwnHeaders_SendsOnlyTheOthers()
    {
        // curl -s -x http://127.0.0.1:18403 -p -U u:p --proxy-header Host: --proxy-header Proxy-Authorization: --proxy-header Proxy-Connection: http://example.com/
        var proxy = HttpProxy with { Credential = new NetworkCredential("u", "p") };

        var request = HttpProxyTunnel.BuildConnectRequest(
            "example.com", 80,
            proxy,
            HttpProxyTunnelOptions.Default with { ProxyHeaders = ["Host:", "Proxy-Authorization:", "Proxy-Connection:"] }, "Basic dTpw");

        Assert.AreEqual("CONNECT example.com:80 HTTP/1.1\r\nUser-Agent: curl/8.21.0\r\n\r\n", Encoding.Latin1.GetString(request));
    }

    [TestMethod]
    public void Default_ProxyHeaders_IsEmpty()
    {
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

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        Assert.AreEqual(expected, result.StatusCode);
        Assert.IsNull(result.RecvErrorMessage);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 Connection established\r\nX-Proxy: yes\r\n\r\n")]
    [DataRow("HTTP/1.0 200 OK\n\n")]
    public async Task ReadReplyAsync_ReturnsTheHeadExactlyAsReadAndNothingAfterIt(string head)
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(head + "HTTP/1.1 200 OK\r\n"));

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        Assert.AreEqual(head, Encoding.Latin1.GetString(result.Head.Span));
    }

    [TestMethod]
    public async Task ReadReplyAsync_WhenTheProxyClosesBeforeTheHeadEnds_ReturnsNoHead()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\n"));

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        Assert.AreEqual("Proxy CONNECT aborted", result.RecvErrorMessage);
        Assert.IsTrue(result.Head.IsEmpty);
    }

    [TestMethod]
    public async Task ReadReplyAsync_ReturnsEveryProxyAuthenticateValueInOrder()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(
            "HTTP/1.1 407 Proxy Authentication Required\r\nproxy-authenticate:  Digest realm=\"r\", nonce=\"abc\"\t\r\nX-A: b\r\nProxy-Authenticate: Basic realm=\"r\"\r\n\r\n"));

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "Digest realm=\"r\", nonce=\"abc\"", "Basic realm=\"r\"" }, result.ProxyAuthenticate.ToArray());
    }

    [TestMethod]
    [DataRow("", 0L, true)]
    [DataRow("Content-Length: 12\r\n", 12L, true)]
    [DataRow("Content-Length: 12\r\nContent-Length: x\r\n", 12L, true)]
    [DataRow("Content-Length: -1\r\n", 0L, true)]
    [DataRow("Connection: keep-alive\r\n", 0L, true)]
    [DataRow("Connection: close\r\n", 0L, false)]
    [DataRow("Proxy-Connection: Keep-Alive, Close\r\n", 0L, false)]
    [DataRow("Transfer-Encoding: gzip, chunked\r\n", 0L, false)]
    [DataRow("Transfer-Encoding: gzip\r\n", 0L, true)]
    [DataRow(": close\r\nno colon\r\n", 0L, true)]
    public async Task ReadReplyAsync_ReadsTheBodyLengthAndWhetherTheConnectionIsReusable(string fields, long contentLength, bool reusable)
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes($"HTTP/1.1 407 Proxy Authentication Required\r\n{fields}\r\n"));

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        Assert.AreEqual(contentLength, result.ContentLength);
        Assert.AreEqual(reusable, result.LeavesConnectionReusable);
        Assert.IsEmpty(result.ProxyAuthenticate);
    }

    [TestMethod]
    public void ProxyAuthenticate_OfADefaultReply_IsEmpty()
    {
        Assert.IsEmpty(default(HttpProxyTunnelReply).ProxyAuthenticate);
    }

    [TestMethod]
    public async Task DiscardBodyAsync_ReadsExactlyTheBody()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(new string('a', 5000) + "next"));

        var discarded = await HttpProxyTunnel.DiscardBodyAsync(connection, 5000, CancellationToken.None);

        Assert.IsTrue(discarded);
        Assert.AreEqual("next".Length, connection.UnreadCount);
    }

    [TestMethod]
    public async Task DiscardBodyAsync_WhenTheProxyClosesFirst_ReturnsFalse()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes("abc"));

        var discarded = await HttpProxyTunnel.DiscardBodyAsync(connection, 4, CancellationToken.None);

        Assert.IsFalse(discarded);
    }

    [TestMethod]
    public async Task DiscardBodyAsync_WhenReadingFails_ReturnsFalse()
    {
        var connection = new ScriptedConnection([]) { ReadException = new IOException("reset") };

        var discarded = await HttpProxyTunnel.DiscardBodyAsync(connection, 1, CancellationToken.None);

        Assert.IsFalse(discarded);
    }

    [TestMethod]
    public void Default_ProxyAuthSchemes_IsBasicAndProxyAuthenticator_IsNull()
    {
        Assert.AreEqual(HttpAuthSchemes.Basic, HttpProxyTunnelOptions.Default.ProxyAuthSchemes);
        Assert.IsNull(HttpProxyTunnelOptions.Default.ProxyAuthenticator);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("HTTP/1.1 200 Connection established\r\nX-A: b")]
    public async Task ReadReplyAsync_WhenTheProxyClosesBeforeTheHeaderBlockEnds_ReportsProxyConnectAborted(string reply)
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(reply));

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

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

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

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

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        Assert.AreEqual(HttpProxyTunnelReply.Failed("Too large response headers: 307762 > 307200"), result);
    }

    [TestMethod]
    public async Task ReadReplyAsync_LeavesTheBytesAfterTheHeaderBlockUnread()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\n\r\ntunnel"));

        await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        Assert.AreEqual("tunnel".Length, connection.UnreadCount);
    }
}
