using System.Text;
using Curl.Conformance.Extended.Coap.Dtls;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.Coap.Messaging;

namespace Curl.Conformance.Extended.UnitTests.Coap.Dtls;

/// <summary>RFC 5246 section 7.4.3: the client verifies the signature over the server's ECDHE parameters.</summary>
[TestClass]
public sealed class CoapDtlsServerKeyExchangeTests
{
    [TestMethod]
    [SpecRequirement("Coap:RFC5246-7.4.3-1")]
    public Task ServerKeyExchangeWithWrongSignature_CurlStopsWithExit35BeforeItsClientKeyExchange() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC5246-7.4.3-1"], async () =>
        {
            using CoapDtlsFaultRun run = new(new DtlsReferenceWrongSignatureFault());

            // A client still waiting after the hello flight would wait on its timer; jumping it lets the run end either way.
            CurlRunResult result = await CoapDtlsRun.RunJumpingAsync(run.Server, () => run.Server.Received.Count >= 2, "-k");

            Assert.AreEqual(35, result.ExitCode);
            Assert.IsEmpty(result.StandardOutput);
            StringAssert.StartsWith(Encoding.UTF8.GetString(result.StandardError), "curl: (35) ");
            Assert.IsFalse(run.Server.Received.Skip(2).Any(record => record.ContentType == DtlsReferenceRecord.Handshake));
            Assert.IsEmpty(run.Coap.Received);
        });
}
