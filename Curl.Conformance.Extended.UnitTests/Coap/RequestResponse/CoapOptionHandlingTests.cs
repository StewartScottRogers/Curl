using System.Text;
using Curl.Conformance.Extended.Coap;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.Coap.Messaging;

namespace Curl.Conformance.Extended.UnitTests.Coap.RequestResponse;

/// <summary>
/// RFC 7252 sections 5.2.2 and 5.4 (options in a response): an unrecognised critical option rejects the response,
/// unrecognised elective, supernumerary and out-of-range options are ignored, and section 3.1's extended delta and
/// length encodings in a response are read.
/// </summary>
[TestClass]
public sealed class CoapOptionHandlingTests
{
    private const ushort BadMessageId = 0xBEEF;

    private const ushort GoodMessageId = 0xBEF0;

    private static readonly CoapReferenceOption UnrecognisedCriticalOption = new(9, [1]);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-5.4.1-1")]
    public Task PiggybackedResponseWithUnrecognisedCriticalOption_IsIgnored() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-5.4.1-1"], async () =>
        {
            CurlRunResult result = await RunAsync(
                (request, index) => index == 0
                    ? [CoapResponses.Piggybacked(request, CoapResponses.Content, "bad"u8.ToArray(), UnrecognisedCriticalOption), CoapResponses.Piggybacked(request, CoapResponses.Content, "good"u8.ToArray())]
                    : [],
                out CoapReferenceServer server);

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("good", Encoding.UTF8.GetString(result.StandardOutput));
            Assert.IsEmpty(result.StandardError);
            Assert.HasCount(1, server.Received);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-5.4.1-1")]
    public Task ConfirmableResponseWithUnrecognisedCriticalOption_IsRejectedWithAReset() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-5.4.1-1"], async () =>
        {
            CurlRunResult result = await RunAsync(BadThenGoodSeparateResponse(CoapReferenceMessageType.Confirmable), out CoapReferenceServer server);

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("good", Encoding.UTF8.GetString(result.StandardOutput));
            Assert.IsEmpty(result.StandardError);
            Assert.HasCount(3, server.Received);
            CoapEmptyMessage.AssertIs(server.Received[1], CoapReferenceMessageType.Reset, BadMessageId);
            CoapEmptyMessage.AssertIs(server.Received[2], CoapReferenceMessageType.Acknowledgement, GoodMessageId);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-5.4.1-2")]
    public Task NonConfirmableResponseWithUnrecognisedCriticalOption_IsRejected() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-5.4.1-2"], async () =>
        {
            CurlRunResult result = await RunAsync(BadThenGoodSeparateResponse(CoapReferenceMessageType.NonConfirmable), out CoapReferenceServer server);

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("good", Encoding.UTF8.GetString(result.StandardOutput));
            Assert.IsEmpty(result.StandardError);
            Assert.IsLessThanOrEqualTo(2, server.Received.Count);
            foreach (CoapReceivedDatagram sent in server.Received.Skip(1))
            {
                CoapEmptyMessage.AssertIs(sent, CoapReferenceMessageType.Reset, BadMessageId);
            }
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-5.4.1-3")]
    [SpecRequirement("Coap:RFC7252-5.2.2-1")]
    [SpecRequirement("Coap:RFC7252-5.5-2")]
    [SpecRequirement("Coap:RFC7252-3.1-4")]
    [SpecRequirement("Coap:RFC7252-3.1-6")]
    public Task ConfirmableResponseWithUnrecognisedElectiveOptionsInExtendedEncodings_IsWrittenAndAcknowledged() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-5.4.1-3", "Coap:RFC7252-5.2.2-1", "Coap:RFC7252-5.5-2", "Coap:RFC7252-3.1-4", "Coap:RFC7252-3.1-6"], async () =>
        {
            byte[] longValue = [.. Enumerable.Repeat((byte)'v', 300)];
            byte[]? response = null;
            CurlRunResult result = await RunAsync(
                (request, index) =>
                {
                    if (index != 0)
                    {
                        return [];
                    }

                    response = new CoapReferenceMessage(
                        1,
                        CoapReferenceMessageType.Confirmable,
                        CoapResponses.Content,
                        GoodMessageId,
                        request.Token,
                        [new CoapReferenceOption(12, [50]), new CoapReferenceOption(48, [.. Enumerable.Repeat((byte)'e', 20)]), new CoapReferenceOption(2050, longValue)],
                        "{\"t\":1}"u8.ToArray()).Encode();
                    return [CoapResponses.EmptyAcknowledgement(request), response];
                },
                out CoapReferenceServer server);

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("{\"t\":1}", Encoding.UTF8.GetString(result.StandardOutput));
            Assert.IsEmpty(result.StandardError);
            Assert.HasCount(2, server.Received);
            CoapEmptyMessage.AssertIs(server.Received[1], CoapReferenceMessageType.Acknowledgement, GoodMessageId);
            int options = 4 + server.Received[0].Message.Token.Length;
            CollectionAssert.AreEqual(new byte[] { 0xC1, 50, 0xDD, 48 - 12 - 13, 20 - 13 }, response![options..(options + 5)]);
            CollectionAssert.AreEqual(new byte[] { 0xEE, (2050 - 48 - 269) >> 8, (2050 - 48 - 269) & 0xFF, 0, 300 - 269 }, response[(options + 25)..(options + 30)]);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-5.4.3-1")]
    [SpecRequirement("Coap:RFC7252-5.4.5-1")]
    public Task PiggybackedResponseWithSupernumeraryAndOutOfRangeElectiveOptions_IsWritten() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-5.4.3-1", "Coap:RFC7252-5.4.5-1"], async () =>
        {
            CurlRunResult result = await RunAsync(
                (request, _) =>
                [
                    CoapResponses.Piggybacked(request, CoapResponses.Content, "kept"u8.ToArray(), new CoapReferenceOption(12, [0]), new CoapReferenceOption(12, [50]), new CoapReferenceOption(14, [1, 2, 3, 4, 5])),
                ],
                out CoapReferenceServer server);

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("kept", Encoding.UTF8.GetString(result.StandardOutput));
            Assert.IsEmpty(result.StandardError);
            Assert.HasCount(1, server.Received);
        });

    private static CoapResponder BadThenGoodSeparateResponse(CoapReferenceMessageType type) =>
        (request, index) => index == 0
            ?
            [
                CoapResponses.EmptyAcknowledgement(request),
                new CoapReferenceMessage(1, type, CoapResponses.Content, BadMessageId, request.Token, [UnrecognisedCriticalOption], "bad"u8.ToArray()).Encode(),
                new CoapReferenceMessage(1, type, CoapResponses.Content, GoodMessageId, request.Token, [], "good"u8.ToArray()).Encode(),
            ]
            : [];

    private Task<CurlRunResult> RunAsync(CoapResponder respond, out CoapReferenceServer server)
    {
        TimerJumpingClock clock = new();
        server = CoapMessagingRun.Server(clock, respond);
        return CoapMessagingRun.RunAsync(server, clock, TestContext.CancellationToken, CoapMessagingRun.Url);
    }
}
