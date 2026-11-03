using System.Text;
using Curl.Conformance.Extended.Coap.Dtls;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.Coap.Messaging;

namespace Curl.Conformance.Extended.UnitTests.Coap.Dtls;

/// <summary>RFC 6347 sections 4.2.2 and 4.2.3: a server flight fragmented across records and sent out of order.</summary>
[TestClass]
public sealed class CoapDtlsFragmentationTests
{
    [TestMethod]
    [SpecRequirement("Coap:RFC6347-4.2.3-1")]
    [SpecRequirement("Coap:RFC6347-4.2.2-1")]
    public Task HelloFlightFragmentedAndReversed_CurlReassemblesItAndCompletesTheHandshake() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC6347-4.2.3-1", "Coap:RFC6347-4.2.2-1"], async () =>
        {
            using CoapDtlsFaultRun run = new(new DtlsReferenceFragmentingFault(100));

            CurlRunResult result = await CoapDtlsRun.RunAsync(run.Server, "-k");

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("ok", Encoding.UTF8.GetString(result.StandardOutput));
            Assert.IsEmpty(result.StandardError);
            IReadOnlyList<byte[]> sent = result.Exchanges.Single().ServerSent;
            Assert.IsGreaterThan(8, sent.Count);
            // The hello flight's first datagram is its last fragment: ServerHelloDone, message_seq 4, whole and empty.
            CollectionAssert.AreEqual(new byte[] { 14, 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0 }, DtlsReferenceRecord.Split(sent[1]).Single().Fragment);
            Assert.HasCount(2, run.Server.ClientHelloCookies);
            CollectionAssert.AreEqual(
                new[] { "22/0", "22/0", "22/0", "20/0", "22/1", "23/1" },
                run.Server.Received.Select(record => $"{record.ContentType}/{record.Epoch}").ToArray());
            Assert.IsTrue(run.Server.ClientFinishedVerified);
        });
}
