using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    // BL-958's answers, one per query (Record-CurlExchange.ps1 -Response gives connection N the Nth).
    private const string QuestionHeader = "000081800001";
    private const string ExampleTest = "076578616D706C650474657374";
    private const string ARecord = "C00C00010001000000" + "3C00047F000002";
    private const string AaaaRecord = "C00C001C00010000005A0010" + "00000000000000000000000000000001";
    private const string CnameRecord = "C00C000500010000001E000801610474657374" + "00";
    private const string AThenAaaaAnswerToA = QuestionHeader + "000200000000" + ExampleTest + "0000010001" + ARecord + AaaaRecord;
    private const string AaaaThenAAnswerToAaaa = QuestionHeader + "000200000000" + ExampleTest + "00001C0001" + AaaaRecord + ARecord;
    private const string AaaaAnswerToAaaa = QuestionHeader + "000100000000" + ExampleTest + "00001C0001" + AaaaRecord;
    private const string CnameAnswerToA = QuestionHeader + "000100000000" + ExampleTest + "0000010001" + CnameRecord;
    private const string CnameAnswerToAaaa = QuestionHeader + "000100000000" + ExampleTest + "00001C0001" + CnameRecord;
    private const string EmptyAnswerToA = QuestionHeader + "000000000000" + ExampleTest + "0000010001";
    private const string EmptyAnswerToAaaa = QuestionHeader + "000000000000" + ExampleTest + "00001C0001";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Constructor_WithNullTrace_Throws()
    {
        Diagnostics.Arrange("trace", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => new DohDnsResolver(new FakeConnector(), MeasuredDohUrl, null!, DescribeExitCode));

        Diagnostics.Act("exception parameter", exception.ParamName);
        Diagnostics.Assert("exception parameter", "dohTrace", exception.ParamName);
        Assert.AreEqual("dohTrace", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_WithNullExitCodeText_Throws()
    {
        Diagnostics.Arrange("exit code text", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => new DohDnsResolver(new FakeConnector(), MeasuredDohUrl, new RecordingTransferEvents(), null!));

        Diagnostics.Act("exception parameter", exception.ParamName);
        Diagnostics.Assert("exception parameter", "describeExitCode", exception.ParamName);
        Assert.AreEqual("describeExitCode", exception.ParamName);
    }

    [TestMethod]
    public async Task ResolveAsync_A500WithAnEmptyBody_ReportsTooSmallForBothTypes()
    {
        var lines = await TraceAsync(Response("HTTP/1.1 500 Internal Server Error\r\nContent-Length: 0\r\n\r\n"));

        var expectedLines = new[]
            {
                "[DNS] DoH: Too small type A for example.test",
                "[DNS] DoH: Too small type AAAA for example.test",
            };
        Diagnostics.Diff("trace lines", string.Join("\n", expectedLines), string.Join("\n", lines));
        CollectionAssert.AreEqual(expectedLines, lines);
    }

    [TestMethod]
    public async Task ResolveAsync_AnNxdomainAnswer_ReportsBadRcodeForBothTypes()
    {
        var nxdomain = Convert.FromHexString(MeasuredAAnswer);
        nxdomain[3] = 0x83;

        var lines = await TraceAsync(Ok(nxdomain));

        var expectedLines = new[]
            {
                "[DNS] DoH: Bad RCODE type A for example.test",
                "[DNS] DoH: Bad RCODE type AAAA for example.test",
            };
        Diagnostics.Diff("trace lines", string.Join("\n", expectedLines), string.Join("\n", lines));
        CollectionAssert.AreEqual(expectedLines, lines);
    }

    [TestMethod]
    public async Task ResolveAsync_ATruncatedAnswer_ReportsOutOfRangeForAAndUnexpectedTypeForAaaa()
    {
        // Content-Length 45 on the 46-byte A answer: the A query's record data is cut short, and the
        // AAAA query meets the A record's type before its data.
        var answer = Convert.FromHexString(MeasuredAAnswer);
        var lines = await TraceAsync([.. Response($"HTTP/1.1 200 OK\r\nContent-Length: {answer.Length - 1}\r\n\r\n"), .. answer]);

        var expectedLines = new[]
            {
                "[DNS] DoH: Out of range type A for example.test",
                "[DNS] DoH: Unexpected TYPE type AAAA for example.test",
            };
        Diagnostics.Diff("trace lines", string.Join("\n", expectedLines), string.Join("\n", lines));
        CollectionAssert.AreEqual(expectedLines, lines);
    }

    [TestMethod]
    public async Task ResolveAsync_AnAnswerWithNoRecord_ReportsNoContentForBothTypes()
    {
        var noRecord = Convert.FromHexString(MeasuredAAnswer)[..30];
        noRecord[7] = 0;

        var lines = await TraceAsync(Ok(noRecord));

        var expectedLines = new[]
            {
                "[DNS] DoH: No content type A for example.test",
                "[DNS] DoH: No content type AAAA for example.test",
            };
        Diagnostics.Diff("trace lines", string.Join("\n", expectedLines), string.Join("\n", lines));
        CollectionAssert.AreEqual(expectedLines, lines);
    }

    [TestMethod]
    public async Task ResolveAsync_ACloseDelimitedBody_ReportsTheReceiveFailuresAndAnEmptyEntry()
    {
        // curl skips the decode of a failed exchange, so it counts as answered and the entry,
        // with no address and the TTL it starts from, is printed.
        var lines = await TraceAsync([.. Response("HTTP/1.1 200 OK\r\n\r\n"), .. Convert.FromHexString(MeasuredAAnswer)]);

        var expectedLines = new[]
            {
                "[DNS] DoH request Failure when receiving data from the peer",
                "[DNS] DoH request Failure when receiving data from the peer",
                "[DNS] hostname: example.test",
                "[DoH] TTL: 2147483647 seconds",
            };
        Diagnostics.Diff("trace lines", string.Join("\n", expectedLines), string.Join("\n", lines));
        CollectionAssert.AreEqual(expectedLines, lines);
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

        var expectedLines = new[]
            {
                "[DNS] DoH request SSL peer certificate or SSH remote key was not OK",
                "[DNS] DoH request SSL peer certificate or SSH remote key was not OK",
                "[DNS] hostname: example.test",
                "[DoH] TTL: 2147483647 seconds",
            };
        Diagnostics.Diff("trace lines", string.Join("\n", expectedLines), string.Join("\n", lines));
        CollectionAssert.AreEqual(expectedLines, lines);
    }

    [TestMethod]
    public async Task ResolveAsync_ADecodedAAnswer_ReportsTheEntryWithItsTtlAndAddress()
    {
        // The AAAA query was given the A answer too, so it fails with Unexpected TYPE.
        var lines = await TraceAsync(Ok(Convert.FromHexString(MeasuredAAnswer)));

        var expectedLines = new[]
            {
                "[DNS] DoH: Unexpected TYPE type AAAA for example.test",
                "[DNS] hostname: example.test",
                "[DoH] TTL: 60 seconds",
                "[DoH] A: 127.0.0.1",
            };
        Diagnostics.Diff("trace lines", string.Join("\n", expectedLines), string.Join("\n", lines));
        CollectionAssert.AreEqual(expectedLines, lines);
    }

    [TestMethod]
    public async Task ResolveAsync_ACnameAndAaaaAnswer_ReportsTheFailedADecodesCnameAsWellAsTheAaaaDecodes()
    {
        // curl's one entry for both queries keeps the CNAME the failed A decode read before the
        // AAAA record stopped it, so "CNAME: a.test" is printed twice (BL-958).
        var lines = await TraceAsync(Ok(Convert.FromHexString(MeasuredCnameAndAaaaAnswer)));

        var expectedLines = new[]
            {
                "[DNS] DoH: Unexpected TYPE type A for example.test",
                "[DNS] hostname: example.test",
                "[DoH] TTL: 30 seconds",
                "[DoH] AAAA: 0000:0000:0000:0000:0000:0000:0000:0001",
                "CNAME: a.test",
                "CNAME: a.test",
            };
        Diagnostics.Diff("trace lines", string.Join("\n", expectedLines), string.Join("\n", lines));
        CollectionAssert.AreEqual(expectedLines, lines);
    }

    [TestMethod]
    public async Task ResolveAsync_AFailedADecodeAfterAnAddress_ReportsAndResolvesThatAddress()
    {
        // Measured 2026-10-02 (BL-958), the A query answered A 127.0.0.2 then AAAA ::1, the AAAA
        // query AAAA ::1: curl traced "[DoH] A: 127.0.0.2" and then printed "IPv6: ::1",
        // "IPv4: 127.0.0.2" and tried both, so the failed decode's address reaches the resolved list.
        var connector = new FakeConnector();
        connector.BytesToRead.Add(Ok(Convert.FromHexString(AThenAaaaAnswerToA)));
        connector.BytesToRead.Add(Ok(Convert.FromHexString(AaaaAnswerToAaaa)));
        var trace = new RecordingTransferEvents();

        ArrangeAnswers(connector);

        var addresses = await new DohDnsResolver(connector, MeasuredDohUrl, trace, DescribeExitCode).ResolveAsync("example.test", CancellationToken.None);

        WriteTrace(trace.Info);
        Diagnostics.Act("addresses", string.Join(", ", addresses));

        var expectedLines = new[]
            {
                "[DNS] DoH: Unexpected TYPE type A for example.test",
                "[DNS] hostname: example.test",
                "[DoH] TTL: 60 seconds",
                "[DoH] A: 127.0.0.2",
                "[DoH] AAAA: 0000:0000:0000:0000:0000:0000:0000:0001",
            };
        Diagnostics.Diff("trace lines", string.Join("\n", expectedLines), string.Join("\n", trace.Info.ToArray()));
        CollectionAssert.AreEqual(expectedLines, trace.Info.ToArray());
        CollectionAssert.AreEqual(new[] { "::1", "127.0.0.2" }, addresses.Select(address => address.ToString()).ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_BothDecodesFailingAfterAnAddress_ResolvesNothing()
    {
        // Measured 2026-10-02 (BL-958): the A query answered A then AAAA, the AAAA query AAAA then
        // A; curl printed both failures, no entry, and exited 6.
        var connector = new FakeConnector();
        connector.BytesToRead.Add(Ok(Convert.FromHexString(AThenAaaaAnswerToA)));
        connector.BytesToRead.Add(Ok(Convert.FromHexString(AaaaThenAAnswerToAaaa)));
        var trace = new RecordingTransferEvents();

        ArrangeAnswers(connector);

        var addresses = await new DohDnsResolver(connector, MeasuredDohUrl, trace, DescribeExitCode).ResolveAsync("example.test", CancellationToken.None);

        WriteTrace(trace.Info);
        Diagnostics.Act("addresses", string.Join(", ", addresses));

        var expectedLines = new[]
            {
                "[DNS] DoH: Unexpected TYPE type A for example.test",
                "[DNS] DoH: Unexpected TYPE type AAAA for example.test",
            };
        Diagnostics.Diff("trace lines", string.Join("\n", expectedLines), string.Join("\n", trace.Info.ToArray()));
        CollectionAssert.AreEqual(expectedLines, trace.Info.ToArray());
        Assert.IsEmpty(addresses);
    }

    [TestMethod]
    public async Task ResolveAsync_AnEmptyAaaaAnswerAfterACnameAnswer_DecodesIntoTheSharedEntry()
    {
        // Measured 2026-10-02 (BL-958): curl decodes A first into its one entry, so the empty AAAA
        // answer finds the A answer's CNAME there and is not "No content".
        var connector = new FakeConnector();
        connector.BytesToRead.Add(Ok(Convert.FromHexString(CnameAnswerToA)));
        connector.BytesToRead.Add(Ok(Convert.FromHexString(EmptyAnswerToAaaa)));

        var lines = await TraceAsync(connector);

        var expectedLines = new[] { "[DNS] hostname: example.test", "[DoH] TTL: 30 seconds", "CNAME: a.test" };
        Diagnostics.Diff("trace lines", string.Join("\n", expectedLines), string.Join("\n", lines));
        CollectionAssert.AreEqual(expectedLines, lines);
    }

    [TestMethod]
    public async Task ResolveAsync_AnEmptyAAnswerBeforeACnameAnswer_IsNoContent()
    {
        // Measured 2026-10-02 (BL-958): the A answer is decoded before the AAAA one fills the
        // entry, so it is still "No content".
        var connector = new FakeConnector();
        connector.BytesToRead.Add(Ok(Convert.FromHexString(EmptyAnswerToA)));
        connector.BytesToRead.Add(Ok(Convert.FromHexString(CnameAnswerToAaaa)));

        var lines = await TraceAsync(connector);

        var expectedLines = new[]
            {
                "[DNS] DoH: No content type A for example.test",
                "[DNS] hostname: example.test",
                "[DoH] TTL: 30 seconds",
                "CNAME: a.test",
            };
        Diagnostics.Diff("trace lines", string.Join("\n", expectedLines), string.Join("\n", lines));
        CollectionAssert.AreEqual(expectedLines, lines);
    }

    [TestMethod]
    public async Task ResolveAsync_OneExchangeFailingAndOneDecoding_ReportsBothWithTheDecodedAddress()
    {
        var connector = new FakeConnector();
        connector.BytesToRead.Add(Ok(Convert.FromHexString(MeasuredAAnswer)));
        connector.BytesToRead.Add(Response("HTTP/1.1 200 OK\r\n\r\n"));

        var lines = await TraceAsync(connector);

        var expectedLines = new[]
            {
                "[DNS] DoH request Failure when receiving data from the peer",
                "[DNS] hostname: example.test",
                "[DoH] TTL: 60 seconds",
                "[DoH] A: 127.0.0.1",
            };
        Diagnostics.Diff("trace lines", string.Join("\n", expectedLines), string.Join("\n", lines));
        CollectionAssert.AreEqual(expectedLines, lines);
    }

    [TestMethod]
    public async Task ResolveAsync_WithoutATraceSink_StillResolves()
    {
        var connector = new FakeConnector();
        connector.BytesToRead.Add(Ok(Convert.FromHexString(MeasuredAAnswer)));

        ArrangeAnswers(connector);

        var addresses = await new DohDnsResolver(connector, MeasuredDohUrl).ResolveAsync("example.test", CancellationToken.None);

        Diagnostics.Act("addresses", string.Join(", ", addresses));
        Diagnostics.Assert("addresses", "127.0.0.1", string.Join(", ", addresses));
        Assert.AreEqual("127.0.0.1", addresses.Single().ToString());
    }

    [TestMethod]
    public async Task ResolveAsync_AddressesAndCnamesOverTheSharedLimit_KeepsTheFirst24AddressesAnd4Cnames()
    {
        // Measured 2026-10-02 (BL-1153), the A query answered A 127.0.0.1-20 then CNAMEs c1-c3.test,
        // the AAAA query AAAA ::1-::a then CNAMEs c4-c6.test (TTL 60 each): curl traced the 20 A lines,
        // AAAA ::1-::4 and CNAMEs c1-c4.test, then printed "IPv6: ::1, ::2, ::3, ::4" and
        // "IPv4: 127.0.0.1, ..., 127.0.0.20".
        var connector = new FakeConnector();
        connector.BytesToRead.Add(Ok(LimitAnswer(DnsRecordType.A, 20, [1, 2, 3])));
        connector.BytesToRead.Add(Ok(LimitAnswer(DnsRecordType.Aaaa, 10, [4, 5, 6])));
        var trace = new RecordingTransferEvents();

        ArrangeAnswers(connector);

        var addresses = await new DohDnsResolver(connector, MeasuredDohUrl, trace, DescribeExitCode).ResolveAsync("example.test", CancellationToken.None);

        WriteTrace(trace.Info);
        Diagnostics.Act("addresses", string.Join(", ", addresses));

        var expectedLines = new[] { "[DNS] hostname: example.test", "[DoH] TTL: 60 seconds" }
                .Concat(Enumerable.Range(1, 20).Select(n => $"[DoH] A: 127.0.0.{n}"))
                .Concat(Enumerable.Range(1, 4).Select(n => $"[DoH] AAAA: 0000:0000:0000:0000:0000:0000:0000:000{n}"))
                .Concat(Enumerable.Range(1, 4).Select(n => $"CNAME: c{n}.test"))
                .ToArray();
        Diagnostics.Diff("trace lines", string.Join("\n", expectedLines), string.Join("\n", trace.Info.ToArray()));
        CollectionAssert.AreEqual(expectedLines, trace.Info.ToArray());
        CollectionAssert.AreEqual(
            Enumerable.Range(1, 4).Select(n => $"::{n}").Concat(Enumerable.Range(1, 20).Select(n => $"127.0.0.{n}")).ToArray(),
            addresses.Select(address => address.ToString()).ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_AFillingTheSharedLimit_KeepsNothingFromTheAaaaAnswer()
    {
        // Measured 2026-10-02 (BL-1153), the A query answered A 127.0.0.1-24 then CNAMEs c1-c4.test,
        // the AAAA query AAAA ::1-::5 then CNAMEs c5-c6.test: curl traced only the A answer's 24
        // addresses and 4 CNAMEs, then printed "IPv6: (none)" and the 24 IPv4 addresses.
        var connector = new FakeConnector();
        connector.BytesToRead.Add(Ok(LimitAnswer(DnsRecordType.A, 24, [1, 2, 3, 4])));
        connector.BytesToRead.Add(Ok(LimitAnswer(DnsRecordType.Aaaa, 5, [5, 6])));
        var trace = new RecordingTransferEvents();

        ArrangeAnswers(connector);

        var addresses = await new DohDnsResolver(connector, MeasuredDohUrl, trace, DescribeExitCode).ResolveAsync("example.test", CancellationToken.None);

        WriteTrace(trace.Info);
        Diagnostics.Act("addresses", string.Join(", ", addresses));

        var expectedLines = new[] { "[DNS] hostname: example.test", "[DoH] TTL: 60 seconds" }
                .Concat(Enumerable.Range(1, 24).Select(n => $"[DoH] A: 127.0.0.{n}"))
                .Concat(Enumerable.Range(1, 4).Select(n => $"CNAME: c{n}.test"))
                .ToArray();
        Diagnostics.Diff("trace lines", string.Join("\n", expectedLines), string.Join("\n", trace.Info.ToArray()));
        CollectionAssert.AreEqual(expectedLines, trace.Info.ToArray());
        CollectionAssert.AreEqual(
            Enumerable.Range(1, 24).Select(n => $"127.0.0.{n}").ToArray(),
            addresses.Select(address => address.ToString()).ToArray());
    }

    [TestMethod]
    public void FormatAddress_WritesEveryIPv6GroupAsFourHexDigits()
    {
        Diagnostics.Arrange("address", "2001:db8::ab:ff01");

        var formatted = DohTraceLines.FormatAddress(System.Net.IPAddress.Parse("2001:db8::ab:ff01"));

        Diagnostics.Act("formatted", formatted);
        Diagnostics.Assert("formatted", "2001:0db8:0000:0000:0000:0000:00ab:ff01", formatted);
        Assert.AreEqual("2001:0db8:0000:0000:0000:0000:00ab:ff01", formatted);
    }

    private async Task<string[]> TraceAsync(byte[] response)
    {
        var connector = new FakeConnector();
        connector.BytesToRead.Add(response);
        connector.BytesToRead.Add(response);
        return await TraceAsync(connector);
    }

    private async Task<string[]> TraceAsync(FakeConnector connector)
    {
        var trace = new RecordingTransferEvents();
        ArrangeAnswers(connector);
        await new DohDnsResolver(connector, MeasuredDohUrl, trace, DescribeExitCode).ResolveAsync("example.test", CancellationToken.None);
        WriteTrace(trace.Info);
        return [.. trace.Info];
    }

    private void ArrangeAnswers(FakeConnector connector)
    {
        Diagnostics.Arrange("DoH URL", MeasuredDohUrl);
        Diagnostics.Arrange("answers queued", connector.BytesToRead.Count);
        Diagnostics.Arrange("connect failure", connector.Failure?.ErrorMessage ?? "(none)");
        for (var index = 0; index < connector.BytesToRead.Count; index++)
        {
            Diagnostics.Bytes($"answer {index + 1}", connector.BytesToRead[index]);
        }
    }

    private void WriteTrace(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            Diagnostics.Act("trace line", line);
        }
    }

    // The two curl_easy_strerror texts the measured runs printed.
    private static string DescribeExitCode(CurlExitCode exitCode) => exitCode switch
    {
        CurlExitCode.RecvError => "Failure when receiving data from the peer",
        CurlExitCode.PeerFailedVerification => "SSL peer certificate or SSH remote key was not OK",
        _ => exitCode.ToString(),
    };

    // BL-1153's measured answers: example.test with addresses 1..addressCount of the type asked
    // (127.0.0.n or ::n), then a CNAME c<n>.test for each n in cnames, every record TTL 60.
    private static byte[] LimitAnswer(DnsRecordType recordType, int addressCount, int[] cnames)
    {
        var addressRecords = Enumerable.Range(1, addressCount).Select(n => recordType == DnsRecordType.A
            ? "C00C000100010000003C00047F0000" + n.ToString("X2", System.Globalization.CultureInfo.InvariantCulture)
            : "C00C001C00010000003C0010" + new string('0', 28) + n.ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
        var cnameRecords = cnames.Select(n => $"C00C000500010000003C000902633{n}047465737400");
        var records = addressRecords.Concat(cnameRecords).ToArray();
        var header = "000081800001" + records.Length.ToString("X4", System.Globalization.CultureInfo.InvariantCulture) + "00000000";
        var question = ExampleTest + "00" + ((int)recordType).ToString("X4", System.Globalization.CultureInfo.InvariantCulture) + "0001";
        return Convert.FromHexString(header + question + string.Concat(records));
    }

    private static byte[] Ok(byte[] answer) =>
        [.. Response($"HTTP/1.1 200 OK\r\nContent-Length: {answer.Length}\r\n\r\n"), .. answer];

    private static byte[] Response(string text) => Encoding.Latin1.GetBytes(text);
}
