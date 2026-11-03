using System.Globalization;
using System.Text;
using Curl.Conformance.Extended.DnsQuery.Reference;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.DnsQuery;

/// <summary>
/// RFC 1035 sections 3.3, 3.4 and 4.1, RFC 3596, RFC 2782, RFC 8659 and RFC 3597: for each record type curl's <c>dns://</c>
/// client sends a one-question query for it, and writes the answer's RDATA as text and with <c>--dns-json</c>.
/// </summary>
[TestClass]
public sealed class DnsQueryRecordTypeTests
{
    private const string Name = "example.com.";

    private static readonly KnownFailures KnownFailures = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC1035-3.4.1-1")]
    [SpecRequirement("DnsQuery:RFC1035-4.1.1-1")]
    [SpecRequirement("DnsQuery:RFC1035-4.1.4-1")]
    public Task A_IsQueriedAndItsAddressWritten() =>
        AssertRecordAsync("DnsQuery:RFC1035-3.4.1-1", "A", 1, DnsReferenceRecord.A(Name, 300, "192.0.2.1"), "192.0.2.1", "\"address\":\"192.0.2.1\"");

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC3596-2.2-1")]
    public Task Aaaa_IsQueriedAndItsAddressWritten() =>
        AssertRecordAsync("DnsQuery:RFC3596-2.2-1", "AAAA", 28, DnsReferenceRecord.Aaaa(Name, 300, "2001:db8::1"), "2001:db8::1", "\"address\":\"2001:db8::1\"");

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC1035-3.3.11-1")]
    public Task Ns_IsQueriedAndItsNameServerWritten() =>
        AssertRecordAsync("DnsQuery:RFC1035-3.3.11-1", "NS", 2, DnsReferenceRecord.OfName(Name, 2, 86400, "ns1.example.com."), "ns1.example.com.", "\"nsdname\":\"ns1.example.com.\"");

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC1035-3.3.12-1")]
    public Task Ptr_IsQueriedAndItsNameWritten() =>
        AssertRecordAsync("DnsQuery:RFC1035-3.3.12-1", "PTR", 12, DnsReferenceRecord.OfName(Name, 12, 60, "host.example.net."), "host.example.net.", "\"ptrdname\":\"host.example.net.\"");

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC1035-3.3.9-1")]
    public Task Mx_IsQueriedAndItsPreferenceAndExchangeWritten() =>
        AssertRecordAsync("DnsQuery:RFC1035-3.3.9-1", "MX", 15, DnsReferenceRecord.Mx(Name, 300, 10, "mail.example.com."), "10 mail.example.com.", "\"preference\":10,\"exchange\":\"mail.example.com.\"");

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC1035-3.3.14-1")]
    public Task Txt_IsQueriedAndEachCharacterStringWritten() =>
        AssertRecordAsync("DnsQuery:RFC1035-3.3.14-1", "TXT", 16, DnsReferenceRecord.Txt(Name, 300, "v=spf1 -all", "say \"hi\""), "\"v=spf1 -all\" \"say \\\"hi\\\"\"", "\"txt\":[\"v=spf1 -all\",\"say \\\\\\\"hi\\\\\\\"\"]");

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC1035-3.3.13-1")]
    public Task Soa_IsQueriedAndItsSevenFieldsWritten() =>
        AssertRecordAsync(
            "DnsQuery:RFC1035-3.3.13-1",
            "SOA",
            6,
            DnsReferenceRecord.Soa(Name, 3600, "ns1.example.com.", "hostmaster.example.com.", 2026110301, 7200, 900, 1209600, 300),
            "ns1.example.com. hostmaster.example.com. 2026110301 7200 900 1209600 300",
            "\"mname\":\"ns1.example.com.\",\"rname\":\"hostmaster.example.com.\",\"serial\":2026110301,\"refresh\":7200,\"retry\":900,\"expire\":1209600,\"minimum\":300");

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC2782-RDATA-1")]
    public Task Srv_IsQueriedAndItsPriorityWeightPortAndTargetWritten() =>
        AssertRecordAsync("DnsQuery:RFC2782-RDATA-1", "SRV", 33, DnsReferenceRecord.Srv(Name, 300, 10, 60, 5060, "sip.example.com."), "10 60 5060 sip.example.com.", "\"priority\":10,\"weight\":60,\"port\":5060,\"target\":\"sip.example.com.\"");

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC8659-4.1-1")]
    public Task Caa_IsQueriedAndItsFlagsTagAndValueWritten() =>
        AssertRecordAsync("DnsQuery:RFC8659-4.1-1", "CAA", 257, DnsReferenceRecord.Caa(Name, 300, 0, "issue", "ca.example.net"), "0 issue \"ca.example.net\"", "\"flags\":0,\"tag\":\"issue\",\"value\":[\"ca.example.net\"]");

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC3597-5-1")]
    public Task UnknownType_IsQueriedByItsNumberAndItsRdataWrittenAsGenericHex() =>
        AssertRecordAsync("DnsQuery:RFC3597-5-1", "TYPE731", 731, new DnsReferenceRecord(Name, 731, DnsReferenceRecord.InternetClass, 300, [0xAB, 0xCD, 0xEF]), "\\# 3 ABCDEF", "\"rdata\":\"\\\\# 3 ABCDEF\"");

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC1035-3.3.1-1")]
    [SpecRequirement("DnsQuery:RFC1035-4.1.4-2")]
    public Task Cname_IsWrittenWithTheRecordsOfItsTargetNamedInFull() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC1035-3.3.1-1", "DnsQuery:RFC1035-4.1.4-2"], async () =>
        {
            DnsReferenceServer server = new((query, _) =>
                [new DnsReferenceReply { Answers = [DnsReferenceRecord.OfName(Name, 5, 300, "www.example.org."), DnsReferenceRecord.A("www.example.org.", 60, "192.0.2.7")] }]);

            CurlRunResult result = await RunAsync(server, "dns://localhost/example.com?type=CNAME");

            AssertQuery(server, 5);
            AssertOutput(result, Text(server, "qr rd ra", "CNAME", "example.com.\t300\tIN\tCNAME\twww.example.org.\nwww.example.org.\t60\tIN\tA\t192.0.2.7\n", 2));
        });

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC1035-4.1.1-2")]
    public Task ReplyHeader_IdAndFlagsAreWrittenAsTheServerSetThem() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC1035-4.1.1-2"], async () =>
        {
            DnsReferenceServer server = new((query, _) =>
                [new DnsReferenceReply { Id = query.Id, Flags = 0x8500, Answers = [DnsReferenceRecord.A(Name, 300, "192.0.2.1")] }]);

            CurlRunResult result = await RunAsync(server, "dns://localhost/example.com");

            AssertOutput(result, Text(server, "qr aa rd", "A", "example.com.\t300\tIN\tA\t192.0.2.1\n", 1));
        });

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC1035-4.1.4-3")]
    public Task Reply_WithEveryNameUncompressed_IsRead() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC1035-4.1.4-3"], async () =>
        {
            DnsReferenceServer server = new((query, _) =>
            [
                new DnsReferenceReply
                {
                    Raw =
                    [
                        (byte)(query.Id >> 8), (byte)query.Id, 0x81, 0x80, 0, 1, 0, 1, 0, 0, 0, 0,
                        .. DnsReferenceName.Encode(Name), 0, 1, 0, 1,
                        .. DnsReferenceName.Encode(Name), 0, 1, 0, 1, 0, 0, 0, 30, 0, 4, 192, 0, 2, 9,
                    ],
                },
            ]);

            CurlRunResult result = await RunAsync(server, "dns://localhost/example.com");

            AssertOutput(result, Text(server, "qr rd ra", "A", "example.com.\t30\tIN\tA\t192.0.2.9\n", 1));
        });

    internal static Task<CurlRunResult> RunAsync(DnsReferenceServer server, params string[] arguments) =>
        DnsReferenceRun.RunAsync(InProcessCurl.RunAsync, server, ReferenceTransport.Udp, 53, arguments);

    internal static void AssertQuery(DnsReferenceServer server, ushort type, ushort recordClass = DnsReferenceRecord.InternetClass, string name = Name)
    {
        Assert.IsNotEmpty(server.Received);
        foreach (DnsReferenceQuery query in server.Received.Select(received => received.Query))
        {
            CollectionAssert.AreEqual(DnsReferenceMessageWriter.WriteQuery(query.Id, name, type, recordClass), query.Bytes);
        }
    }

    internal static void AssertOutput(CurlRunResult result, string expected)
    {
        Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
        Assert.AreEqual(expected, Encoding.UTF8.GetString(result.StandardOutput));
        Assert.IsEmpty(result.StandardError);
    }

    internal static string Text(DnsReferenceServer server, string flags, string type, string answers, int answerCount, string questionClass = "IN", string name = Name) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $";; ->>HEADER<<- opcode: QUERY, status: NOERROR, id: {server.Received[0].Query.Id}\n;; flags: {flags}; QUERY: 1, ANSWER: {answerCount}, AUTHORITY: 0, ADDITIONAL: 0\n\n;; QUESTION SECTION:\n;{name}\t\t{questionClass}\t{type}\n\n;; ANSWER SECTION:\n{answers}");

    private static Task AssertRecordAsync(string requirementId, string type, ushort typeCode, DnsReferenceRecord record, string textData, string jsonData) =>
        KnownFailures.RunCaseAsync([requirementId], async () =>
        {
            DnsReferenceServer server = new((_, _) => [new DnsReferenceReply { Answers = [record] }]);
            string url = "dns://localhost/example.com?type=" + type;

            CurlRunResult text = await RunAsync(server, url);
            CurlRunResult json = await RunAsync(server, "--dns-json", url);

            AssertQuery(server, typeCode);
            AssertOutput(text, Text(server, "qr rd ra", type, string.Create(CultureInfo.InvariantCulture, $"example.com.\t{record.TimeToLive}\tIN\t{type}\t{textData}\n"), 1));
            AssertOutput(
                json,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{{\"id\":{server.Received[1].Query.Id},\"opcode\":\"QUERY\",\"status\":\"NOERROR\",\"flags\":[\"qr\",\"rd\",\"ra\"],\"question\":[{{\"name\":\"example.com.\",\"type\":\"{type}\",\"class\":\"IN\"}}],\"answer\":[{{\"name\":\"example.com.\",\"type\":\"{type}\",\"class\":\"IN\",\"ttl\":{record.TimeToLive},\"data\":{{{jsonData}}}}}],\"authority\":[],\"additional\":[]}}\n"));
        });
}
