using Curl.Conformance.Extended.DnsQuery.Reference;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.DnsQuery;

/// <summary>RFC 4501 section 2: the <c>dns:</c> URI's authority (server and port), name, <c>TYPE=</c> and <c>CLASS=</c>, as curl's query carries them.</summary>
[TestClass]
public sealed class DnsQueryUrlTests
{
    private static readonly KnownFailures KnownFailures = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC4501-2-1")]
    public Task TypeElement_InEitherCase_SetsTheQtype() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC4501-2-1"], async () =>
        {
            DnsReferenceServer server = new((_, _) => [new DnsReferenceReply { Answers = [DnsReferenceRecord.Mx("example.com.", 300, 5, "mx.example.com.")] }]);

            CurlRunResult result = await DnsQueryRecordTypeTests.RunAsync(server, "dns://localhost/example.com?TYPE=mx");

            DnsQueryRecordTypeTests.AssertQuery(server, 15);
            DnsQueryRecordTypeTests.AssertOutput(result, DnsQueryRecordTypeTests.Text(server, "qr rd ra", "MX", "example.com.\t300\tIN\tMX\t5 mx.example.com.\n", 1));
        });

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC4501-2-2")]
    public Task ClassElement_WithType_SetsTheQclassAndQtype() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC4501-2-2"], async () =>
        {
            DnsReferenceServer server = new((_, _) => [new DnsReferenceReply { Answers = [new DnsReferenceRecord("version.bind.", 16, 3, 0, [4, .. "9.18"u8])] }]);

            CurlRunResult result = await DnsQueryRecordTypeTests.RunAsync(server, "dns://localhost/version.bind?CLASS=CH;TYPE=TXT");

            DnsQueryRecordTypeTests.AssertQuery(server, 16, 3, "version.bind.");
            DnsQueryRecordTypeTests.AssertOutput(result, DnsQueryRecordTypeTests.Text(server, "qr rd ra", "TXT", "version.bind.\t0\tCH\tTXT\t\"9.18\"\n", 1, "CH", "version.bind."));
        });

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC4501-2-3")]
    public Task Authority_WithAPort_IsTheServerQueried() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC4501-2-3"], async () =>
        {
            DnsReferenceServer server = new((_, _) => [new DnsReferenceReply { Answers = [DnsReferenceRecord.A("example.com.", 300, "192.0.2.1")] }]);

            CurlRunResult result = await DnsReferenceRun.RunAsync(InProcessCurl.RunAsync, server, ReferenceTransport.Udp, 5353, "dns://dns.example:5353/example.com");

            DnsQueryRecordTypeTests.AssertQuery(server, 1);
            Assert.AreEqual(5353, result.Exchanges.Single().Port);
            DnsQueryRecordTypeTests.AssertOutput(result, DnsQueryRecordTypeTests.Text(server, "qr rd ra", "A", "example.com.\t300\tIN\tA\t192.0.2.1\n", 1));
        });

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC4501-2-4")]
    [SpecRequirement("DnsQuery:RFC1035-4.2.2-1")]
    public Task Authority_OverTcp_IsQueriedWithATwoOctetLengthPrefix() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC4501-2-4", "DnsQuery:RFC1035-4.2.2-1"], async () =>
        {
            DnsReferenceServer server = new((_, _) => [new DnsReferenceReply { Answers = [DnsReferenceRecord.A("example.com.", 300, "192.0.2.1")] }]);

            CurlRunResult result = await DnsReferenceRun.RunAsync(InProcessCurl.RunAsync, server, ReferenceTransport.Tcp, 5300, "--dns-transport", "tcp", "dns://localhost:5300/example.com");

            DnsReceivedQuery received = server.Received.Single();
            Assert.AreEqual(ReferenceTransport.Tcp, received.Transport);
            byte[] query = DnsReferenceMessageWriter.WriteQuery(received.Query.Id, "example.com.", 1, 1);
            CollectionAssert.AreEqual(new byte[] { 0, (byte)query.Length }.Concat(query).ToArray(), result.Exchanges.Single().ClientBytes);
            DnsQueryRecordTypeTests.AssertOutput(result, DnsQueryRecordTypeTests.Text(server, "qr rd ra", "A", "example.com.\t300\tIN\tA\t192.0.2.1\n", 1));
        });
}
