using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.Coap;
using Curl.Conformance.Extended.Coap.Dtls;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.Coap.Messaging;

namespace Curl.Conformance.Extended.UnitTests.Coap.Dtls;

/// <summary>RFC 6347 sections 4.1 and 4.2.1 and RFC 7252 section 9.1: the cookie exchange, the full handshake and the certificate check of <c>coaps://</c>.</summary>
[TestClass]
public sealed class CoapDtlsHandshakeTests
{
    [TestMethod]
    [SpecRequirement("Coap:RFC6347-4.2.1-1")]
    [SpecRequirement("Coap:RFC6347-4.1-1")]
    [SpecRequirement("Coap:RFC7252-9.1-1")]
    public Task FullHandshake_TheSecondClientHelloEchoesTheCookieAndFinishedIsTheFirstRecordOfEpoch1() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC6347-4.2.1-1", "Coap:RFC6347-4.1-1", "Coap:RFC7252-9.1-1"], async () =>
        {
            using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
            using DtlsReferenceServer server = new(CoapDtlsRun.CoapServer((request, _) => [CoapResponses.Piggybacked(request, 0x45, "ok"u8.ToArray())]), certificate);

            CurlRunResult result = await CoapDtlsRun.RunAsync(server, "-k");

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual(CoapDtlsRun.Port, result.Exchanges.Single().Port);
            Assert.AreEqual(2, server.ClientHelloCookies.Count);
            Assert.IsEmpty(server.ClientHelloCookies[0]);
            CollectionAssert.AreEqual(server.Cookie, server.ClientHelloCookies[1]);
            CollectionAssert.AreEqual(
                new[] { "22/0", "22/0", "22/0", "20/0", "22/1", "23/1" },
                server.Received.Select(record => $"{record.ContentType}/{record.Epoch}").ToArray());
            Assert.AreEqual(0UL, server.Received[4].SequenceNumber);
            Assert.IsTrue(server.ClientFinishedVerified);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-9.1-2")]
    public Task UntrustedCertificate_FailsWithExit60BeforeAnyKeyExchange() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-9.1-2"], async () =>
        {
            using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
            CoapReferenceServer coap = CoapDtlsRun.CoapServer((request, _) => [CoapResponses.Piggybacked(request, 0x45, "ok"u8.ToArray())]);
            using DtlsReferenceServer server = new(coap, certificate);

            CurlRunResult result = await CoapDtlsRun.RunAsync(server);

            Assert.AreEqual(60, result.ExitCode);
            Assert.IsEmpty(result.StandardOutput);
            StringAssert.StartsWith(Encoding.UTF8.GetString(result.StandardError), "curl: (60) ");
            Assert.AreEqual(2, server.Received.Count);
            Assert.IsEmpty(coap.Received);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-9.1-3")]
    public Task UntrustedCertificateUnderInsecure_TheHandshakeCompletesAndTheResponseIsWritten() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-9.1-3"], async () =>
        {
            using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
            using DtlsReferenceServer server = new(CoapDtlsRun.CoapServer((request, _) => [CoapResponses.Piggybacked(request, 0x45, "ok"u8.ToArray())]), certificate);

            CurlRunResult result = await CoapDtlsRun.RunAsync(server, "--insecure");

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("ok", Encoding.UTF8.GetString(result.StandardOutput));
            Assert.IsEmpty(result.StandardError);
        });
}
