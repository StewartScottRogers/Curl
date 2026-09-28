using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Tftp.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Tftp;

/// <summary>
/// Pins <c>tftp://</c> through a proxy against curl 8.21.0's Schannel build (ADR-0056,
/// rule 4; ADR-0096), measured by BL-330, BL-345 and BL-398 against a loopback listener:
/// through an HTTP or HTTPS proxy the MASQUE <c>connect-udp</c> request goes to the proxy,
/// no datagram is sent, and the proxy's reply decides the exit 7 or 56 failure; through a
/// SOCKS proxy nothing is sent and the transfer ends with exit 97.
/// </summary>
[TestClass]
public sealed class TftpHttpProxyTests
{
    private const string BindFailed = "bind() failed; Invalid arguments";

    /// <summary>What <c>curl -sS -x http://127.0.0.1:18331 tftp://example.com/f</c> sent (BL-330).</summary>
    private static readonly byte[] ExpectedRequest = Encoding.ASCII.GetBytes(
        "GET http://127.0.0.1:18331/.well-known/masque/udp/example.com/69/ HTTP/1.1\r\n" +
        "Host: 127.0.0.1:18331\r\n" +
        "User-Agent: curl/8.21.0\r\n" +
        "Proxy-Connection: Keep-Alive\r\n" +
        "Connection: Upgrade\r\n" +
        "Upgrade: connect-udp\r\n" +
        "Capsule-Protocol: ?1\r\n" +
        "\r\n");

    [TestMethod]
    public async Task ExecuteAsync_HttpProxy_SendsMeasuredMasqueRequestToProxyAndSendsNoDatagram()
    {
        var connection = new RecordingConnection();
        var proxyConnector = new RecordingConnector(ConnectResult.Connected(connection));
        var datagramConnector = DatagramConnector();

        var result = await new TftpProtocolHandler(datagramConnector, proxyConnector)
            .ExecuteAsync(Context("tftp://example.com/f", HttpProxy()));

        AssertBindFailed(result);
        CollectionAssert.AreEqual(ExpectedRequest, connection.Written);
        Assert.IsEmpty(datagramConnector.Opens);
        Assert.IsTrue(connection.IsDisposed);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 18331, UseTls: false) { IsForwardProxy = true }, proxyConnector.Targets.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_Http10Proxy_SendsMeasuredHttp10Request()
    {
        // Measured: curl -sS --proxy1.0 127.0.0.1:18345 tftp://example.com/f
        var connection = new RecordingConnection();

        var result = await new TftpProtocolHandler(DatagramConnector(), new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(Context("tftp://example.com/f", new ProxyEndpoint(ProxyKind.Http10, "127.0.0.1", 18345, null)));

        AssertBindFailed(result);
        Assert.AreEqual(
            "GET http://127.0.0.1:18345/.well-known/masque/udp/example.com/69/ HTTP/1.0\r\n" +
            "Host: 127.0.0.1:18345\r\n" +
            "User-Agent: curl/8.21.0\r\n" +
            "Proxy-Connection: Keep-Alive\r\n" +
            "Connection: Upgrade\r\n" +
            "Upgrade: connect-udp\r\n" +
            "Capsule-Protocol: ?1\r\n" +
            "\r\n",
            Encoding.ASCII.GetString(connection.Written));
    }

    [TestMethod]
    public async Task ExecuteAsync_ProxyCredentialUserAgentAndIPv6Host_SendsMeasuredRequest()
    {
        // Measured: curl -sS -x http://127.0.0.1:18345 -U u:p -A X/1 tftp://[::1]:70/f
        var connection = new RecordingConnection();
        var proxy = new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 18345, new NetworkCredential("u", "p"));
        var context = Context("tftp://[::1]:70/f", proxy, new HttpRequestOptions { UserAgent = "X/1" });

        var result = await new TftpProtocolHandler(DatagramConnector(), new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(context);

        AssertBindFailed(result);
        Assert.AreEqual(
            "GET http://127.0.0.1:18345/.well-known/masque/udp/%3A%3A1/70/ HTTP/1.1\r\n" +
            "Host: 127.0.0.1:18345\r\n" +
            "Proxy-Authorization: Basic dTpw\r\n" +
            "User-Agent: X/1\r\n" +
            "Proxy-Connection: Keep-Alive\r\n" +
            "Connection: Upgrade\r\n" +
            "Upgrade: connect-udp\r\n" +
            "Capsule-Protocol: ?1\r\n" +
            "\r\n",
            Encoding.ASCII.GetString(connection.Written));
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyUserAgent_SendsNoUserAgent()
    {
        var connection = new RecordingConnection();
        var context = Context("tftp://example.com/f", HttpProxy(), new HttpRequestOptions { UserAgent = "" });

        await new TftpProtocolHandler(DatagramConnector(), new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(context);

        Assert.DoesNotContain("User-Agent", Encoding.ASCII.GetString(connection.Written));
    }

    [TestMethod]
    public async Task ExecuteAsync_GivenCredentialEncoding_EncodesProxyCredentialWithIt()
    {
        var connection = new RecordingConnection();
        var proxy = new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 18331, new NetworkCredential("ü", "p"));

        await new TftpProtocolHandler(DatagramConnector(), new RecordingConnector(ConnectResult.Connected(connection)), Encoding.Latin1)
            .ExecuteAsync(Context("tftp://example.com/f", proxy));

        StringAssert.Contains(Encoding.ASCII.GetString(connection.Written), "Proxy-Authorization: Basic /Dpw\r\n");
    }

    [TestMethod]
    [DataRow("::1")]
    [DataRow("[::1]")]
    public async Task ExecuteAsync_IPv6Proxy_BracketsItInTheRequest(string proxyHost)
    {
        var connection = new RecordingConnection();

        await new TftpProtocolHandler(DatagramConnector(), new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(Context("tftp://example.com/f", new ProxyEndpoint(ProxyKind.Http, proxyHost, 8080, null)));

        StringAssert.StartsWith(
            Encoding.ASCII.GetString(connection.Written),
            "GET http://[::1]:8080/.well-known/masque/udp/example.com/69/ HTTP/1.1\r\nHost: [::1]:8080\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_HttpProxyAndNoFileName_StillFailsWithBind()
    {
        // Measured: curl -sS -x http://127.0.0.1:18345 tftp://example.com/ sends the same
        // request and exits 7, not 71 Missing filename.
        var connection = new RecordingConnection();

        var result = await new TftpProtocolHandler(DatagramConnector(), new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(Context("tftp://example.com/", HttpProxy()));

        AssertBindFailed(result);
        CollectionAssert.AreEqual(ExpectedRequest, connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_HttpProxyWithoutProxyConnector_SendsNothingAndFailsWithBind()
    {
        var datagramConnector = DatagramConnector();

        var result = await new TftpProtocolHandler(datagramConnector).ExecuteAsync(Context("tftp://example.com/f", HttpProxy()));

        AssertBindFailed(result);
        Assert.IsEmpty(datagramConnector.Opens);
    }

    [TestMethod]
    public async Task ExecuteAsync_ProxyRefusesConnection_ReturnsConnectorsFailureUnchanged()
    {
        var proxyConnector = new RecordingConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect to 127.0.0.1 port 18331"));

        var result = await new TftpProtocolHandler(DatagramConnector(), proxyConnector).ExecuteAsync(Context("tftp://example.com/f", HttpProxy()));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to 127.0.0.1 port 18331", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoProxy_OpensTheDatagramChannelAsBefore()
    {
        var datagramConnector = DatagramConnector();
        var proxyConnector = new RecordingConnector(ConnectResult.Connected(new RecordingConnection()));

        await new TftpProtocolHandler(datagramConnector, proxyConnector).ExecuteAsync(Context("tftp://example.com/f", proxy: null));

        CollectionAssert.AreEqual(new[] { ("example.com", 69) }, datagramConnector.Opens);
        Assert.IsEmpty(proxyConnector.Targets);
    }

    [TestMethod]
    [DataRow(ProxyKind.Socks4, "tftp://example.com/f")]
    [DataRow(ProxyKind.Socks4a, "tftp://example.com/f")]
    [DataRow(ProxyKind.Socks5, "tftp://example.com/f")]
    [DataRow(ProxyKind.Socks5Hostname, "tftp://example.com/f")]
    [DataRow(ProxyKind.Socks5, "tftp://example.com/")]
    public async Task ExecuteAsync_SocksProxy_SendsNothingAndFailsWithSendFailure(ProxyKind kind, string url)
    {
        // Measured (BL-398): curl -sS --socks5 127.0.0.1:18398 tftp://example.com/f, and
        // --socks4, --socks4a, --socks5-hostname and a URL with no file name, all exit 97
        // with nothing sent to the proxy.
        var datagramConnector = DatagramConnector();
        var proxyConnector = new RecordingConnector(ConnectResult.Connected(new RecordingConnection()));

        var result = await new TftpProtocolHandler(datagramConnector, proxyConnector)
            .ExecuteAsync(Context(url, new ProxyEndpoint(kind, "127.0.0.1", 18398, null)));

        Assert.AreEqual(CurlExitCode.Proxy, result.ExitCode);
        Assert.AreEqual("Send failure: Socket is not connected", result.ErrorMessage);
        Assert.IsEmpty(proxyConnector.Targets);
        Assert.IsEmpty(datagramConnector.Opens);
    }

    [TestMethod]
    public async Task ExecuteAsync_HttpsProxy_SendsMeasuredMasqueRequestOverTls()
    {
        // Measured (BL-398): curl -sS --proxy-insecure -x https://127.0.0.1:18398 tftp://example.com/f
        // against a TLS loopback proxy answering 200.
        var connection = new RecordingConnection();
        var proxyConnector = new RecordingConnector(ConnectResult.Connected(connection));
        var datagramConnector = DatagramConnector();

        var result = await new TftpProtocolHandler(datagramConnector, proxyConnector)
            .ExecuteAsync(Context("tftp://example.com/f", new ProxyEndpoint(ProxyKind.Https, "127.0.0.1", 18398, null)));

        AssertBindFailed(result);
        Assert.AreEqual(
            "GET https://127.0.0.1:18398/.well-known/masque/udp/example.com/69/ HTTP/1.1\r\n" +
            "Host: 127.0.0.1:18398\r\n" +
            "User-Agent: curl/8.21.0\r\n" +
            "Proxy-Connection: Keep-Alive\r\n" +
            "Connection: Upgrade\r\n" +
            "Upgrade: connect-udp\r\n" +
            "Capsule-Protocol: ?1\r\n" +
            "\r\n",
            Encoding.ASCII.GetString(connection.Written));
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 18398, UseTls: true) { IsForwardProxy = true }, proxyConnector.Targets.Single());
        Assert.IsEmpty(datagramConnector.Opens);
    }

    [TestMethod]
    public async Task ExecuteAsync_HttpsProxyHandshakeFails_ReturnsConnectorsFailureUnchanged()
    {
        // Measured (BL-398): a proxy that never answers the TLS handshake is exit 35.
        const string handshakeFailed = "schannel: failed to receive handshake, SSL/TLS connection failed";
        var proxyConnector = new RecordingConnector(ConnectResult.Failed(CurlExitCode.SslConnectError, handshakeFailed));

        var result = await new TftpProtocolHandler(DatagramConnector(), proxyConnector)
            .ExecuteAsync(Context("tftp://example.com/f", new ProxyEndpoint(ProxyKind.Https, "127.0.0.1", 18398, null)));

        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual(handshakeFailed, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(ProxyKind.Http)]
    [DataRow(ProxyKind.Https)]
    public async Task ExecuteAsync_ProxyAnswers403_FailsWithTunnelFailedResponse403(ProxyKind kind)
    {
        // Measured (BL-398) through both an HTTP and an HTTPS proxy.
        var connection = new RecordingConnection("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n");

        var result = await new TftpProtocolHandler(DatagramConnector(), new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(Context("tftp://example.com/f", new ProxyEndpoint(kind, "127.0.0.1", 18398, null)));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("CONNECT-UDP tunnel failed, response 403", result.ErrorMessage);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: connect-udp\r\nCapsule-Protocol: ?1\r\n\r\n", BindFailed)]
    [DataRow("HTTP/1.1 204 No\r\n\r\n", BindFailed)]
    [DataRow("HTTP/1.1 200 OK\n\n", BindFailed)]
    [DataRow("HTTP/1.1 302 Found\r\nContent-Length: 0\r\n\r\n", "CONNECT-UDP tunnel failed, response 302")]
    [DataRow("HTTP/1.1 407 Proxy Auth\r\nContent-Length: 0\r\n\r\n", "CONNECT-UDP tunnel failed, response 407")]
    [DataRow("garbage\r\n\r\n", "CONNECT-UDP tunnel failed, response 0")]
    [DataRow("HTTP/1.1 2000 OK\r\n\r\n", "CONNECT-UDP tunnel failed, response 0")]
    [DataRow("HTTP/1.1 2x0 OK\r\n\r\n", "CONNECT-UDP tunnel failed, response 0")]
    [DataRow("HTTP/1.1\r\n\r\n", "CONNECT-UDP tunnel failed, response 0")]
    [DataRow("\r\n", "CONNECT-UDP tunnel failed, response 0")]
    public async Task ExecuteAsync_ProxyReply_FailsAsMeasured(string reply, string expectedMessage)
    {
        // Measured (BL-398) for 101, 204, 302, 407 and "garbage"; a 101 or 2xx accepts the
        // tunnel, anything else refuses it, and a first line that is not a status line is 0.
        var connection = new RecordingConnection(reply + "unread");

        var result = await new TftpProtocolHandler(DatagramConnector(), new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(Context("tftp://example.com/f", HttpProxy()));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(expectedMessage, result.ErrorMessage);
        Assert.AreEqual(reply.Length, connection.ReplyBytesRead);
    }

    [TestMethod]
    public async Task ExecuteAsync_ProxyClosesMidReply_FailsWithProxyConnectAborted()
    {
        var connection = new RecordingConnection("HTTP/1.1 200 OK\r\nX: y\r\n");

        var result = await new TftpProtocolHandler(DatagramConnector(), new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(Context("tftp://example.com/f", HttpProxy()));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Proxy CONNECT aborted", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ProxyClosesWithoutReply_FailsWithProxyConnectAborted()
    {
        // Measured (BL-398): curl -sS -x http://127.0.0.1:18398 tftp://example.com/f against
        // a proxy that closes without replying.
        var connection = new RecordingConnection("");

        var result = await new TftpProtocolHandler(DatagramConnector(), new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(Context("tftp://example.com/f", HttpProxy()));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Proxy CONNECT aborted", result.ErrorMessage);
    }

    private static void AssertBindFailed(TransferResult result)
    {
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(BindFailed, result.ErrorMessage);
    }

    private static ProxyEndpoint HttpProxy() => new(ProxyKind.Http, "127.0.0.1", 18331, null);

    // A datagram connector whose channel answers with one short DATA block, so a transfer
    // that reaches it ends at once.
    private static RecordingDatagramConnector DatagramConnector() =>
        new(DatagramOpenResult.Opened(new ScriptedDatagramChannel(
            new IPEndPoint(IPAddress.Loopback, 69),
            [([0, 3, 0, 1, (byte)'x'], new IPEndPoint(IPAddress.Loopback, 50123))])));

    private static TransferContext Context(string url, ProxyEndpoint? proxy, HttpRequestOptions? http = null) =>
        new() { Url = CurlUrl.Parse(url), Output = new MemoryStream(), Proxy = proxy, Http = http };
}
