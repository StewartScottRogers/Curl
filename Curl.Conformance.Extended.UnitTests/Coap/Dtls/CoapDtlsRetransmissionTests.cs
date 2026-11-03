using System.Text;
using Curl.Conformance.Extended.Coap.Dtls;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.Coap.Messaging;

namespace Curl.Conformance.Extended.UnitTests.Coap.Dtls;

/// <summary>RFC 6347 section 4.2.4: a lost flight is sent again when the retransmission timer expires.</summary>
[TestClass]
public sealed class CoapDtlsRetransmissionTests
{
    [TestMethod]
    [SpecRequirement("Coap:RFC6347-4.2.4-1")]
    public Task HelloFlightLost_CurlSendsItsClientHelloAgainOnTheTimerAndCompletesTheHandshake() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC6347-4.2.4-1"], async () =>
        {
            using CoapDtlsFaultRun run = new(new DtlsReferenceFlightDropFault());

            // Once the ClientHello with the cookie is in and its flight lost, only curl's timer moves the handshake on.
            CurlRunResult result = await CoapDtlsRun.RunJumpingAsync(run.Server, () => run.Server.Received.Count == 2, "-k");

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("ok", Encoding.UTF8.GetString(result.StandardOutput));
            Assert.IsEmpty(result.StandardError);
            Assert.HasCount(3, run.Server.ClientHelloCookies);
            CollectionAssert.AreEqual(run.Server.Cookie, run.Server.ClientHelloCookies[2]);
            CollectionAssert.AreEqual(
                new[] { "22/0", "22/0", "22/0", "22/0", "20/0", "22/1", "23/1" },
                run.Server.Received.Select(record => $"{record.ContentType}/{record.Epoch}").ToArray());
            Assert.IsTrue(run.Server.ClientFinishedVerified);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC6347-4.1-3")]
    public Task HelloFlightLost_TheRetransmittedClientHelloGoesUnderANewRecordSequenceNumber() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC6347-4.1-3"], async () =>
        {
            using CoapDtlsFaultRun run = new(new DtlsReferenceFlightDropFault());

            await CoapDtlsRun.RunJumpingAsync(run.Server, () => run.Server.Received.Count == 2, "-k");

            Assert.IsGreaterThan(run.Server.Received[1].SequenceNumber, run.Server.Received[2].SequenceNumber);
        });
}
