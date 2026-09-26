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
            new ConnectTarget("example.com", 80, UseTls: false),
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
            new ConnectTarget("example.com", 8443, UseTls: true),
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
            new ConnectTarget("example.com", 80, UseTls: false),
            proxy,
            HttpProxyTunnelOptions.Default with { CredentialEncoding = Encoding.Latin1 });

        StringAssert.Contains(Encoding.Latin1.GetString(request), "Proxy-Authorization: Basic 6Tpw\r\n");
    }

    [TestMethod]
    public void BuildConnectRequest_ForAnIPv6Target_BracketsTheAddress()
    {
        // curl -p -x 127.0.0.1:18263 http://[::1]:81/
        var request = HttpProxyTunnel.BuildConnectRequest(
            new ConnectTarget("::1", 81, UseTls: false),
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
            new ConnectTarget("[::1]", 81, UseTls: false),
            HttpProxy,
            HttpProxyTunnelOptions.Default);

        StringAssert.StartsWith(Encoding.Latin1.GetString(request), "CONNECT [::1]:81 HTTP/1.1\r\n");
    }

    [TestMethod]
    public void BuildConnectRequest_ForAnHttp10Proxy_UsesHttp10()
    {
        // curl --proxy1.0 127.0.0.1:18264 -p http://example.com/
        var request = HttpProxyTunnel.BuildConnectRequest(
            new ConnectTarget("example.com", 80, UseTls: false),
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
            new ConnectTarget("example.com", 80, UseTls: false),
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
            new ConnectTarget("example.com", 80, UseTls: false),
            HttpProxy,
            HttpProxyTunnelOptions.Default with { UserAgent = null });

        Assert.AreEqual(
            "CONNECT example.com:80 HTTP/1.1\r\nHost: example.com:80\r\nProxy-Connection: Keep-Alive\r\n\r\n",
            Encoding.Latin1.GetString(request));
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
