using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

// Pins the --trace-config doh lines for what the A and AAAA answers decoded to (BL-850, BL-958, BL-1153).
public sealed partial class DohDnsResolverTraceTests
{
    [TestMethod]
    public async Task ResolveAsync_AnNxdomainAnswer_ReportsNoSuchNameForBothTypes()
    {
        // curl 8.22.0 decodes RCODE 3 as DOH_DNS_NXDOMAIN, "No such name" (BL-2969); the line's
        // 8.21.0 form stays until BL-2970 writes 8.22.0's.
        var nxdomain = Convert.FromHexString(MeasuredAAnswer);
        nxdomain[3] = 0x83;

        var lines = await TraceAsync(Ok(nxdomain));

        var expectedLines = new[]
            {
                "[DNS] DoH: No such name type A for example.test",
                "[DNS] DoH: No such name type AAAA for example.test",
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
}
