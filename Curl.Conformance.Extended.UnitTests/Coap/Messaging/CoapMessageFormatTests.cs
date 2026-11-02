using System.Text;
using Curl.Conformance.Extended.Coap;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.Coap.Messaging;

/// <summary>RFC 7252 section 3 (message format): the header curl writes and the malformed messages it must ignore, plus the reference codec's own checks.</summary>
[TestClass]
public sealed class CoapMessageFormatTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-3-1")]
    [SpecRequirement("Coap:RFC7252-3-3")]
    [SpecRequirement("Coap:RFC7252-4.6-1")]
    public Task Get_RequestHeader_IsVersion1ConfirmableGetWithTokenAndUriOptions() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-3-1", "Coap:RFC7252-3-3", "Coap:RFC7252-4.6-1"], async () =>
        {
            TimerJumpingClock clock = new();
            CoapReferenceServer server = CoapMessagingRun.Server(clock, (request, _) => [CoapResponses.Piggybacked(request, CoapResponses.Content, "hi"u8.ToArray())]);

            CurlRunResult result = await CoapMessagingRun.RunAsync(server, clock, TestContext.CancellationToken, CoapMessagingRun.Url);

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            CoapReceivedDatagram sent = server.Received.Single();
            CoapReferenceMessage request = sent.Message;
            Assert.AreEqual(1, request.Version);
            Assert.AreEqual(CoapReferenceMessageType.Confirmable, request.Type);
            Assert.AreEqual((byte)0x01, request.Code);
            Assert.IsTrue(request.Token.Length is >= 1 and <= 8, $"token length {request.Token.Length}");
            CollectionAssert.AreEqual(
                new[] { "3=localhost", "11=hello" },
                request.Options.Select(option => $"{option.Number}={Encoding.UTF8.GetString(option.Value)}").ToArray());
            Assert.IsEmpty(request.Payload);
            Assert.IsLessThanOrEqualTo(1152, sent.Bytes.Length);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-3.2-2")]
    public Task PiggybackedResponse_WithUintOptionsWithLeadingZeroBytes_IsWritten() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-3.2-2"], async () =>
        {
            TimerJumpingClock clock = new();
            CoapReferenceServer server = CoapMessagingRun.Server(clock, (request, _) =>
                [CoapResponses.Piggybacked(request, CoapResponses.Content, "zero"u8.ToArray(), new CoapReferenceOption(12, [0, 0]), new CoapReferenceOption(14, [0, 0, 0, 60]))]);

            CurlRunResult result = await CoapMessagingRun.RunAsync(server, clock, TestContext.CancellationToken, CoapMessagingRun.Url);

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("zero", Encoding.UTF8.GetString(result.StandardOutput));
            Assert.IsEmpty(result.StandardError);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-3-2")]
    [SpecRequirement("Coap:RFC7252-3-4")]
    [SpecRequirement("Coap:RFC7252-3-5")]
    [SpecRequirement("Coap:RFC7252-3.1-1")]
    [SpecRequirement("Coap:RFC7252-3.1-2")]
    [SpecRequirement("Coap:RFC7252-4.1-2")]
    [SpecRequirement("Coap:RFC7252-4.2-5")]
    [DataRow("version 2")]
    [DataRow("token length 9")]
    [DataRow("payload marker with no payload")]
    [DataRow("option delta 15")]
    [DataRow("option length 15")]
    [DataRow("empty message with bytes")]
    public Task MalformedAcknowledgement_IsSilentlyIgnoredAndTheRequestRetransmitted(string malformation) =>
        CoapMessagingRun.CaseAsync(
            ["Coap:RFC7252-3-2", "Coap:RFC7252-3-4", "Coap:RFC7252-3-5", "Coap:RFC7252-3.1-1", "Coap:RFC7252-3.1-2", "Coap:RFC7252-4.1-2", "Coap:RFC7252-4.2-5"],
            async () =>
            {
                TimerJumpingClock clock = new();
                CoapReferenceServer server = CoapMessagingRun.Server(clock, (request, index) =>
                    index == 0 ? [Malformed(malformation, request)] : [CoapResponses.Piggybacked(request, CoapResponses.Content, "ok"u8.ToArray())]);

                CurlRunResult result = await CoapMessagingRun.RunAsync(server, clock, TestContext.CancellationToken, CoapMessagingRun.Url);

                Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
                Assert.AreEqual("ok", Encoding.UTF8.GetString(result.StandardOutput));
                Assert.HasCount(2, server.Received, string.Join(" ", server.Received.Select(d => (d.ReceivedAt - server.Received[0].ReceivedAt).TotalSeconds + ":" + Convert.ToHexString(d.Bytes))));
                CollectionAssert.AreEqual(server.Received[0].Bytes, server.Received[1].Bytes);
            });

    [TestMethod]
    public void ReferenceCodec_RoundTripsExtendedOptionDeltasAndLengths()
    {
        CoapReferenceMessage message = new(1, CoapReferenceMessageType.NonConfirmable, 0x45, 0x1234, [1, 2], [new CoapReferenceOption(60, new byte[300]), new CoapReferenceOption(400, [7])], [9]);

        CoapReferenceMessage decoded = CoapReferenceMessage.Decode(message.Encode());

        Assert.AreEqual(CoapReferenceMessageType.NonConfirmable, decoded.Type);
        Assert.AreEqual((ushort)0x1234, decoded.MessageId);
        CollectionAssert.AreEqual(new[] { 60, 400 }, decoded.Options.Select(option => option.Number).ToArray());
        Assert.HasCount(300, decoded.Options[0].Value);
        CollectionAssert.AreEqual(new byte[] { 9 }, decoded.Payload);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x49, 0x45, 0, 1, 1, 2, 3, 4, 5, 6, 7, 8, 9 })]
    [DataRow(new byte[] { 0x60, 0x45, 0, 1, 0xFF })]
    [DataRow(new byte[] { 0x60, 0x45, 0, 1, 0x1F })]
    public void ReferenceCodec_MessageFormatError_Throws(byte[] datagram) =>
        Assert.ThrowsExactly<FormatException>(() => CoapReferenceMessage.Decode(datagram));

    private static byte[] Malformed(string malformation, CoapReferenceMessage request)
    {
        byte[] good = CoapResponses.Piggybacked(request, CoapResponses.Content, "bad"u8.ToArray());
        byte[] header = good[..(4 + request.Token.Length)];
        return malformation switch
        {
            "version 2" => [(byte)(good[0] ^ 0xC0), .. good[1..]],
            "token length 9" => [(byte)((good[0] & 0xF0) | 9), .. good[1..4], .. request.Token, .. new byte[9 - request.Token.Length], 0xFF, 1],
            "payload marker with no payload" => [.. header, 0xFF],
            "option delta 15" => [.. header, 0xF1, 0x00, 0xFF, 1],
            "option length 15" => [.. header, 0x1F, 0x00, 0xFF, 1],
            _ => [.. CoapResponses.EmptyAcknowledgement(request), 0x00],
        };
    }
}
