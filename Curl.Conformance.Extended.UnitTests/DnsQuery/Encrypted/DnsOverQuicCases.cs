using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.DnsQuery.Encrypted;
using Curl.Conformance.Extended.DnsQuery.Reference;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Http3Server;
using Curl.Console;
using Curl.Tls;

namespace Curl.Conformance.Extended.UnitTests.DnsQuery.Encrypted;

/// <summary>
/// RFC 9250 (BL-4683): curl's <c>dns://</c> client under <c>--dns-transport quic</c> connects to UDP port 853 by
/// default with the ALPN protocol <c>doq</c>, sends its query with Message ID 0 behind a two-octet length prefix on the
/// first client-initiated bidirectional stream and ends that stream with FIN, treats a non-zero Message ID or a
/// connection close as an error, and refuses a server it cannot authenticate.
/// </summary>
[TestClass]
public sealed class DnsOverQuicCases
{
    private const string Name = "example.com.";

    private const string Url = "dns://localhost/example.com";

    private const string AnswerText = ";; flags: qr rd ra; QUERY: 1, ANSWER: 1, AUTHORITY: 0, ADDITIONAL: 0\n\n;; QUESTION SECTION:\n;example.com.\t\tIN\tA\n\n;; ANSWER SECTION:\nexample.com.\t300\tIN\tA\t192.0.2.1\n";

    private static readonly KnownFailures KnownFailures = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC9250-4.1-1")]
    [SpecRequirement("DnsQuery:RFC9250-4.1.1-1")]
    [SpecRequirement("DnsQuery:RFC9250-4.2-1")]
    [SpecRequirement("DnsQuery:RFC9250-4.2-2")]
    [SpecRequirement("DnsQuery:RFC9250-4.2-3")]
    [SpecRequirement("DnsQuery:RFC9250-4.2.1-1")]
    public Task Quic_DefaultPort853_SendsTheLengthPrefixedQueryWithMessageId0AndFinOnTheFirstStream() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC9250-4.1-1", "DnsQuery:RFC9250-4.1.1-1", "DnsQuery:RFC9250-4.2-1", "DnsQuery:RFC9250-4.2-2", "DnsQuery:RFC9250-4.2-3", "DnsQuery:RFC9250-4.2.1-1"], async () =>
        {
            using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
            using DoqReferenceServer server = Server(certificate, Answer);

            CurlRunResult result = await RunAsync(server, DoqReferenceServer.Port, "-k", Url);

            // The server's handshake accepts only ALPN doq, and it records a query only once its stream's FIN arrived.
            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.IsTrue(server.IsHandshakeComplete);
            DoqReceivedQuery received = server.Received.Single();
            Assert.AreEqual(0UL, received.StreamId);
            Assert.AreEqual((ushort)0, received.Query.Id);
            byte[] message = received.Query.Bytes;
            CollectionAssert.AreEqual(new byte[] { 0, (byte)message.Length }.Concat(message).ToArray(), received.StreamBytes);
            AssertAnswer(result);
            Assert.AreEqual(0UL, server.ClientClose!.ErrorCode);
        });

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC9250-4.2.1-2")]
    [SpecRequirement("DnsQuery:RFC9250-4.3.3-1")]
    public Task Quic_AnswerWithANonZeroMessageId_IsAProtocolErrorWithExit8() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC9250-4.2.1-2", "DnsQuery:RFC9250-4.3.3-1"], async () =>
        {
            using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
            using DoqReferenceServer server = Server(certificate, (_, _) => [new DnsReferenceReply { Id = 0x1234, Answers = [DnsReferenceRecord.A(Name, 300, "192.0.2.1")] }]);

            CurlRunResult result = await RunAsync(server, DoqReferenceServer.Port, "-k", Url);

            AssertFailed(result, 8, "The DNS-over-QUIC server's answer was not one length-prefixed message with Message ID 0");
            Assert.AreEqual(2UL, server.ClientClose!.ErrorCode);
        });

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC9250-4.3-1")]
    public Task Quic_ServerClosesWithAQuicErrorCode_IsNoAnswerAndExit56() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC9250-4.3-1"], async () =>
        {
            using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
            using DoqReferenceServer server = Server(certificate, Answer, closeCode: 0x2);

            CurlRunResult result = await RunAsync(server, DoqReferenceServer.Port, "-k", Url);

            Assert.AreEqual(56, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.IsEmpty(result.StandardOutput);
            StringAssert.StartsWith(Encoding.UTF8.GetString(result.StandardError), "curl: (56) The DNS-over-QUIC server did not answer: ");
            Assert.ContainsSingle(server.Received);
        });

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC9250-5.1-1")]
    public Task Quic_UntrustedCertificate_IsRefusedWithExit60BeforeAnyQuery() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC9250-5.1-1"], async () =>
        {
            using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
            using DoqReferenceServer server = Server(certificate, Answer);

            CurlRunResult result = await RunAsync(server, DoqReferenceServer.Port, Url);

            Assert.AreEqual(60, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.IsEmpty(result.StandardOutput);
            StringAssert.StartsWith(Encoding.UTF8.GetString(result.StandardError), "curl: (60) ");
            Assert.IsEmpty(server.Received);
        });

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC9250-5.4-1")]
    public Task Quic_Query_IsPaddedWithTheEdnsPaddingOption() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC9250-5.4-1"], async () =>
        {
            using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
            using DoqReferenceServer server = Server(certificate, Answer);

            await RunAsync(server, DoqReferenceServer.Port, "-k", Url);

            // As under DoT: example.com.'s question ends at octet 29; an OPT record follows, and its first option at
            // octet 40 is Padding, option code 12 (RFC 7830).
            DnsReferenceQuery query = server.Received.Single().Query;
            Assert.AreEqual((ushort)1, query.Counts[3]);
            CollectionAssert.AreEqual(new byte[] { 0, 0, 41 }, query.Bytes[29..32]);
            CollectionAssert.AreEqual(new byte[] { 0, 12 }, query.Bytes[40..42]);
        });

    [TestMethod]
    [SpecRequirement("DnsQuery:RFC9250-4.1.1-2")]
    public Task Quic_ExplicitPort53_IsRefusedWithoutAConnection() =>
        KnownFailures.RunCaseAsync(["DnsQuery:RFC9250-4.1.1-2"], async () =>
        {
            using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
            using DoqReferenceServer server = Server(certificate, Answer);

            CurlRunResult result = await RunAsync(server, 53, "-k", "dns://localhost:53/example.com");

            Assert.AreNotEqual(0, result.ExitCode);
            Assert.IsEmpty(result.StandardOutput);
            Assert.IsFalse(server.IsHandshakeComplete);
        });

    internal static DoqReferenceServer Server(X509Certificate2 certificate, DnsReferenceResponder respond, ulong? closeCode = null) =>
        new(Http3ServerTlsSettings.FromRsaCertificate(certificate, []), respond, SystemTlsRandomSource.Instance, closeCode);

    private static IReadOnlyList<DnsReferenceReply> Answer(DnsReferenceQuery query, int index) =>
        [new DnsReferenceReply { Answers = [DnsReferenceRecord.A(Name, 300, "192.0.2.1")] }];

    private static Task<CurlRunResult> RunAsync(DoqReferenceServer server, int port, params string[] arguments) =>
        ExtendedConformanceHarness.RunAsync(InProcessCurl.RunAsync, [ReferenceRoute.Udp(port, server)], ["-sS", "-m", "10", "--dns-transport", "quic", .. arguments]);

    // curl writes the ID of the query it built, not the Message ID 0 the wire carried, so only the rest is pinned.
    private static void AssertAnswer(CurlRunResult result)
    {
        string standardOutput = Encoding.UTF8.GetString(result.StandardOutput);
        StringAssert.StartsWith(standardOutput, ";; ->>HEADER<<- opcode: QUERY, status: NOERROR, id: ");
        StringAssert.EndsWith(standardOutput, AnswerText);
        Assert.IsEmpty(result.StandardError);
    }

    private static void AssertFailed(CurlRunResult result, int exitCode, string message)
    {
        Assert.AreEqual(exitCode, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
        Assert.IsEmpty(result.StandardOutput);
        Assert.AreEqual($"curl: ({exitCode}) {message}\n".Replace("\n", Environment.NewLine, StringComparison.Ordinal), Encoding.UTF8.GetString(result.StandardError));
    }
}
