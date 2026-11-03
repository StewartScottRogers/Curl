using System.Globalization;
using System.Text;
using Curl.Conformance.Extended.DnsQuery.Reference;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.DnsQuery;

/// <summary>
/// RFC 1035 sections 4.1.1 and 4.1.4 and RFC 5452 section 9.1: curl's <c>dns://</c> client follows compression
/// pointers that point back and refuses one that points forward or at itself, takes only the reply whose ID and
/// question match its query, and writes each response code 0 to 5 with exit 0, as dig does.
/// </summary>
[TestClass]
public sealed class DnsQueryMessageTests
{
    private const string Name = "example.com.";

    private const string Url = "dns://localhost/example.com";

    private static readonly KnownFailures KnownFailures = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC1035-4.1.4-4")]
    public Task Reply_WithAPointerToAPointerAfterALabel_IsReadAsTheWholeName() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC1035-4.1.4-4"], async () =>
        {
            // Offset 12: the question's name, 13 octets, then QTYPE and QCLASS; the answer starts at offset 29.
            // The CNAME's owner points at offset 12; its RDATA, at offset 41, is "www" and a pointer to offset 12;
            // the A record's owner points at offset 41, a name that itself ends in a pointer.
            DnsReferenceServer server = new((query, _) =>
            [
                Raw(query.Id, 0x8180, 2, questionType: 5, records: [0xC0, 12, 0, 5, 0, 1, 0, 0, 1, 44, 0, 6, 3, (byte)'w', (byte)'w', (byte)'w', 0xC0, 12, 0xC0, 41, 0, 1, 0, 1, 0, 0, 0, 60, 0, 4, 192, 0, 2, 7]),
            ]);

            CurlRunResult result = await RunAsync(server, Url + "?type=CNAME");

            AssertOutput(result, Text(server, "NOERROR", "CNAME", 2, "example.com.\t300\tIN\tCNAME\twww.example.com.\nwww.example.com.\t60\tIN\tA\t192.0.2.7\n"));
        });

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC1035-4.1.4-5")]
    public Task Reply_WithAPointerForwardToALaterName_IsRefused() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC1035-4.1.4-5"], async () =>
        {
            // The first answer's owner points at offset 45, the second answer's owner, written in full after it.
            DnsReferenceServer server = new((query, _) =>
            [
                Raw(query.Id, 0x8180, 2, [0xC0, 45, 0, 1, 0, 1, 0, 0, 0, 60, 0, 4, 192, 0, 2, 1, .. DnsReferenceName.Encode(Name), 0, 1, 0, 1, 0, 0, 0, 60, 0, 4, 192, 0, 2, 2]),
            ]);

            AssertRefused(await RunAsync(server, Url), "curl: (8) A DNS compression pointer does not point back to an earlier name.\n");
        });

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC1035-4.1.4-6")]
    public Task Reply_WithAPointerToItself_IsRefusedRatherThanFollowedForever() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC1035-4.1.4-6"], async () =>
        {
            // The answer's owner, at offset 29, points at offset 29.
            DnsReferenceServer server = new((query, _) => [Raw(query.Id, 0x8180, 1, [0xC0, 29, 0, 1, 0, 1, 0, 0, 0, 60, 0, 4, 192, 0, 2, 1])]);

            AssertRefused(await RunAsync(server, Url), "curl: (8) A DNS compression pointer does not point back to an earlier name.\n");
        });

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC5452-9.1-1")]
    public Task ReplyWithAnotherId_IsIgnoredAndTheMatchingReplyAfterItTaken() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC5452-9.1-1"], async () =>
        {
            DnsReferenceServer server = new((query, _) =>
            [
                new DnsReferenceReply { Id = (ushort)(query.Id ^ 0x5A5A), Answers = [DnsReferenceRecord.A(Name, 60, "192.0.2.66")] },
                new DnsReferenceReply { Answers = [DnsReferenceRecord.A(Name, 60, "192.0.2.1")] },
            ]);

            CurlRunResult result = await RunAsync(server, Url);

            Assert.HasCount(1, server.Received);
            AssertOutput(result, Text(server, "NOERROR", "A", 1, "example.com.\t60\tIN\tA\t192.0.2.1\n"));
        });

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC5452-9.1-2")]
    public Task ReplyWithAnotherQuestion_IsIgnoredAndTheMatchingReplyAfterItTaken() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC5452-9.1-2"], async () =>
        {
            DnsReferenceServer server = new((query, _) =>
            [
                Raw(query.Id, 0x8180, 0, [], "example.net."),
                new DnsReferenceReply { Answers = [DnsReferenceRecord.A(Name, 60, "192.0.2.1")] },
            ]);

            CurlRunResult result = await RunAsync(server, Url);

            Assert.HasCount(1, server.Received);
            AssertOutput(result, Text(server, "NOERROR", "A", 1, "example.com.\t60\tIN\tA\t192.0.2.1\n"));
        });

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC1035-4.1.1-3")]
    public Task ResponseCode0_NoError_IsWrittenWithItsAnswersAndExit0() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC1035-4.1.1-3"], async () =>
        {
            DnsReferenceServer server = new((_, _) => [new DnsReferenceReply { Flags = 0x8180, Answers = [DnsReferenceRecord.A(Name, 60, "192.0.2.1")] }]);

            AssertOutput(await RunAsync(server, Url), Text(server, "NOERROR", "A", 1, "example.com.\t60\tIN\tA\t192.0.2.1\n"));
        });

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC1035-4.1.1-4")]
    public Task ResponseCode1_FormatError_IsWrittenAsFormerrWithExit0() => AssertResponseCodeAsync("DnsQuery:RFC1035-4.1.1-4", 1, "FORMERR");

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC1035-4.1.1-5")]
    public Task ResponseCode2_ServerFailure_IsWrittenAsServfailWithExit0() => AssertResponseCodeAsync("DnsQuery:RFC1035-4.1.1-5", 2, "SERVFAIL");

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC1035-4.1.1-6")]
    public Task ResponseCode3_NameError_IsWrittenAsNxdomainWithExit0() => AssertResponseCodeAsync("DnsQuery:RFC1035-4.1.1-6", 3, "NXDOMAIN");

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC1035-4.1.1-7")]
    public Task ResponseCode4_NotImplemented_IsWrittenAsNotimpWithExit0() => AssertResponseCodeAsync("DnsQuery:RFC1035-4.1.1-7", 4, "NOTIMP");

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC1035-4.1.1-8")]
    public Task ResponseCode5_Refused_IsWrittenAsRefusedWithExit0() => AssertResponseCodeAsync("DnsQuery:RFC1035-4.1.1-8", 5, "REFUSED");

    private static Task AssertResponseCodeAsync(string requirementId, ushort responseCode, string mnemonic) =>
        KnownFailures.RunCaseAsync([requirementId], async () =>
        {
            DnsReferenceServer server = new((_, _) => [new DnsReferenceReply { Flags = (ushort)(0x8180 | responseCode) }]);

            AssertOutput(await RunAsync(server, Url), Text(server, mnemonic, "A", 0, string.Empty));
        });

    private static Task<CurlRunResult> RunAsync(DnsReferenceServer server, string url) =>
        DnsReferenceRun.RunAsync(InProcessCurl.RunAsync, server, ReferenceTransport.Udp, 53, url);

    /// <summary>A reply of one question, <paramref name="questionName" /> of type <paramref name="questionType" /> and class IN, then <paramref name="records" /> as raw octets.</summary>
    private static DnsReferenceReply Raw(ushort id, ushort flags, byte answerCount, byte[] records, string questionName = Name, byte questionType = 1) =>
        new()
        {
            Raw = [(byte)(id >> 8), (byte)id, (byte)(flags >> 8), (byte)flags, 0, 1, 0, answerCount, 0, 0, 0, 0, .. DnsReferenceName.Encode(questionName), 0, questionType, 0, 1, .. records],
        };

    private static void AssertOutput(CurlRunResult result, string expected)
    {
        Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
        Assert.AreEqual(expected, Encoding.UTF8.GetString(result.StandardOutput));
        Assert.IsEmpty(result.StandardError);
    }

    private static void AssertRefused(CurlRunResult result, string standardError)
    {
        Assert.AreEqual(8, result.ExitCode);
        Assert.IsEmpty(result.StandardOutput);
        // curl ends its message line with the platform's newline, CRLF on Windows.
        Assert.AreEqual(standardError, Encoding.UTF8.GetString(result.StandardError).Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    private static string Text(DnsReferenceServer server, string status, string type, int answerCount, string answers) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $";; ->>HEADER<<- opcode: QUERY, status: {status}, id: {server.Received[0].Query.Id}\n;; flags: qr rd ra; QUERY: 1, ANSWER: {answerCount}, AUTHORITY: 0, ADDITIONAL: 0\n\n;; QUESTION SECTION:\n;{Name}\t\tIN\t{type}\n")
        + (answerCount == 0 ? string.Empty : "\n;; ANSWER SECTION:\n" + answers);
}
