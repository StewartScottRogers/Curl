using System.Globalization;
using System.Net;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins the <c>-v --trace-config doh</c> lines of the HTTPS (type 65) DoH query to curl 8.22.0's
/// HTTPSRR build, derived from <c>lib/vdns/doh.c</c> and <c>lib/vdns/httpsrr.c</c> at tag
/// <c>curl-8_22_0</c> (ADR-0727's BL-2954 amendment; the reference curl on Windows has no HTTPSRR
/// feature to measure): the query's own sub-transfer lines, then its <c>[DoH]</c> and
/// <c>[DNS] [HTTPS]</c> lines, all before the A and AAAA queries' lines.
/// </summary>
[TestClass]
public sealed class DohDnsResolverHttpsRecordTraceTests
{
    private const string FooExampleCom = "03666F6F076578616D706C6503636F6D00";

    private const string HttpsQuestion = FooExampleCom + "00410001";

    // Priority 1, target ".", alpn h2 and a six-byte ech.
    private const string AlpnAndEchRecord = "000100" + "00010003026832" + "00050006AABBCCDDEEFF";

    private static readonly Uri DohUrl = new("https://127.0.0.1:41444/dns-query");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ResolveHttpsOriginAsync_WithARecord_ReportsTheHttpsLinesAndNoneForTheDecodedAAndAaaaAnswers()
    {
        var trace = new RecordingTransferEvents();
        var resolver = new DohDnsResolver(AnsweringAllThree(), DohUrl, trace, DescribeExitCode);
        Diagnostics.Arrange("origin", "https://foo.example.com/ with an HTTPS record holding alpn h2 and ech");

        _ = await resolver.ResolveHttpsOriginAsync("foo.example.com", 443, CancellationToken.None);

        WriteTrace(trace.Info);
        var expectedLines = new[]
            {
                "[DNS] [HTTPS] got 1 records",
                "[DNS] [HTTPS] record: 1 . alpn=h2",
                "[DNS] [HTTPS] record: 1 . alpn=h2 ech=<6 bytes>",
                "[DoH] DoH request complete, 0 to go",
            };
        string[] actualLines = [.. trace.Info];
        Diagnostics.Diff("trace lines", string.Join("\n", expectedLines), string.Join("\n", actualLines));
        CollectionAssert.AreEqual(expectedLines, actualLines);
    }

    [TestMethod]
    public async Task ResolveHttpsOriginAsync_WithSubTransferEvents_WritesTheHttpsSubTransferFirstOnConnectionOne()
    {
        var trace = new RecordingTransferEvents();
        var resolver = new DohDnsResolver(AnsweringAllThree(), DohUrl, trace, DescribeExitCode) { SubTransferEvents = trace };
        Diagnostics.Arrange("events", "one sink for the sub-transfer lines and the trace lines");

        _ = await resolver.ResolveHttpsOriginAsync("foo.example.com", 443, CancellationToken.None);

        WriteTrace(trace.Info);
        var expectedLines = new[]
            {
                "using HTTP/1.x",
                "upload completely sent off: 33 bytes",
                "Connection #1 to host 127.0.0.1:41444 left intact",
                "[DNS] [HTTPS] got 1 records",
                "[DNS] [HTTPS] record: 1 . alpn=h2",
                "[DNS] [HTTPS] record: 1 . alpn=h2 ech=<6 bytes>",
                "[DoH] DoH request complete, 0 to go",
                "using HTTP/1.x",
                "upload completely sent off: 33 bytes",
                "Connection #2 to host 127.0.0.1:41444 left intact",
                "a DoH request is completed, 1 to go",
                "using HTTP/1.x",
                "upload completely sent off: 33 bytes",
                "Connection #3 to host 127.0.0.1:41444 left intact",
                "a DoH request is completed, 0 to go",
            };
        string[] actualLines = [.. trace.Info.Take(expectedLines.Length)];
        Diagnostics.Diff("first lines", string.Join("\n", expectedLines), string.Join("\n", actualLines));
        CollectionAssert.AreEqual(expectedLines, actualLines);
    }

    [TestMethod]
    public async Task ResolveHttpsRecordAsync_AResponseCutShort_ReportsTheExchangeError()
    {
        var lines = await TraceHttpsRecordAsync(Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 40\r\n\r\n0001"));

        AssertLines(["[DoH] [DoH] [HTTPS] error: Failure when receiving data from the peer"], lines);
    }

    [TestMethod]
    [DataRow("83", DisplayName = "NXDOMAIN")]
    [DataRow("80", DisplayName = "no answer")]
    public async Task ResolveHttpsRecordAsync_AnAnswerWithNoRecord_ReportsNoSuchName(string flagsLow)
    {
        Diagnostics.Arrange("flags", "81" + flagsLow);
        var answer = Convert.FromHexString("000081" + flagsLow + "000100000000" + "0000" + HttpsQuestion);

        var lines = await TraceHttpsRecordAsync(Ok(answer));

        AssertLines(["[DoH] [DoH] [HTTPS] error decoding response: No such name"], lines);
    }

    [TestMethod]
    public async Task ResolveHttpsRecordAsync_AnAnswerWithOnlyACname_ReportsTheRequestComplete()
    {
        var cname = "C00C000500010000003C0008" + "0161" + "0474657374" + "00";
        var answer = Convert.FromHexString("000081800001000100000000" + HttpsQuestion + cname);

        var lines = await TraceHttpsRecordAsync(Ok(answer));

        AssertLines(["[DoH] DoH request complete, 0 to go"], lines);
    }

    [TestMethod]
    public async Task ResolveHttpsRecordAsync_TwoRecords_CountsBothAndWritesTheFirstOnly()
    {
        var lines = await TraceHttpsRecordAsync(Ok(HttpsAnswer(AlpnAndEchRecord, "000200" + "00030002" + "01BB")));

        AssertLines(
            [
                "[DNS] [HTTPS] got 2 records",
                "[DNS] [HTTPS] record: 1 . alpn=h2",
                "[DNS] [HTTPS] record: 1 . alpn=h2 ech=<6 bytes>",
                "[DoH] DoH request complete, 0 to go",
            ],
            lines);
    }

    [TestMethod]
    public async Task ResolveHttpsRecordAsync_ARecordWithEveryParameter_WritesTheRecordAfterEachParameter()
    {
        var record = "0002" + "03737663" + "0474657374" + "00"
            + "00000002" + "0001"
            + "0001000D" + "026833" + "03666F6F" + "026832" + "026833"
            + "00020000"
            + "00030002" + "01BB"
            + "00040008" + "7F000001" + "C0000201"
            + "00050002" + "ABCD"
            + "00060010" + "00000000000000000000000000000001"
            + "00070001" + "FF";

        var lines = await TraceHttpsRecordAsync(Ok(HttpsAnswer(record)));

        const string Full = "2 svc.test. mandatory-keys(ignored) alpn=h3,h2 port=443 no-default-alpn ipv6hint=::1 ipv4hint=127.0.0.1,192.0.2.1 ech=<2 bytes>";
        AssertLines(
            [
                "[DNS] [HTTPS] got 1 records",
                "[DNS] [HTTPS] record: 2 svc.test. mandatory-keys(ignored)",
                "[DNS] [HTTPS] record: 2 svc.test. mandatory-keys(ignored) alpn=h3,h2",
                "[DNS] [HTTPS] record: 2 svc.test. mandatory-keys(ignored) alpn=h3,h2 no-default-alpn",
                "[DNS] [HTTPS] record: 2 svc.test. mandatory-keys(ignored) alpn=h3,h2 port=443 no-default-alpn",
                "[DNS] [HTTPS] record: 2 svc.test. mandatory-keys(ignored) alpn=h3,h2 port=443 no-default-alpn ipv4hint=127.0.0.1,192.0.2.1",
                "[DNS] [HTTPS] record: 2 svc.test. mandatory-keys(ignored) alpn=h3,h2 port=443 no-default-alpn ipv4hint=127.0.0.1,192.0.2.1 ech=<2 bytes>",
                "[DNS] [HTTPS] record: " + Full,
                "[DNS] [HTTPS] record: " + Full,
                "[DoH] DoH request complete, 0 to go",
            ],
            lines);
    }

    // Each refusal ends the record's lines with curl's CURLcode text for it, and no "complete" line.
    [TestMethod]
    [DataRow("0001", "", "A libcurl function was given a bad argument", DisplayName = "two bytes")]
    [DataRow("0001" + "056162", "", "Out of memory", DisplayName = "target name overruns")]
    [DataRow("0001" + "010100", "", "Weird server reply", DisplayName = "target name has a control character")]
    [DataRow("000100" + "00030002" + "01BB" + "00010003026832", "1 . port=443", "Weird server reply", DisplayName = "keys out of order")]
    [DataRow("000100" + "00030004" + "01BB", "", "Weird server reply", DisplayName = "value overruns")]
    [DataRow("000100" + "00010002" + "0568", "", "Unrecognized or bad HTTP Content or Transfer-Encoding", DisplayName = "bad alpn list")]
    [DataRow("000100" + "00020001" + "00", "", "A libcurl function was given a bad argument", DisplayName = "no-default-alpn with a value")]
    public async Task ResolveHttpsRecordAsync_ARecordThatDoesNotDecode_ReportsTheDecodingError(string record, string readSoFar, string error)
    {
        Diagnostics.Arrange("record", record);

        var lines = await TraceHttpsRecordAsync(Ok(HttpsAnswer(record)));

        string[] recordLines = readSoFar.Length == 0 ? [] : ["[DNS] [HTTPS] record: " + readSoFar];
        AssertLines(["[DNS] [HTTPS] got 1 records", .. recordLines, "[DoH] [DoH] error decoding HTTPS RR: " + error], lines);
    }

    [TestMethod]
    public void FormatHttpsRecord_WithAnUnknownAlpnOnly_WritesNoAlpn()
    {
        var record = new ServiceBindingRecord(1, "a.test") { ApplicationProtocols = ["foo"] };
        Diagnostics.Arrange("record", record);

        var text = DohTraceLines.FormatHttpsRecord(record, hasMandatoryKeys: false);

        Diagnostics.Act("text", text);
        Diagnostics.Assert("text", "1 a.test.", text);
        Assert.AreEqual("1 a.test.", text);
    }

    private static FakeConnector AnsweringAllThree()
    {
        var queryA = Convert.FromHexString("000001000001000000000000" + FooExampleCom + "00010001");
        var queryAaaa = Convert.FromHexString("000001000001000000000000" + FooExampleCom + "001C0001");
        var connector = new FakeConnector();
        connector.BytesToRead.Add(Ok(HttpsAnswer(AlpnAndEchRecord)));
        connector.BytesToRead.Add(Ok(DnsTestReplies.Answer(queryA, [IPAddress.Loopback])));
        connector.BytesToRead.Add(Ok(DnsTestReplies.Answer(queryAaaa, [IPAddress.IPv6Loopback])));
        return connector;
    }

    private async Task<string[]> TraceHttpsRecordAsync(byte[] response)
    {
        var connector = new FakeConnector();
        connector.BytesToRead.Add(response);
        var trace = new RecordingTransferEvents();
        Diagnostics.Bytes("response", response);

        _ = await new DohDnsResolver(connector, DohUrl, trace, DescribeExitCode).ResolveHttpsRecordAsync("foo.example.com", 443, CancellationToken.None);

        WriteTrace(trace.Info);
        return [.. trace.Info];
    }

    private void AssertLines(string[] expected, string[] actual)
    {
        Diagnostics.Diff("trace lines", string.Join("\n", expected), string.Join("\n", actual));
        CollectionAssert.AreEqual(expected, actual);
    }

    private void WriteTrace(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            Diagnostics.Act("trace line", line);
        }
    }

    // An answer to the HTTPS question holding one HTTPS record per rdata, TTL 60.
    private static byte[] HttpsAnswer(params string[] records)
    {
        var answers = records.Select(rdata => "C00C004100010000003C" + (rdata.Length / 2).ToString("X4", CultureInfo.InvariantCulture) + rdata);
        var header = "000081800001" + records.Length.ToString("X4", CultureInfo.InvariantCulture) + "00000000";
        return Convert.FromHexString(header + HttpsQuestion + string.Concat(answers));
    }

    private static byte[] Ok(byte[] answer) =>
        [.. Encoding.Latin1.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {answer.Length}\r\n\r\n"), .. answer];

    // The curl_easy_strerror texts of curl 8.22.0 for the codes these lines carry.
    private static string DescribeExitCode(CurlExitCode exitCode) => exitCode switch
    {
        CurlExitCode.RecvError => "Failure when receiving data from the peer",
        CurlExitCode.OutOfMemory => "Out of memory",
        CurlExitCode.WeirdServerReply => "Weird server reply",
        CurlExitCode.BadFunctionArgument => "A libcurl function was given a bad argument",
        CurlExitCode.BadContentEncoding => "Unrecognized or bad HTTP Content or Transfer-Encoding",
        _ => exitCode.ToString(),
    };
}
