using System.Text;
using Curl.Conformance.Extended.Coap.Dtls;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.Coap.Messaging;

namespace Curl.Conformance.Extended.UnitTests.Coap.Dtls;

/// <summary>RFC 5246 section 7.4.9: the client verifies the server's Finished before it trusts the keys.</summary>
[TestClass]
public sealed class CoapDtlsFinishedTests
{
    [TestMethod]
    [SpecRequirement("Coap:RFC5246-7.4.9-1")]
    public Task ServerFinishedWithWrongVerifyData_CurlStopsWithExit35AndSendsNoRequest() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC5246-7.4.9-1"], async () =>
        {
            using CoapDtlsFaultRun run = new(new DtlsReferenceWrongFinishedFault());

            // A client still waiting after its Finished would wait on its timer; jumping it lets the run end either way.
            CurlRunResult result = await CoapDtlsRun.RunJumpingAsync(run.Server, () => run.Server.Received.Count >= 5, "-k");

            Assert.AreEqual(35, result.ExitCode);
            Assert.IsEmpty(result.StandardOutput);
            StringAssert.StartsWith(Encoding.UTF8.GetString(result.StandardError), "curl: (35) ");
            Assert.IsFalse(run.Server.Received.Any(record => record.ContentType == DtlsReferenceRecord.ApplicationData));
            Assert.IsEmpty(run.Coap.Received);
        });
}
