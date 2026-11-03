using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.DnsQuery.Encrypted;
using Curl.Conformance.Extended.DnsQuery.Reference;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.DnsQuery.Encrypted;

/// <summary>
/// RFC 8484 (BL-4681): curl's <c>dns://</c> client under <c>--dns-transport https</c> connects to port 443 by default,
/// POSTs the query as an <c>application/dns-message</c> body (it never sends GET, so no <c>dns=</c> parameter), takes
/// a 2xx response as the DNS answer whatever its RCODE, and a non-2xx status as no answer.
/// </summary>
[TestClass]
public sealed class DnsOverHttpsCases
{
    private const string Name = "example.com.";

    private const string Url = "dns://localhost/example.com";

    private static readonly KnownFailures KnownFailures = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC8484-4.1-1")]
    [SpecRequirement("DnsQuery:RFC8484-4.1-3")]
    [SpecRequirement("DnsQuery:RFC8484-6-1")]
    public Task Https_DefaultPort443_PostsTheQueryAsAnApplicationDnsMessageBody() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC8484-4.1-1", "DnsQuery:RFC8484-4.1-3", "DnsQuery:RFC8484-6-1"], async () =>
        {
            DohReferenceServer server = new((_, _) => [new DnsReferenceReply { Answers = [DnsReferenceRecord.A(Name, 300, "192.0.2.1")] }]);

            CurlRunResult result = await RunAsync(server);

            Assert.AreEqual(443, result.Exchanges.Single().Port);
            DohReceivedRequest request = server.Received.Single();
            Assert.AreEqual("POST /dns-query HTTP/1.1", request.RequestLine);
            Assert.AreEqual("application/dns-message", request.Headers["Content-Type"]);
            Assert.AreEqual("application/dns-message", request.Headers["Accept"]);
            Assert.AreEqual(request.Body.Length.ToString(CultureInfo.InvariantCulture), request.Headers["Content-Length"]);
            DnsReferenceQuery query = DnsReferenceQuery.Decode(request.Body);
            Assert.AreEqual(Name, query.Name);
            Assert.AreEqual((ushort)1, query.Type);
            DnsQueryRecordTypeTests.AssertOutput(result, Text(request, "NOERROR", "example.com.\t300\tIN\tA\t192.0.2.1\n", 1));
        });

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC8484-4.1-2")]
    public Task Https_Query_CarriesDnsId0() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC8484-4.1-2"], async () =>
        {
            DohReferenceServer server = new((_, _) => [new DnsReferenceReply { Answers = [DnsReferenceRecord.A(Name, 300, "192.0.2.1")] }]);

            await RunAsync(server);

            Assert.AreEqual((ushort)0, BinaryPrimitives.ReadUInt16BigEndian(server.Received.Single().Body));
        });

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC8484-4.2.1-1")]
    public Task Https_2xxWithServfail_IsWrittenAsTheDnsAnswer() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC8484-4.2.1-1"], async () =>
        {
            // QR, RD, RA and RCODE 2 (SERVFAIL).
            DohReferenceServer server = new((_, _) => [new DnsReferenceReply { Flags = 0x8182 }]);

            CurlRunResult result = await RunAsync(server);

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            StringAssert.Contains(Encoding.UTF8.GetString(result.StandardOutput), "status: SERVFAIL");
            Assert.IsEmpty(result.StandardError);
        });

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC8484-4.2.1-2")]
    public Task Https_Non2xxStatus_IsNoAnswerAndExit22() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC8484-4.2.1-2"], async () =>
        {
            DohReferenceServer server = new((_, _) => [], status: 503);

            CurlRunResult result = await RunAsync(server);

            Assert.AreEqual(22, result.ExitCode);
            Assert.IsEmpty(result.StandardOutput);
            Assert.AreEqual(
                "curl: (22) The DoH server answered HTTP 503\n",
                Encoding.UTF8.GetString(result.StandardError).Replace("\r\n", "\n", StringComparison.Ordinal));
            Assert.HasCount(1, server.Received);
        });

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC8484-5.2-1")]
    public Task Https_Request_IsSentOverHttp2() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC8484-5.2-1"], async () =>
        {
            using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
            DohHttp2ReferenceServer server = new((_, _) => [new DnsReferenceReply { Answers = [DnsReferenceRecord.A(Name, 300, "192.0.2.1")] }], certificate);

            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(
                InProcessCurl.RunAsync,
                [ReferenceRoute.Tcp(443, server)],
                ["-sS", "-k", "--dns-transport", "https", Url]);

            // The server selects h2 through ALPN and reads only HTTP/2, so a request it received came after the client's
            // connection preface (RFC 9113 section 3.4) as a POST on one stream.
            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Curl.Conformance.Extended.Grpc.GrpcReferenceCall request = server.Received.Single();
            Assert.AreEqual("POST", request.Header(":method"));
            Assert.AreEqual("/dns-query", request.Path);
            Assert.AreEqual("application/dns-message", request.Header("content-type"));
            StringAssert.Contains(Encoding.UTF8.GetString(result.StandardOutput), "192.0.2.1");
        });

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC8484-5-1")]
    public Task Https_UntrustedCertificate_IsRefusedWithExit60BeforeAnyRequest() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC8484-5-1"], async () =>
        {
            using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
            DnsTlsRefusalServer server = new(certificate);

            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(
                InProcessCurl.RunAsync,
                [ReferenceRoute.Tcp(443, server)],
                ["-sS", "--dns-transport", "https", Url]);

            Assert.AreEqual(60, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            StringAssert.StartsWith(Encoding.UTF8.GetString(result.StandardError), "curl: (60) ");
            Assert.IsEmpty(result.StandardOutput);
            Assert.IsTrue(server.CurlRefusedTheCertificate);
        });

    private static async Task<CurlRunResult> RunAsync(DohReferenceServer server)
    {
        using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
        return await ExtendedConformanceHarness.RunAsync(
            InProcessCurl.RunAsync,
            [ReferenceRoute.Tls(443, server, certificate)],
            ["-sS", "-k", "--dns-transport", "https", Url]);
    }

    private static string Text(DohReceivedRequest request, string status, string answers, int answerCount) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $";; ->>HEADER<<- opcode: QUERY, status: {status}, id: {BinaryPrimitives.ReadUInt16BigEndian(request.Body)}\n;; flags: qr rd ra; QUERY: 1, ANSWER: {answerCount}, AUTHORITY: 0, ADDITIONAL: 0\n\n;; QUESTION SECTION:\n;{Name}\t\tIN\tA\n\n;; ANSWER SECTION:\n{answers}");
}
