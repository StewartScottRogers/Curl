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
public sealed partial class DohDnsResolverTraceTests
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
