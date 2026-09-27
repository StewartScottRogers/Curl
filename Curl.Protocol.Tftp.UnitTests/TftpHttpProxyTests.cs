using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Tftp.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Tftp;

/// <summary>
/// Pins <c>tftp://</c> through an HTTP proxy against curl 8.21.0's Schannel build
/// (ADR-0056, rule 4), measured by BL-330 and BL-345 against a loopback listener: the
/// MASQUE <c>connect-udp</c> request goes to the proxy, no datagram is sent, and the
/// transfer ends with exit 7 <c>bind() failed; Invalid arguments</c>.
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
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 18331, UseTls: false), proxyConnector.Targets.Single());
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
    public async Task ExecuteAsync_SocksProxy_IsNotSentTheMasqueRequest()
    {
        var datagramConnector = DatagramConnector();
        var proxyConnector = new RecordingConnector(ConnectResult.Connected(new RecordingConnection()));

        await new TftpProtocolHandler(datagramConnector, proxyConnector)
            .ExecuteAsync(Context("tftp://example.com/f", new ProxyEndpoint(ProxyKind.Socks5, "127.0.0.1", 1080, null)));

        Assert.IsEmpty(proxyConnector.Targets);
        Assert.HasCount(1, datagramConnector.Opens);
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
