using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Mqtt5;
using Curl.Conformance.Extended.Mqtt5.Connack;
using Curl.Conformance.Extended.Mqtt5.DisconnectAuth;

namespace Curl.Conformance.Extended.UnitTests.Mqtt5;

/// <summary>
/// Runs curl's <c>mqtt5://</c> against a reference broker that sends a Malformed Packet or breaks the protocol, and
/// checks curl's error handling against OASIS MQTT Version 5.0 section 4.13 (BL-4519): the transfer ends with exit 8,
/// and the client answers with a DISCONNECT carrying the reason code before it closes the connection.
/// </summary>
[TestClass]
public sealed class ErrorHandlingCases
{
    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    /// <summary>"(" after a UTF-8 lead byte: 0xC3 0x28 is not well-formed UTF-8 (section 1.5.4).</summary>
    private static readonly byte[] IllFormedUtf8 = [(byte)'o', (byte)'k', 0xC3, 0x28];

    [TestMethod]
    public async Task Connack_ReasonStringNotWellFormedUtf8_IsAMalformedPacketWithExit8AndNoPublish()
    {
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await DisconnectAuthCases.PublishAnsweredByAsync(IllFormedUtf8ReasonStringConnAck());

        DisconnectAuthCases.AssertFailedAsMalformed(result);
        StringAssert.Contains(System.Text.Encoding.UTF8.GetString(result.StandardError), "A UTF-8 Encoded String is not well-formed UTF-8");
        Assert.IsFalse(broker.ReceivedPackets.Any(packet => packet.PacketType == Mqtt5WirePacket.Publish));
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT5-4.13.1-1")]
    public Task Connack_ReasonStringNotWellFormedUtf8_IsAnsweredByDisconnect0x81() =>
        Known.RunCaseAsync(["Mqtt5:MQTT5-4.13.1-1"], async () =>
        {
            (CurlRunResult result, Mqtt5ReferenceBroker broker) = await DisconnectAuthCases.PublishAnsweredByAsync(IllFormedUtf8ReasonStringConnAck());

            DisconnectAuthCases.AssertFailedAsMalformed(result);
            DisconnectAuthCases.AssertEndedWithDisconnect(result, broker, 0x81);
        });

    [TestMethod]
    public async Task Subscribe_ServerDisconnectReasonStringNotWellFormedUtf8_IsAMalformedPacketWithExit8()
    {
        (CurlRunResult result, _) = await DisconnectAuthCases.SubscribeAsync(
            Mqtt5DisconnectAuthBytes.Disconnect(0, 0x00, Mqtt5DisconnectAuthBytes.RawStringProperty(Mqtt5DisconnectAuthBytes.ReasonString, IllFormedUtf8)));

        DisconnectAuthCases.AssertFailedAsMalformed(result);
    }

    [TestMethod]
    public async Task Subscribe_SecondConnAck_IsAWeirdServerReplyWithExit8()
    {
        (CurlRunResult result, _) = await DisconnectAuthCases.SubscribeAsync(Mqtt5ReferenceBroker.SuccessConnAck);

        DisconnectAuthCases.AssertFailed(result, 8, $"curl: (8) Weird server reply{Environment.NewLine}");
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT5-4.13.1-1")]
    public Task Subscribe_SecondConnAck_IsAProtocolErrorAnsweredByDisconnect0x82() =>
        Known.RunCaseAsync(["Mqtt5:MQTT5-4.13.1-1"], async () =>
        {
            (CurlRunResult result, Mqtt5ReferenceBroker broker) = await DisconnectAuthCases.SubscribeAsync(Mqtt5ReferenceBroker.SuccessConnAck);

            DisconnectAuthCases.AssertFailed(result, 8, $"curl: (8) Weird server reply{Environment.NewLine}");
            DisconnectAuthCases.AssertEndedWithDisconnect(result, broker, 0x82);
        });

    private static byte[] IllFormedUtf8ReasonStringConnAck() =>
        Mqtt5ConnAckBytes.Build(0, 0, Mqtt5DisconnectAuthBytes.RawStringProperty(Mqtt5DisconnectAuthBytes.ReasonString, IllFormedUtf8));
}
