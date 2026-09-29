using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins the lines <see cref="DohDnsResolver" /> reports for <c>-v --trace-config doh</c> to what
/// curl 8.21.0 (Schannel) printed on 2026-09-29 (BL-850), measured with
/// <c>Record-CurlExchange.ps1 -Tls -Connections 2</c> as the DoH server, every connection given the
/// same response, and <c>-sS -v --trace-config doh --doh-url https://127.0.0.1:P/dns-query
/// --doh-insecure http://example.test:48799/</c>. Each expected line is curl's text after <c>* </c>.
/// </summary>
[TestClass]
public sealed class DohDnsResolverTraceTests
{
    private static readonly Uri MeasuredDohUrl = new("https://127.0.0.1:48711/dns-query");

    // The 46-byte answer the recorder sent: example.test A 127.0.0.1, TTL 60.
    private const string MeasuredAAnswer =
        "000081800001000100000000076578616D706C650474657374000001000" + "1C00C000100010000003C00047F000001";

    // The 78-byte answer to AAAA: example.test CNAME a.test (TTL 30), then a.test AAAA ::1 (TTL 90).
    private const string MeasuredCnameAndAaaaAnswer =
        "000081800001000200000000076578616D706C650474657374000" + "01C0001"
        + "C00C000500010000001E000801610474657374" + "00"
        + "C02A001C00010000005A001000000000000000000000000000000001";

    [TestMethod]
    public void Constructor_WithNullTrace_Throws()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => new DohDnsResolver(new FakeConnector(), MeasuredDohUrl, null!, DescribeExitCode));

        Assert.AreEqual("dohTrace", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_WithNullExitCodeText_Throws()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => new DohDnsResolver(new FakeConnector(), MeasuredDohUrl, new RecordingTransferEvents(), null!));

        Assert.AreEqual("describeExitCode", exception.ParamName);
    }

    [TestMethod]
    public async Task ResolveAsync_A500WithAnEmptyBody_ReportsTooSmallForBothTypes()
    {
        var lines = await TraceAsync(Response("HTTP/1.1 500 Internal Server Error\r\nContent-Length: 0\r\n\r\n"));

        CollectionAssert.AreEqual(
            new[]
            {
                "[DNS] DoH: Too small type A for example.test",
                "[DNS] DoH: Too small type AAAA for example.test",
            },
            lines);
    }

    [TestMethod]
    public async Task ResolveAsync_AnNxdomainAnswer_ReportsBadRcodeForBothTypes()
    {
        var nxdomain = Convert.FromHexString(MeasuredAAnswer);
        nxdomain[3] = 0x83;

        var lines = await TraceAsync(Ok(nxdomain));

        CollectionAssert.AreEqual(
            new[]
            {
                "[DNS] DoH: Bad RCODE type A for example.test",
                "[DNS] DoH: Bad RCODE type AAAA for example.test",
            },
            lines);
    }

    [TestMethod]
    public async Task ResolveAsync_ATruncatedAnswer_ReportsOutOfRangeForAAndUnexpectedTypeForAaaa()
    {
        // Content-Length 45 on the 46-byte A answer: the A query's record data is cut short, and the
        // AAAA query meets the A record's type before its data.
        var answer = Convert.FromHexString(MeasuredAAnswer);
        var lines = await TraceAsync([.. Response($"HTTP/1.1 200 OK\r\nContent-Length: {answer.Length - 1}\r\n\r\n"), .. answer]);

        CollectionAssert.AreEqual(
            new[]
            {
                "[DNS] DoH: Out of range type A for example.test",
                "[DNS] DoH: Unexpected TYPE type AAAA for example.test",
            },
            lines);
    }

    [TestMethod]
    public async Task ResolveAsync_AnAnswerWithNoRecord_ReportsNoContentForBothTypes()
    {
        var noRecord = Convert.FromHexString(MeasuredAAnswer)[..30];
        noRecord[7] = 0;

        var lines = await TraceAsync(Ok(noRecord));

        CollectionAssert.AreEqual(
            new[]
            {
                "[DNS] DoH: No content type A for example.test",
                "[DNS] DoH: No content type AAAA for example.test",
            },
            lines);
    }

    [TestMethod]
    public async Task ResolveAsync_ACloseDelimitedBody_ReportsTheReceiveFailuresAndAnEmptyEntry()
    {
        // curl skips the decode of a failed exchange, so it counts as answered and the entry,
        // with no address and the TTL it starts from, is printed.
        var lines = await TraceAsync([.. Response("HTTP/1.1 200 OK\r\n\r\n"), .. Convert.FromHexString(MeasuredAAnswer)]);

        CollectionAssert.AreEqual(
            new[]
            {
                "[DNS] DoH request Failure when receiving data from the peer",
                "[DNS] DoH request Failure when receiving data from the peer",
                "[DNS] hostname: example.test",
                "[DoH] TTL: 2147483647 seconds",
            },
            lines);
    }

    [TestMethod]
    public async Task ResolveAsync_ARefusedCertificate_ReportsTheConnectFailuresAndAnEmptyEntry()
    {
        // Without --doh-insecure: both handshakes failed with exit 60.
        var connector = new FakeConnector
        {
            Failure = ConnectResult.Failed(CurlExitCode.PeerFailedVerification, "schannel: SEC_E_UNTRUSTED_ROOT (0x80090325)"),
        };

        var lines = await TraceAsync(connector);

        CollectionAssert.AreEqual(
            new[]
            {
                "[DNS] DoH request SSL peer certificate or SSH remote key was not OK",
                "[DNS] DoH request SSL peer certificate or SSH remote key was not OK",
                "[DNS] hostname: example.test",
                "[DoH] TTL: 2147483647 seconds",
            },
            lines);
    }

    [TestMethod]
    public async Task ResolveAsync_ADecodedAAnswer_ReportsTheEntryWithItsTtlAndAddress()
    {
        // The AAAA query was given the A answer too, so it fails with Unexpected TYPE.
        var lines = await TraceAsync(Ok(Convert.FromHexString(MeasuredAAnswer)));

        CollectionAssert.AreEqual(
            new[]
            {
                "[DNS] DoH: Unexpected TYPE type AAAA for example.test",
                "[DNS] hostname: example.test",
                "[DoH] TTL: 60 seconds",
                "[DoH] A: 127.0.0.1",
            },
            lines);
    }

    [TestMethod]
    public async Task ResolveAsync_ACnameAndAaaaAnswer_ReportsTheSmallestTtlTheUncompressedAddressAndTheName()
    {
        // curl printed "CNAME: a.test" twice: its one entry for both queries keeps the CNAME the
        // failed A decode read before the AAAA record stopped it. The failed decode here drops its
        // names, so the name is printed once (BL-958 matches curl).
        var lines = await TraceAsync(Ok(Convert.FromHexString(MeasuredCnameAndAaaaAnswer)));

        CollectionAssert.AreEqual(
            new[]
            {
                "[DNS] DoH: Unexpected TYPE type A for example.test",
                "[DNS] hostname: example.test",
                "[DoH] TTL: 30 seconds",
                "[DoH] AAAA: 0000:0000:0000:0000:0000:0000:0000:0001",
                "CNAME: a.test",
            },
            lines);
    }

    [TestMethod]
    public async Task ResolveAsync_OneExchangeFailingAndOneDecoding_ReportsBothWithTheDecodedAddress()
    {
        var connector = new FakeConnector();
        connector.BytesToRead.Add(Ok(Convert.FromHexString(MeasuredAAnswer)));
        connector.BytesToRead.Add(Response("HTTP/1.1 200 OK\r\n\r\n"));

        var lines = await TraceAsync(connector);

        CollectionAssert.AreEqual(
            new[]
            {
                "[DNS] DoH request Failure when receiving data from the peer",
                "[DNS] hostname: example.test",
                "[DoH] TTL: 60 seconds",
                "[DoH] A: 127.0.0.1",
            },
            lines);
    }

    [TestMethod]
    public async Task ResolveAsync_WithoutATraceSink_StillResolves()
    {
        var connector = new FakeConnector();
        connector.BytesToRead.Add(Ok(Convert.FromHexString(MeasuredAAnswer)));

        var addresses = await new DohDnsResolver(connector, MeasuredDohUrl).ResolveAsync("example.test", CancellationToken.None);

        Assert.AreEqual("127.0.0.1", addresses.Single().ToString());
    }

    [TestMethod]
    public void FormatAddress_WritesEveryIPv6GroupAsFourHexDigits()
    {
        Assert.AreEqual("2001:0db8:0000:0000:0000:0000:00ab:ff01", DohTraceLines.FormatAddress(System.Net.IPAddress.Parse("2001:db8::ab:ff01")));
    }

    private static async Task<string[]> TraceAsync(byte[] response)
    {
        var connector = new FakeConnector();
        connector.BytesToRead.Add(response);
        connector.BytesToRead.Add(response);
        return await TraceAsync(connector);
    }

    private static async Task<string[]> TraceAsync(FakeConnector connector)
    {
        var trace = new RecordingTransferEvents();
        await new DohDnsResolver(connector, MeasuredDohUrl, trace, DescribeExitCode).ResolveAsync("example.test", CancellationToken.None);
        return [.. trace.Info];
    }

    // The two curl_easy_strerror texts the measured runs printed.
    private static string DescribeExitCode(CurlExitCode exitCode) => exitCode switch
    {
        CurlExitCode.RecvError => "Failure when receiving data from the peer",
        CurlExitCode.PeerFailedVerification => "SSL peer certificate or SSH remote key was not OK",
        _ => exitCode.ToString(),
    };

    private static byte[] Ok(byte[] answer) =>
        [.. Response($"HTTP/1.1 200 OK\r\nContent-Length: {answer.Length}\r\n\r\n"), .. answer];

    private static byte[] Response(string text) => Encoding.Latin1.GetBytes(text);
}
