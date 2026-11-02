using System.Text;
using Curl.Conformance.Extended.Coap;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.Coap.Messaging;

/// <summary>RFC 7252 sections 4.2, 4.7 and 4.8: curl retransmits a confirmable request unanswered at ACK_TIMEOUT (2 s) times a factor up to ACK_RANDOM_FACTOR (1.5), doubling, and gives up with exit 28 after MAX_RETRANSMIT (4).</summary>
[TestClass]
public sealed class CoapRetransmissionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-4.2-7")]
    [SpecRequirement("Coap:RFC7252-4.2-8")]
    [SpecRequirement("Coap:RFC7252-4.4-1")]
    [SpecRequirement("Coap:RFC7252-4.7-1")]
    [SpecRequirement("Coap:RFC7252-4.8-1")]
    public Task SilentServer_RequestIsRetransmittedFourTimesAtDoublingTimeoutsThenExit28() =>
        CoapMessagingRun.CaseAsync(
            ["Coap:RFC7252-4.2-7", "Coap:RFC7252-4.2-8", "Coap:RFC7252-4.4-1", "Coap:RFC7252-4.7-1", "Coap:RFC7252-4.8-1"],
            async () =>
            {
                TimerJumpingClock clock = new();
                CoapReferenceServer server = CoapMessagingRun.Server(clock, CoapResponses.Silence);

                CurlRunResult result = await CoapMessagingRun.RunAsync(server, clock, TestContext.CancellationToken, CoapMessagingRun.Url);

                Assert.AreEqual(28, result.ExitCode);
                StringAssert.StartsWith(Encoding.UTF8.GetString(result.StandardError), "curl: (28) ");
                Assert.IsEmpty(result.StandardOutput);
                IReadOnlyList<CoapReceivedDatagram> sent = server.Received;
                Assert.HasCount(5, sent);
                foreach (CoapReceivedDatagram copy in sent)
                {
                    CollectionAssert.AreEqual(sent[0].Bytes, copy.Bytes);
                }

                TimeSpan initial = sent[1].ReceivedAt - sent[0].ReceivedAt;
                Assert.IsTrue(initial >= TimeSpan.FromSeconds(2) && initial <= TimeSpan.FromSeconds(3), $"initial timeout {initial}");
                for (int retransmission = 2; retransmission < sent.Count; retransmission++)
                {
                    Assert.IsLessThanOrEqualTo(20.0, Math.Abs(((initial * (1 << (retransmission - 1))) - (sent[retransmission].ReceivedAt - sent[retransmission - 1].ReceivedAt)).TotalMilliseconds), string.Join(" ", sent.Select(d => (d.ReceivedAt - sent[0].ReceivedAt).TotalSeconds)));
                }

                Assert.IsLessThanOrEqualTo(20.0, Math.Abs(((initial * 16) - (clock.GetUtcNow() - sent[4].ReceivedAt)).TotalMilliseconds));
            });
}
