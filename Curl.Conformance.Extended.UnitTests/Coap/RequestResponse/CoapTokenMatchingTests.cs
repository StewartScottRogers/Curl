using System.Text;
using Curl.Conformance.Extended.Coap;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.Coap.Messaging;

namespace Curl.Conformance.Extended.UnitTests.Coap.RequestResponse;

/// <summary>RFC 7252 section 5.3 (request/response matching): the tokens curl generates and the responses whose token does not match.</summary>
[TestClass]
public sealed class CoapTokenMatchingTests
{
    private const ushort WrongMessageId = 0xBEEF;

    private const ushort RightMessageId = 0xBEF0;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-5.3.1-1")]
    [SpecRequirement("Coap:RFC7252-5.3.1-2")]
    public Task Tokens_HoldAtLeast32BitsAndDifferBetweenRequests() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-5.3.1-1", "Coap:RFC7252-5.3.1-2"], async () =>
        {
            byte[] first = await TokenOfARunAsync();
            byte[] second = await TokenOfARunAsync();

            Assert.IsGreaterThanOrEqualTo(4, first.Length);
            Assert.IsGreaterThanOrEqualTo(4, second.Length);
            CollectionAssert.AreNotEqual(first, second);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-5.3.2-1")]
    public Task PiggybackedResponseWithAMismatchedToken_IsIgnored() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-5.3.2-1"], async () =>
        {
            CurlRunResult result = await RunAsync(
                (request, _) =>
                [
                    new CoapReferenceMessage(1, CoapReferenceMessageType.Acknowledgement, CoapResponses.Content, request.MessageId, OtherToken(request), [], "wrong"u8.ToArray()).Encode(),
                    CoapResponses.Piggybacked(request, CoapResponses.Content, "right"u8.ToArray()),
                ],
                out CoapReferenceServer server);

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("right", Encoding.UTF8.GetString(result.StandardOutput));
            Assert.IsEmpty(result.StandardError);
            Assert.HasCount(1, server.Received);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-5.3.2-2")]
    public Task SeparateResponseWithAMismatchedToken_IsRejectedWithAReset() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-5.3.2-2"], async () =>
        {
            CurlRunResult result = await RunAsync(
                (request, index) => index == 0
                    ?
                    [
                        CoapResponses.EmptyAcknowledgement(request),
                        new CoapReferenceMessage(1, CoapReferenceMessageType.Confirmable, CoapResponses.Content, WrongMessageId, OtherToken(request), [], "wrong"u8.ToArray()).Encode(),
                        CoapResponses.Separate(request, CoapReferenceMessageType.Confirmable, RightMessageId, CoapResponses.Content, "right"u8.ToArray()),
                    ]
                    : [],
                out CoapReferenceServer server);

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("right", Encoding.UTF8.GetString(result.StandardOutput));
            Assert.IsEmpty(result.StandardError);
            Assert.HasCount(3, server.Received);
            CoapEmptyMessage.AssertIs(server.Received[1], CoapReferenceMessageType.Reset, WrongMessageId);
            CoapEmptyMessage.AssertIs(server.Received[2], CoapReferenceMessageType.Acknowledgement, RightMessageId);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-5.3.2-3")]
    public Task ResponseFromAnotherEndpoint_IsIgnored() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-5.3.2-3"], async () =>
        {
            CurlRunResult result = await RunAsync(
                (request, _) =>
                [
                    CoapResponses.FromAnotherEndpoint(CoapResponses.Piggybacked(request, CoapResponses.Content, "spoofed"u8.ToArray())),
                    CoapResponses.Piggybacked(request, CoapResponses.Content, "genuine"u8.ToArray()),
                ],
                out CoapReferenceServer server);

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("genuine", Encoding.UTF8.GetString(result.StandardOutput));
            Assert.HasCount(1, server.Received);
        });

    private static byte[] OtherToken(CoapReferenceMessage request) => [.. request.Token.Select(octet => (byte)(octet ^ 0xFF))];

    private async Task<byte[]> TokenOfARunAsync()
    {
        CurlRunResult result = await RunAsync((request, _) => [CoapResponses.Piggybacked(request, CoapResponses.Content, [])], out CoapReferenceServer server);
        Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
        return server.Received.Single().Message.Token;
    }

    private Task<CurlRunResult> RunAsync(CoapResponder respond, out CoapReferenceServer server)
    {
        TimerJumpingClock clock = new();
        server = CoapMessagingRun.Server(clock, respond);
        return CoapMessagingRun.RunAsync(server, clock, TestContext.CancellationToken, CoapMessagingRun.Url);
    }
}
