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
            HttpProxyTunnelOptions.Default);

        Assert.AreEqual(
            "CONNECT example.com:80 HTTP/1.1\r\nHost: example.com:80\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n",
            Encoding.Latin1.GetString(request));
    }

    [TestMethod]
    public void BuildConnectRequest_WithACredential_SendsProxyAuthorizationBeforeUserAgent()
    {
        // curl -x http://127.0.0.1:18262 -U user:p@ss https://example.com:8443/path
        var proxy = HttpProxy with { Credential = new NetworkCredential("user", "p@ss") };

        var request = HttpProxyTunnel.BuildConnectRequest(
            "example.com", 8443,
            proxy,
            HttpProxyTunnelOptions.Default);

        Assert.AreEqual(
            "CONNECT example.com:8443 HTTP/1.1\r\nHost: example.com:8443\r\nProxy-Authorization: Basic dXNlcjpwQHNz\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n",
            Encoding.Latin1.GetString(request));
    }

    [TestMethod]
    public void BuildConnectRequest_EncodesTheCredentialWithTheGivenEncoding()
    {
        var proxy = HttpProxy with { Credential = new NetworkCredential("é", "p") };

        var request = HttpProxyTunnel.BuildConnectRequest(
            "example.com", 80,
            proxy,
            HttpProxyTunnelOptions.Default with { CredentialEncoding = Encoding.Latin1 });

        StringAssert.Contains(Encoding.Latin1.GetString(request), "Proxy-Authorization: Basic 6Tpw\r\n");
    }

    [TestMethod]
    public void BuildConnectRequest_ForAnIPv6Target_BracketsTheAddress()
    {
        // curl -p -x 127.0.0.1:18263 http://[::1]:81/
        var request = HttpProxyTunnel.BuildConnectRequest(
            "::1", 81,
            HttpProxy,
            HttpProxyTunnelOptions.Default);

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
            HttpProxyTunnelOptions.Default);

        StringAssert.StartsWith(Encoding.Latin1.GetString(request), "CONNECT [::1]:81 HTTP/1.1\r\n");
    }

    [TestMethod]
    public void BuildConnectRequest_ForAnHttp10Proxy_UsesHttp10()
    {
        // curl --proxy1.0 127.0.0.1:18264 -p http://example.com/
        var request = HttpProxyTunnel.BuildConnectRequest(
            "example.com", 80,
            HttpProxy with { Kind = ProxyKind.Http10 },
            HttpProxyTunnelOptions.Default);

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
            HttpProxyTunnelOptions.Default with { UserAgent = "Agent/1" });

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
            HttpProxyTunnelOptions.Default with { UserAgent = null });

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
            HttpProxyTunnelOptions.Default with { ProxyHeaders = proxyHeaders });

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
            HttpProxyTunnelOptions.Default with { ProxyHeaders = ["Proxy-Authorization: Z", "X-B: 2"] });

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
            HttpProxyTunnelOptions.Default with { ProxyHeaders = ["Host:", "Proxy-Authorization:", "Proxy-Connection:"] });

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

        Assert.AreEqual(new HttpProxyTunnelReply(expected, null), result);
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
