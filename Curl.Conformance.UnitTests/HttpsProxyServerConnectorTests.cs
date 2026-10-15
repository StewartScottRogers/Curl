using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>Pins that <see cref="HttpsProxyServerConnector"/> serves the sws emulation's proxy port behind TLS on <c>%HTTPSPROXYPORT</c> and passes every other port on.</summary>
[TestClass]
public sealed class HttpsProxyServerConnectorTests
{
    private const string ReplyCase = "<reply>\n<data>\nHTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nhi\n</data>\n<connect>\nHTTP/1.1 200 Mighty fine indeed\r\n\r\n</connect>\n</reply>\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ConnectAsync_AnotherPort_ReachesTheWrappedConnectorAsItIs()
    {
        using X509Certificate2 certificate = MailTlsServerConnectorTests.CreateCertificate();
        HttpsProxyServerConnector connector = new(certificate, new NoListenPortConnector(new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System)));

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", NoListenPortConnector.NoListenPort, false), TestContext.CancellationToken);

        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ConnectAsync_HttpsProxyPort_CompletesTheHandshakeAsTheProxyPort()
    {
        using X509Certificate2 certificate = MailTlsServerConnectorTests.CreateCertificate();
        HttpsProxyServerConnector connector = new(certificate, new SwsHttpServerConnector(ParsedTestCase.From(ReplyCase), TimeProvider.System));

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", HttpsProxyServerConnector.HttpsProxyPort, false), TestContext.CancellationToken);
        await using (SslStream client = await MailTlsServerConnectorTests.HandshakeAsync(result.Connection!))
        {
            Assert.IsTrue(client.IsAuthenticated);
            Assert.AreEqual(string.Empty, client.NegotiatedApplicationProtocol.ToString());
        }

        Assert.AreEqual(HttpsProxyServerConnector.HttpsProxyPort, ((IPEndPoint)result.Connection!.RemoteEndPoint!).Port);
        Assert.IsFalse(result.Connection.IsSecure);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ConnectAsync_ProxiedGet_IsAnsweredAndRecordedAsAProxyRequest()
    {
        using X509Certificate2 certificate = MailTlsServerConnectorTests.CreateCertificate();
        SwsHttpServerConnector sws = new(ParsedTestCase.From(ReplyCase), TimeProvider.System);
        HttpsProxyServerConnector connector = new(certificate, sws);
        const string request = "GET http://example.com/1 HTTP/1.1\r\nHost: example.com\r\n\r\n";

        string reply = await ExchangeAsync(connector, request, "HTTP/1.1 200 OK".Length);

        Assert.AreEqual("HTTP/1.1 200 OK", reply);
        Assert.AreEqual(request, Encoding.Latin1.GetString(sws.ProxyReceivedBytes.ToArray()));
        Assert.IsEmpty(sws.ReceivedBytes.ToArray());
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ConnectAsync_Connect_IsAnsweredAndRecordedAsAProxyRequest()
    {
        using X509Certificate2 certificate = MailTlsServerConnectorTests.CreateCertificate();
        SwsHttpServerConnector sws = new(ParsedTestCase.From(ReplyCase), TimeProvider.System);
        HttpsProxyServerConnector connector = new(certificate, sws);
        const string request = "CONNECT example.com:443 HTTP/1.1\r\nHost: example.com:443\r\n\r\n";

        string reply = await ExchangeAsync(connector, request, "HTTP/1.1 200 Mighty".Length);

        Assert.AreEqual("HTTP/1.1 200 Mighty", reply);
        Assert.AreEqual(request, Encoding.Latin1.GetString(sws.ProxyReceivedBytes.ToArray()));
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ConnectAsync_SecondProxiedGetOnTheSameConnection_IsAnsweredToo()
    {
        using X509Certificate2 certificate = MailTlsServerConnectorTests.CreateCertificate();
        SwsHttpServerConnector sws = new(ParsedTestCase.From(ReplyCase), TimeProvider.System);
        HttpsProxyServerConnector connector = new(certificate, sws);
        const string request = "GET http://example.com/1 HTTP/1.1\r\nHost: example.com\r\n\r\n";
        int replyLength = "HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nhi\n".Length;
        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", HttpsProxyServerConnector.HttpsProxyPort, false), TestContext.CancellationToken);
        await using SslStream client = await MailTlsServerConnectorTests.HandshakeAsync(result.Connection!);

        await client.WriteAsync(Encoding.Latin1.GetBytes(request), TestContext.CancellationToken);
        string first = await MailTlsServerConnectorTests.ReadAsync(client, replyLength);
        await client.WriteAsync(Encoding.Latin1.GetBytes(request), TestContext.CancellationToken);
        string second = await MailTlsServerConnectorTests.ReadAsync(client, replyLength);

        Assert.AreEqual(first, second);
        Assert.StartsWith("HTTP/1.1 200 OK", second);
        Assert.AreEqual(request + request, Encoding.Latin1.GetString(sws.ProxyReceivedBytes.ToArray()));
    }

    private async Task<string> ExchangeAsync(HttpsProxyServerConnector connector, string request, int replyLength)
    {
        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", HttpsProxyServerConnector.HttpsProxyPort, false), TestContext.CancellationToken);
        await using SslStream client = await MailTlsServerConnectorTests.HandshakeAsync(result.Connection!);
        await client.WriteAsync(Encoding.Latin1.GetBytes(request), TestContext.CancellationToken);
        await client.FlushAsync(TestContext.CancellationToken);
        return await MailTlsServerConnectorTests.ReadAsync(client, replyLength);
    }
}
