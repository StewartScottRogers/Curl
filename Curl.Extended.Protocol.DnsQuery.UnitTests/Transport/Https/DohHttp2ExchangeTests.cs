using System.Text;
using Curl.Extended.Protocol.DnsQuery.Client;
using Curl.Extended.Protocol.DnsQuery.Transport.Https;
using Curl.Extended.Protocol.DnsQuery.UnitTests.Transport.Tls;
using Curl.Extended.Protocol.DnsQuery.UnitTests.Wire;
using Curl.Extended.Protocol.DnsQuery.Wire;
using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.DnsQuery.UnitTests.Transport.Https;

/// <summary>
/// Checks DNS over HTTPS over HTTP/2 (RFC 8484 section 5.2, BL-4690): ALPN offers <c>h2</c> first, and once the server
/// selects it the query goes out after the client preface (RFC 9113 section 3.4) as a POST on one stream, and the answer
/// is the final 200 response's DATA.
/// </summary>
[TestClass]
public sealed class DohHttp2ExchangeTests
{
    private const string Preface = "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n";

    private static readonly byte[] Query = new DnsWire().Header(0, 0x0100, 1, 0, 0, 0).Question(DnsWire.Name("example.com"), 1, 1).ToArray();
    private static readonly byte[] Answer = DnsWire.SingleAnswer(0, [192, 0, 2, 1]);

    [TestMethod]
    public async Task ExchangeAsync_Rfc8484Section5_2_ServerSelectsH2_SendsThePrefaceAndAPostAndReadsTheAnswer()
    {
        HpackEncoder encoder = new();
        var server = new ScriptedDnsServer((_, _) => [],
        [
            .. Frame(Http2FrameFactory.CreateSettings([])),
            .. Frame(Http2FrameFactory.CreateHeaders(1, encoder.Encode([new(":status", "103")]), isEndStream: false, isEndHeaders: true)),
            .. Frame(Http2FrameFactory.CreateHeaders(1, encoder.Encode([new(":status", "200"), new("content-type", "application/dns-message")]), isEndStream: false, isEndHeaders: true)),
            .. Frame(Http2FrameFactory.CreateData(1, Answer.AsMemory(0, 5), isEndStream: false)),
            .. Frame(Http2FrameFactory.CreateData(1, Answer.AsMemory(5), isEndStream: false)),
            .. Frame(Http2FrameFactory.CreateHeaders(1, encoder.Encode([new("x-trailer", "1")]), isEndStream: true, isEndHeaders: true)),
        ]);
        var tls = new HandshakingTlsProvider { ApplicationProtocol = "h2" };

        byte[] answer = await new DnsHttpsTransport(server, tls).ExchangeAsync("dns.example", 8443, Query, ContextOf());

        CollectionAssert.AreEqual(Answer, answer);
        CollectionAssert.AreEqual(new[] { "h2", "http/1.1" }, tls.OfferedApplicationProtocols!.ToArray());
        byte[] written = [.. server.TcpWritten];
        StringAssert.StartsWith(Encoding.ASCII.GetString(written), Preface);
        CollectionAssert.AreEqual(
            new[] { ":method: POST", ":scheme: https", ":authority: dns.example:8443", ":path: /dns-query", "accept: application/dns-message", "content-type: application/dns-message", $"content-length: {DnsEdnsPadding.Pad(Query).Length}" },
            RequestHeadersOf(written).Select(static field => $"{field.Name}: {field.Value}").ToArray());
        Assert.AreEqual(1, server.Disposed);
    }

    [TestMethod]
    public async Task ExchangeAsync_StreamEndsAfterAnInformationalResponse_FailsWithExit56() =>
        Assert.AreEqual(CurlExitCode.RecvError, await FailureOfAsync([new(":status", "100")], isEndStream: true));

    [TestMethod]
    public async Task ExchangeAsync_ConnectionClosesBeforeTheStreamEnds_FailsWithExit56() =>
        Assert.AreEqual(CurlExitCode.RecvError, await FailureOfAsync([new(":status", "200"), new("content-type", "application/dns-message")], isEndStream: false));

    [TestMethod]
    public async Task ExchangeAsync_ResponseHasNoStatus_FailsWithExit8() =>
        Assert.AreEqual(CurlExitCode.WeirdServerReply, await FailureOfAsync([new("content-type", "application/dns-message")], isEndStream: true));

    [TestMethod]
    public async Task ExchangeAsync_ServerSelectsNoProtocol_FallsBackToHttp11()
    {
        var server = new ScriptedDnsServer((_, _) => [],
            [.. Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/dns-message\r\nContent-Length: {Answer.Length}\r\n\r\n"), .. Answer]);

        byte[] answer = await new DnsHttpsTransport(server, new HandshakingTlsProvider()).ExchangeAsync("h", 443, Query, ContextOf());

        CollectionAssert.AreEqual(Answer, answer);
        CollectionAssert.AreEqual(DohRequest.Encode("h", 443, DnsEdnsPadding.Pad(Query)), server.TcpWritten.ToArray());
    }

    private static async Task<CurlExitCode> FailureOfAsync(HeaderField[] responseHeaders, bool isEndStream)
    {
        var server = new ScriptedDnsServer((_, _) => [],
        [
            .. Frame(Http2FrameFactory.CreateSettings([])),
            .. Frame(Http2FrameFactory.CreateHeaders(1, new HpackEncoder().Encode(responseHeaders), isEndStream, isEndHeaders: true)),
        ]);

        DnsQueryException failure = await Assert.ThrowsExactlyAsync<DnsQueryException>(async () =>
            await new DnsHttpsTransport(server, new HandshakingTlsProvider { ApplicationProtocol = "h2" }).ExchangeAsync("h", 443, Query, ContextOf()));
        return failure.ExitCode;
    }

    private static byte[] Frame(Http2Frame frame) => Http2FrameCodec.Serialize(frame);

    private static IReadOnlyList<HeaderField> RequestHeadersOf(byte[] written)
    {
        int offset = Preface.Length;
        while ((Http2FrameType)written[offset + 3] != Http2FrameType.Headers)
        {
            offset += 9 + LengthAt(written, offset);
        }

        return new HpackDecoder().Decode(written.AsSpan(offset + 9, LengthAt(written, offset)));
    }

    private static int LengthAt(byte[] written, int offset) => (written[offset] << 16) | (written[offset + 1] << 8) | written[offset + 2];

    private static TransferContext ContextOf() => new() { Url = CurlUrl.Parse("dns://h/example.com"), Output = new MemoryStream() };
}
