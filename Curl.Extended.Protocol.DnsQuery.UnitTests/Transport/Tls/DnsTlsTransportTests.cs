using Curl.Extended.Protocol.DnsQuery.Client;
using Curl.Extended.Protocol.DnsQuery.Transport.Tls;
using Curl.Extended.Protocol.DnsQuery.UnitTests.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.DnsQuery.UnitTests.Transport.Tls;

/// <summary>
/// Checks DNS over TLS (RFC 7858) over a fake connection: port 853 (section 3.1), the TLS handshake before any DNS
/// message (section 3.2), and the two-octet length prefix of DNS over TCP (section 3.3).
/// </summary>
[TestClass]
public sealed class DnsTlsTransportTests
{
    private static readonly byte[] Query = new DnsWire().Header(0x1234, 0x0100, 1, 0, 0, 0).Question(DnsWire.Name("example.com"), 1, 1).ToArray();
    private static readonly byte[] Answer = DnsWire.SingleAnswer(1, [192, 0, 2, 1]);

    [TestMethod]
    public void DefaultPort_Rfc7858Section3_1_Is853() =>
        Assert.AreEqual(853, new DnsTlsTransport(new ScriptedDnsServer((_, _) => [], []), new HandshakingTlsProvider()).DefaultPort);

    [TestMethod]
    public async Task ExchangeAsync_Rfc7858Section3_2_HandshakesBeforeSendingAndVerifiesTheServersHost()
    {
        var server = new ScriptedDnsServer((_, _) => [], [.. DnsWire.UInt16(Answer.Length), .. Answer]);
        int writtenAtHandshake = -1;
        var tls = new HandshakingTlsProvider { OnHandshake = () => writtenAtHandshake = server.TcpWritten.Count };

        await new DnsTlsTransport(server, tls).ExchangeAsync("dns.example", 853, Query, ContextOf());

        Assert.AreEqual(0, writtenAtHandshake);
        Assert.AreEqual("dns.example", tls.TargetHost);
        Assert.AreEqual(("tcp", "dns.example", 853), server.Opened.Single());
    }

    [TestMethod]
    public async Task ExchangeAsync_Rfc7858Section3_3_PrefixesTheQueryWithItsLengthAndReadsTheAnswerByItsLength()
    {
        var server = new ScriptedDnsServer((_, _) => [], [.. DnsWire.UInt16(Answer.Length), .. Answer, 0xEE]) { TcpReadSize = 1 };

        byte[] answer = await new DnsTlsTransport(server, new HandshakingTlsProvider()).ExchangeAsync("dns.example", 853, Query, ContextOf());

        CollectionAssert.AreEqual(Answer, answer);
        CollectionAssert.AreEqual(new byte[] { 0, (byte)Query.Length }.Concat(Query).ToArray(), server.TcpWritten.ToArray());
        Assert.AreEqual(1, server.Disposed);
    }

    [TestMethod]
    public async Task ExchangeAsync_HandshakeFails_FailsWithTheProvidersExitCodeAndClosesTheConnection()
    {
        var server = new ScriptedDnsServer((_, _) => [], []);
        var tls = new HandshakingTlsProvider { Failure = ConnectResult.Failed(CurlExitCode.SslConnectError, "TLS handshake failed") };

        DnsQueryException failure = await Assert.ThrowsExactlyAsync<DnsQueryException>(async () =>
            await new DnsTlsTransport(server, tls).ExchangeAsync("dns.example", 853, Query, ContextOf()));

        Assert.AreEqual(CurlExitCode.SslConnectError, failure.ExitCode);
        Assert.AreEqual("TLS handshake failed", failure.Message);
        Assert.IsEmpty(server.TcpWritten);
        Assert.AreEqual(1, server.Disposed);
    }

    [TestMethod]
    public async Task ExchangeAsync_CannotConnect_FailsWithTheConnectorsExitCode()
    {
        var server = new ScriptedDnsServer((_, _) => [], []) { ConnectFailure = ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect to h port 853") };
        var tls = new HandshakingTlsProvider();

        DnsQueryException failure = await Assert.ThrowsExactlyAsync<DnsQueryException>(async () =>
            await new DnsTlsTransport(server, tls).ExchangeAsync("h", 853, Query, ContextOf()));

        Assert.AreEqual(CurlExitCode.CouldntConnect, failure.ExitCode);
        Assert.IsNull(tls.TargetHost);
    }

    private static TransferContext ContextOf() => new() { Url = CurlUrl.Parse("dns://h/example.com"), Output = new MemoryStream() };
}
