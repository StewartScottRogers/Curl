using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.DnsQuery.Encrypted;
using Curl.Conformance.Extended.DnsQuery.Reference;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.DnsQuery.Encrypted;

/// <summary>
/// RFC 7858 sections 3.1 to 3.3 and RFC 8310 section 5 (BL-4681): curl's <c>dns://</c> client under
/// <c>--dns-transport tls</c> connects to port 853 by default, completes the TLS handshake before any DNS, sends the
/// query after its two-octet length prefix, and refuses a server whose certificate it cannot authenticate (unless
/// <c>-k</c>) without falling back to cleartext.
/// </summary>
[TestClass]
public sealed class DnsOverTlsCases
{
    private const string Name = "example.com.";

    private const string Url = "dns://localhost/example.com";

    private static readonly KnownFailures KnownFailures = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC7858-3.1-1")]
    [SpecRequirement("DnsQuery:RFC7858-3.2-1")]
    [SpecRequirement("DnsQuery:RFC7858-3.3-1")]
    public Task Tls_DefaultPort853_CarriesTheLengthPrefixedQueryAfterTheHandshake() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC7858-3.1-1", "DnsQuery:RFC7858-3.2-1", "DnsQuery:RFC7858-3.3-1"], async () =>
        {
            using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
            DnsReferenceServer server = new((_, _) => [new DnsReferenceReply { Answers = [DnsReferenceRecord.A(Name, 300, "192.0.2.1")] }]);

            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(
                InProcessCurl.RunAsync,
                [ReferenceRoute.Tls(853, server, certificate)],
                ["-sS", "-k", "--dns-transport", "tls", Url]);

            WireExchange exchange = result.Exchanges.Single();
            Assert.AreEqual(853, exchange.Port);
            Assert.AreEqual(ReferenceTransport.Tls, exchange.Transport);
            byte[] query = server.Received.Single().Query.Bytes;
            CollectionAssert.AreEqual(new byte[] { 0, (byte)query.Length }.Concat(query).ToArray(), exchange.ClientBytes);
            DnsQueryRecordTypeTests.AssertOutput(result, DnsQueryRecordTypeTests.Text(server, "qr rd ra", "A", "example.com.\t300\tIN\tA\t192.0.2.1\n", 1));
        });

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC8310-5-1")]
    [SpecRequirement("DnsQuery:RFC8310-5-2")]
    public Task Tls_UntrustedCertificate_IsRefusedWithExit60AndNoCleartextFallback() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC8310-5-1", "DnsQuery:RFC8310-5-2"], async () =>
        {
            using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
            DnsTlsRefusalServer server = new(certificate);
            DnsReferenceServer cleartext = new((_, _) => [new DnsReferenceReply { Answers = [DnsReferenceRecord.A(Name, 300, "192.0.2.1")] }]);

            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(
                InProcessCurl.RunAsync,
                [ReferenceRoute.Tcp(853, server), ReferenceRoute.Udp(53, cleartext), ReferenceRoute.Tcp(53, cleartext)],
                ["-sS", "--dns-transport", "tls", Url]);

            Assert.AreEqual(60, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            StringAssert.StartsWith(Encoding.UTF8.GetString(result.StandardError), "curl: (60) ");
            Assert.IsEmpty(result.StandardOutput);
            Assert.IsTrue(server.CurlRefusedTheCertificate);
            Assert.IsEmpty(cleartext.Received);
        });

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC8310-9-1")]
    public Task Tls_Query_IsPaddedWithTheEdnsPaddingOption() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC8310-9-1"], async () =>
        {
            using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
            DnsReferenceServer server = new((_, _) => [new DnsReferenceReply { Answers = [DnsReferenceRecord.A(Name, 300, "192.0.2.1")] }]);

            await ExtendedConformanceHarness.RunAsync(
                InProcessCurl.RunAsync,
                [ReferenceRoute.Tls(853, server, certificate)],
                ["-sS", "-k", "--dns-transport", "tls", Url]);

            // example.com.'s question ends at octet 29; an OPT record (root name, TYPE 41, CLASS, TTL, RDLENGTH) follows,
            // and its first option at octet 40 is Padding, option code 12 (RFC 7830).
            DnsReferenceQuery query = server.Received.Single().Query;
            Assert.AreEqual((ushort)1, query.Counts[3]);
            CollectionAssert.AreEqual(new byte[] { 0, 0, 41 }, query.Bytes[29..32]);
            CollectionAssert.AreEqual(new byte[] { 0, 12 }, query.Bytes[40..42]);
        });
}
