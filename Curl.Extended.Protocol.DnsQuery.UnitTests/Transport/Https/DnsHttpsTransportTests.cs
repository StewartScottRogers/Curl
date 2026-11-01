using System.Text;
using Curl.Extended.Protocol.DnsQuery.Client;
using Curl.Extended.Protocol.DnsQuery.Transport;
using Curl.Extended.Protocol.DnsQuery.Transport.Https;
using Curl.Extended.Protocol.DnsQuery.UnitTests.Transport.Tls;
using Curl.Extended.Protocol.DnsQuery.UnitTests.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.DnsQuery.UnitTests.Transport.Https;

/// <summary>
/// Checks DNS over HTTPS (RFC 8484) over a fake connection: port 443 and the TLS handshake checking the server's host
/// (section 5), the POST of the query (section 4.1) and the answer read from the 200 response's body (section 4.2).
/// </summary>
[TestClass]
public sealed class DnsHttpsTransportTests
{
    private static readonly byte[] Query = new DnsWire().Header(0x1234, 0x0100, 1, 0, 0, 0).Question(DnsWire.Name("example.com"), 1, 1).ToArray();
    private static readonly byte[] Answer = DnsWire.SingleAnswer(1, [192, 0, 2, 1]);

    [TestMethod]
    public void DefaultPort_Rfc8484Section5_IsTheHttpsPort443() =>
        Assert.AreEqual(443, new DnsHttpsTransport(new ScriptedDnsServer((_, _) => [], []), new HandshakingTlsProvider()).DefaultPort);

    [TestMethod]
    public void TransportOf_Https_GivesTheDnsOverHttpsTransport()
    {
        var server = new ScriptedDnsServer((_, _) => [], []);

        IDnsQueryTransport transport = DnsQueryTransportSelection.TransportOf("HTTPS", new ProtocolHandlerServices(server, server, new HandshakingTlsProvider(), null!));

        Assert.IsInstanceOfType<DnsHttpsTransport>(transport);
    }

    [TestMethod]
    public async Task ExchangeAsync_Rfc8484Section4_1_HandshakesThenPostsTheQueryAndReadsTheAnswer()
    {
        var server = new ScriptedDnsServer((_, _) => [], ResponseOf(Answer));
        int writtenAtHandshake = -1;
        var tls = new HandshakingTlsProvider { OnHandshake = () => writtenAtHandshake = server.TcpWritten.Count };

        byte[] answer = await new DnsHttpsTransport(server, tls).ExchangeAsync("dns.example", 443, Query, ContextOf());

        CollectionAssert.AreEqual(Answer, answer);
        Assert.AreEqual(0, writtenAtHandshake);
        Assert.AreEqual("dns.example", tls.TargetHost);
        Assert.AreEqual(("tcp", "dns.example", 443), server.Opened.Single());
        CollectionAssert.AreEqual(DohRequest.Encode("dns.example", 443, Query), server.TcpWritten.ToArray());
        Assert.AreEqual(1, server.Disposed);
    }

    [TestMethod]
    public async Task ExchangeAsync_HandshakeFails_FailsWithTheProvidersExitCodeAndClosesTheConnection()
    {
        var server = new ScriptedDnsServer((_, _) => [], []);
        var tls = new HandshakingTlsProvider { Failure = ConnectResult.Failed(CurlExitCode.SslConnectError, "TLS handshake failed") };

        DnsQueryException failure = await Assert.ThrowsExactlyAsync<DnsQueryException>(async () =>
            await new DnsHttpsTransport(server, tls).ExchangeAsync("dns.example", 443, Query, ContextOf()));

        Assert.AreEqual(CurlExitCode.SslConnectError, failure.ExitCode);
        Assert.AreEqual("TLS handshake failed", failure.Message);
        Assert.IsEmpty(server.TcpWritten);
        Assert.AreEqual(1, server.Disposed);
    }

    [TestMethod]
    public async Task ExchangeAsync_CannotConnect_FailsWithTheConnectorsExitCode()
    {
        var server = new ScriptedDnsServer((_, _) => [], []) { ConnectFailure = ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect to h port 443") };
        var tls = new HandshakingTlsProvider();

        DnsQueryException failure = await Assert.ThrowsExactlyAsync<DnsQueryException>(async () =>
            await new DnsHttpsTransport(server, tls).ExchangeAsync("h", 443, Query, ContextOf()));

        Assert.AreEqual(CurlExitCode.CouldntConnect, failure.ExitCode);
        Assert.IsNull(tls.TargetHost);
    }

    private static byte[] ResponseOf(byte[] body) =>
        [.. Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/dns-message\r\nContent-Length: {body.Length}\r\n\r\n"), .. body];

    private static TransferContext ContextOf() => new() { Url = CurlUrl.Parse("dns://h/example.com"), Output = new MemoryStream() };
}
