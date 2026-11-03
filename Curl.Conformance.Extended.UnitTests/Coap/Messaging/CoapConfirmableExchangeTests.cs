using System.Text;
using Curl.Conformance.Extended.Coap;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.Coap.Messaging;

/// <summary>RFC 7252 section 4 (messaging): piggybacked, separate, Non-confirmable, duplicate and Reset answers to curl's confirmable GET.</summary>
[TestClass]
public sealed class CoapConfirmableExchangeTests
{
    private const ushort ServerMessageId = 0xBEEF;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-4.2-1")]
    public Task PiggybackedResponse_IsWrittenAndNothingMoreIsSent() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-4.2-1"], async () =>
        {
            CurlRunResult result = await RunAsync((request, _) => [CoapResponses.Piggybacked(request, CoapResponses.Content, "hello"u8.ToArray())], out CoapReferenceServer server);

            Assert.AreEqual(0, result.ExitCode);
            Assert.AreEqual("hello", Encoding.UTF8.GetString(result.StandardOutput));
            Assert.IsEmpty(result.StandardError);
            Assert.HasCount(1, server.Received);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-4.1-1")]
    [SpecRequirement("Coap:RFC7252-4.2-2")]
    public Task SeparateConfirmableResponse_IsWrittenAndAcknowledgedWithAnEmptyAcknowledgement() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-4.1-1", "Coap:RFC7252-4.2-2"], async () =>
        {
            CurlRunResult result = await RunAsync(
                (request, index) => index == 0
                    ? [CoapResponses.EmptyAcknowledgement(request), CoapResponses.Separate(request, CoapReferenceMessageType.Confirmable, ServerMessageId, CoapResponses.Content, "later"u8.ToArray())]
                    : [],
                out CoapReferenceServer server);

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("later", Encoding.UTF8.GetString(result.StandardOutput));
            Assert.HasCount(2, server.Received);
            AssertEmpty(server.Received[1], CoapReferenceMessageType.Acknowledgement, ServerMessageId);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-4.3-1")]
    public Task SeparateNonConfirmableResponse_IsWrittenAndNotAcknowledged() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-4.3-1"], async () =>
        {
            CurlRunResult result = await RunAsync(
                (request, _) => [CoapResponses.EmptyAcknowledgement(request), CoapResponses.Separate(request, CoapReferenceMessageType.NonConfirmable, ServerMessageId, CoapResponses.Content, "non"u8.ToArray())],
                out CoapReferenceServer server);

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("non", Encoding.UTF8.GetString(result.StandardOutput));
            Assert.HasCount(1, server.Received);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-4.5-2")]
    public Task DuplicatePiggybackedResponse_IsWrittenOnce() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-4.5-2"], async () =>
        {
            CurlRunResult result = await RunAsync(
                (request, _) =>
                {
                    byte[] response = CoapResponses.Piggybacked(request, CoapResponses.Content, "once"u8.ToArray());
                    return [response, response];
                },
                out CoapReferenceServer server);

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("once", Encoding.UTF8.GetString(result.StandardOutput));
            Assert.HasCount(1, server.Received);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-4.2-3")]
    [SpecRequirement("Coap:RFC7252-4.2-4")]
    public Task UnrelatedConfirmableMessage_IsRejectedWithAnEmptyReset() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-4.2-3", "Coap:RFC7252-4.2-4"], async () =>
        {
            CurlRunResult result = await RunAsync(
                (request, index) => index == 0
                    ? [
                        new CoapReferenceMessage(1, CoapReferenceMessageType.Confirmable, CoapResponses.Content, ServerMessageId, [0xEE], [], "other"u8.ToArray()).Encode(),
                        CoapResponses.Piggybacked(request, CoapResponses.Content, "mine"u8.ToArray()),
                    ]
                    : [],
                out CoapReferenceServer server);

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("mine", Encoding.UTF8.GetString(result.StandardOutput));
            Assert.HasCount(2, server.Received);
            AssertEmpty(server.Received[1], CoapReferenceMessageType.Reset, ServerMessageId);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-4.2-6")]
    public Task Reset_FailsTheTransferWithExit56() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-4.2-6"], async () =>
        {
            CurlRunResult result = await RunAsync((request, _) => [CoapResponses.Reset(request)], out CoapReferenceServer server);

            Assert.AreEqual(56, result.ExitCode);
            StringAssert.StartsWith(Encoding.UTF8.GetString(result.StandardError), "curl: (56) ");
            Assert.IsEmpty(result.StandardOutput);
            Assert.HasCount(1, server.Received);
        });

    private static void AssertEmpty(CoapReceivedDatagram sent, CoapReferenceMessageType type, ushort messageId)
    {
        Assert.HasCount(4, sent.Bytes);
        Assert.AreEqual(1, sent.Message.Version);
        Assert.AreEqual(type, sent.Message.Type);
        Assert.AreEqual((byte)0, sent.Message.Code);
        Assert.AreEqual(messageId, sent.Message.MessageId);
        Assert.IsEmpty(sent.Message.Token);
    }

    private Task<CurlRunResult> RunAsync(CoapResponder respond, out CoapReferenceServer server)
    {
        TimerJumpingClock clock = new();
        server = CoapMessagingRun.Server(clock, respond);
        return CoapMessagingRun.RunAsync(server, clock, TestContext.CancellationToken, CoapMessagingRun.Url);
    }
}
