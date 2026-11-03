using System.Text;
using Curl.Conformance.Extended.Coap.Dtls;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.Coap.Messaging;

namespace Curl.Conformance.Extended.UnitTests.Coap.Dtls;

/// <summary>RFC 6347 section 4.2.7 with RFC 5246 section 7.2: a fatal alert ends the handshake at once.</summary>
[TestClass]
public sealed class CoapDtlsAlertTests
{
    [TestMethod]
    [SpecRequirement("Coap:RFC6347-4.2.7-1")]
    public Task FatalAlertInPlaceOfTheHelloFlight_CurlStopsWithExit35AndSendsNothingMore() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC6347-4.2.7-1"], async () =>
        {
            using CoapDtlsFaultRun run = new(new DtlsReferenceAlertFault(40));

            // A client that ignored the alert would wait on its timer; jumping it lets such a client retransmit and fail the case.
            CurlRunResult result = await CoapDtlsRun.RunJumpingAsync(run.Server, () => run.Server.Received.Count >= 2, "-k");

            Assert.AreEqual(35, result.ExitCode);
            Assert.IsEmpty(result.StandardOutput);
            StringAssert.StartsWith(Encoding.UTF8.GetString(result.StandardError), "curl: (35) ");
            CollectionAssert.AreEqual(new byte[] { DtlsReferenceAlertFault.Fatal, 40 }, DtlsReferenceRecord.Split(result.Exchanges.Single().ServerSent[^1]).Single().Fragment);
            Assert.HasCount(2, run.Server.Received);
            Assert.IsEmpty(run.Coap.Received);
        });
}
