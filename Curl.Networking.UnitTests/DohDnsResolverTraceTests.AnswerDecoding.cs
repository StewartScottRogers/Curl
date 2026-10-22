using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

// Pins the --trace-config doh lines for what the A and AAAA answers decoded to (BL-958, BL-1153, BL-2970).
public sealed partial class DohDnsResolverTraceTests
{
    [TestMethod]
    public async Task ResolveAsync_AnAAnswer_ReportsNoLineAndResolvesItsAddress()
    {
        // Measured: "DoH request complete, 0 to go" (a sub-transfer line), then "Host example.test:P was resolved."
        var (lines, addresses) = await TraceOnlyAAsync(Ok(Convert.FromHexString(MeasuredAAnswer)));

        Diagnostics.Assert("trace line count", 0, lines.Length);
        Assert.IsEmpty(lines);
        Assert.AreEqual("127.0.0.1", addresses.Single());
    }

    [TestMethod]
    public async Task ResolveAsync_AnNxdomainAnswer_ReportsNoSuchNameAndResolvesNothing()
    {
        // Measured: "[DoH] [DoH] [A] error decoding response: No such name", then exit 6.
        var nxdomain = Convert.FromHexString(MeasuredAAnswer);
        nxdomain[3] = 0x83;

        var (lines, addresses) = await TraceOnlyAAsync(Ok(nxdomain));

        AssertLines(["[DoH] [DoH] [A] error decoding response: No such name"], lines);
        Assert.IsEmpty(addresses);
    }

    [TestMethod]
    public async Task ResolveAsync_ACnameOnlyAnswer_ReportsNoLineAndResolvesNothing()
    {
        // Measured: "DoH request complete, 0 to go", then exit 6; 8.22.0 skips CNAME data.
        var (lines, addresses) = await TraceOnlyAAsync(Ok(Convert.FromHexString(CnameAnswerToA)));

        Diagnostics.Assert("trace line count", 0, lines.Length);
        Assert.IsEmpty(lines);
        Assert.IsEmpty(addresses);
    }

    [TestMethod]
    public async Task ResolveAsync_AnEmptyAnswer_ReportsNoLineAndResolvesNothing()
    {
        // Measured: "DoH request complete, 0 to go", then exit 6; 8.22.0 has no "No content".
        var (lines, addresses) = await TraceOnlyAAsync(Ok(Convert.FromHexString(EmptyAnswerToA)));

        Diagnostics.Assert("trace line count", 0, lines.Length);
        Assert.IsEmpty(lines);
        Assert.IsEmpty(addresses);
    }

    [TestMethod]
    public async Task ResolveAsync_AnNxdomainAnswerToBothQueries_ReportsNoSuchNameForEach()
    {
        // Measured without -4: one error line per query (8.22.0 sends AAAA first, and so does Curl, BL-2973).
        var nxdomain = Convert.FromHexString(MeasuredAAnswer);
        nxdomain[3] = 0x83;

        var lines = await TraceAsync(Ok(nxdomain));

        AssertLines(
            ["[DoH] [DoH] [AAAA] error decoding response: No such name", "[DoH] [DoH] [A] error decoding response: No such name"],
            lines);
    }

    [TestMethod]
    public async Task ResolveAsync_ATruncatedAnswer_ReportsOutOfRangeForAAndUnexpectedTypeForAaaa()
    {
        // Content-Length 45 on the 46-byte A answer: the A query's record data is cut short, and the
        // AAAA query meets the A record's type before its data.
        var answer = Convert.FromHexString(MeasuredAAnswer);

        var lines = await TraceAsync([.. Response($"HTTP/1.1 200 OK\r\nContent-Length: {answer.Length - 1}\r\n\r\n"), .. answer]);

        AssertLines(
            ["[DoH] [DoH] [AAAA] error decoding response: Unexpected TYPE", "[DoH] [DoH] [A] error decoding response: Out of range"],
            lines);
    }

    [TestMethod]
    public async Task ResolveAsync_AFailedADecodeAfterAnAddress_ReportsTheFailureAndResolvesThatAddress()
    {
        // Measured with 8.21.0 (BL-958): a failed decode's address still reaches the resolved list.
        var connector = new FakeConnector();
        connector.BytesToRead.Add(Ok(Convert.FromHexString(AThenAaaaAnswerToA)));
        connector.BytesToRead.Add(Ok(Convert.FromHexString(AaaaAnswerToAaaa)));

        var (lines, addresses) = await ResolveAsync(connector, System.Net.Sockets.AddressFamily.Unspecified);

        AssertLines(["[DoH] [DoH] [A] error decoding response: Unexpected TYPE"], lines);
        CollectionAssert.AreEqual(new[] { "::1", "127.0.0.2" }, addresses);
    }

    [TestMethod]
    public async Task ResolveAsync_BothDecodesFailingAfterAnAddress_ReportsBothAndResolvesNothing()
    {
        // Measured with 8.21.0 (BL-958): both decodes failed, no entry, exit 6.
        var connector = new FakeConnector();
        connector.BytesToRead.Add(Ok(Convert.FromHexString(AaaaThenAAnswerToAaaa)));
        connector.BytesToRead.Add(Ok(Convert.FromHexString(AThenAaaaAnswerToA)));

        var (lines, addresses) = await ResolveAsync(connector, System.Net.Sockets.AddressFamily.Unspecified);

        AssertLines(
            ["[DoH] [DoH] [AAAA] error decoding response: Unexpected TYPE", "[DoH] [DoH] [A] error decoding response: Unexpected TYPE"],
            lines);
        Assert.IsEmpty(addresses);
    }

    [TestMethod]
    public async Task ResolveAsync_AddressesOverTheSharedLimit_KeepsTheFirst24()
    {
        // Measured with 8.21.0 (BL-1153): A 127.0.0.1-20, AAAA ::1-::a, then "IPv6: ::1, ::2, ::3, ::4"
        // and "IPv4: 127.0.0.1, ..., 127.0.0.20".
        var connector = new FakeConnector();
        connector.BytesToRead.Add(Ok(LimitAnswer(DnsRecordType.A, 20)));
        connector.BytesToRead.Add(Ok(LimitAnswer(DnsRecordType.Aaaa, 10)));

        var (lines, addresses) = await ResolveAsync(connector, System.Net.Sockets.AddressFamily.Unspecified);

        Assert.IsEmpty(lines);
        CollectionAssert.AreEqual(
            Enumerable.Range(1, 4).Select(n => $"::{n}").Concat(Enumerable.Range(1, 20).Select(n => $"127.0.0.{n}")).ToArray(),
            addresses);
    }

    [TestMethod]
    public async Task ResolveAsync_AFillingTheSharedLimit_KeepsNothingFromTheAaaaAnswer()
    {
        // Measured with 8.21.0 (BL-1153): A 127.0.0.1-24, AAAA ::1-::5, then "IPv6: (none)" and the 24 IPv4 addresses.
        var connector = new FakeConnector();
        connector.BytesToRead.Add(Ok(LimitAnswer(DnsRecordType.A, 24)));
        connector.BytesToRead.Add(Ok(LimitAnswer(DnsRecordType.Aaaa, 5)));

        var (lines, addresses) = await ResolveAsync(connector, System.Net.Sockets.AddressFamily.Unspecified);

        Assert.IsEmpty(lines);
        CollectionAssert.AreEqual(Enumerable.Range(1, 24).Select(n => $"127.0.0.{n}").ToArray(), addresses);
    }
}
