using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Mqtt5;
using Curl.Conformance.Extended.Mqtt5.PublishSubscribe;

namespace Curl.Conformance.Extended.UnitTests.Mqtt5.PublishSubscribe;

/// <summary>
/// The SUBSCRIBE curl sends without <c>-d</c> and its SUBACK (OASIS MQTT Version 5.0 sections 3.8, 3.9, 4.7 and 4.8,
/// BL-4485): the reserved flags, one filter at QoS 0, wildcard and shared filters, and a SUBACK refusing it.
/// </summary>
[TestClass]
public sealed class SubscribeCases
{
    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-3.8.1-1")]
    [SpecRequirement("Mqtt5:MQTT-3.8.3-1")]
    [SpecRequirement("Mqtt5:MQTT-3.8.3-2")]
    [SpecRequirement("Mqtt5:MQTT-3.8.3-5")]
    [SpecRequirement("Mqtt5:MQTT-4.7.1-1")]
    [SpecRequirement("Mqtt5:MQTT-4.7.1-2")]
    [DataRow("t", "t")]
    [DataRow("a/+/c", "a/+/c")]
    [DataRow("a/%23", "a/#")]
    [DataRow("%23", "#")]
    public async Task Subscribe_SendsOneFilterAtQos0WithReservedBitsClear(string urlTopic, string filter)
    {
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await SubscribeToAsync(urlTopic, Mqtt5PublishSubscribeBytes.SubAck(1, 0x00), PublishSubscribeRun.NormalDisconnect);

        PublishSubscribeRun.AssertSucceeded(result, string.Empty);
        Mqtt5WirePacket subscribe = broker.ReceivedPackets[1];
        Assert.AreEqual(0x82, subscribe.FirstByte);
        byte[] filterBytes = System.Text.Encoding.UTF8.GetBytes(filter);
        CollectionAssert.AreEqual((byte[])[0x00, 0x01, 0x00, 0x00, (byte)filterBytes.Length, .. filterBytes, 0x00], subscribe.Body);
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT5-4.7.1-1")]
    [DataRow("a%23")]
    [DataRow("a/%23/c")]
    [DataRow("a/b%2B")]
    public async Task Subscribe_WildcardNotAloneInItsLevel_IsRefusedBeforeAnyPacketWithExit3(string urlTopic)
    {
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await SubscribeToAsync(urlTopic, Mqtt5PublishSubscribeBytes.SubAck(1, 0x00));

        Assert.AreEqual(3, result.ExitCode);
        StringAssert.StartsWith(System.Text.Encoding.UTF8.GetString(result.StandardError), "curl: (3) Invalid MQTT 5 topic: ");
        Assert.IsEmpty(broker.ReceivedPackets);
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-3.8.3-4")]
    [SpecRequirement("Mqtt5:MQTT-4.8.2-1")]
    [SpecRequirement("Mqtt5:MQTT-4.8.2-2")]
    public async Task Subscribe_SharedSubscription_IsSentWithNoLocalClear()
    {
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await SubscribeToAsync("$share/g/t", Mqtt5PublishSubscribeBytes.SubAck(1, 0x00), PublishSubscribeRun.NormalDisconnect);

        PublishSubscribeRun.AssertSucceeded(result, string.Empty);
        CollectionAssert.AreEqual((byte[])[0x00, 0x01, 0x00, 0x00, 0x0A, .. "$share/g/t"u8.ToArray(), 0x00], broker.ReceivedPackets[1].Body);
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT5-4.8.2-1")]
    [DataRow("$share//t")]
    [DataRow("$share/g")]
    [DataRow("$share/g%2B/t")]
    public async Task Subscribe_SharedSubscriptionWithoutAValidShareName_IsRefusedBeforeAnyPacketWithExit3(string urlTopic)
    {
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await SubscribeToAsync(urlTopic, Mqtt5PublishSubscribeBytes.SubAck(1, 0x00));

        Assert.AreEqual(3, result.ExitCode);
        Assert.IsEmpty(broker.ReceivedPackets);
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT5-3.9.3-1")]
    [DataRow((byte)0x80, "UnspecifiedError")]
    [DataRow((byte)0x83, "ImplementationSpecificError")]
    [DataRow((byte)0x87, "NotAuthorized")]
    [DataRow((byte)0x8F, "TopicFilterInvalid")]
    [DataRow((byte)0x91, "PacketIdentifierInUse")]
    [DataRow((byte)0x97, "QuotaExceeded")]
    [DataRow((byte)0x9E, "SharedSubscriptionsNotSupported")]
    [DataRow((byte)0xA1, "SubscriptionIdentifiersNotSupported")]
    [DataRow((byte)0xA2, "WildcardSubscriptionsNotSupported")]
    public async Task Subscribe_SubAckRefusesTheFilter_EndsWithExit8NamingTheReasonCode(byte reasonCode, string name)
    {
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await SubscribeToAsync("t", Mqtt5PublishSubscribeBytes.SubAck(1, reasonCode));

        PublishSubscribeRun.AssertFailed(result, 8, $"MQTT 5 SUBACK reason code 0x{reasonCode:X2} {name}");
        CollectionAssert.AreEqual(new[] { Mqtt5WirePacket.Connect, Mqtt5WirePacket.Subscribe }, PublishSubscribeRun.TypesOf(broker));
    }

    private static Task<(CurlRunResult Result, Mqtt5ReferenceBroker Broker)> SubscribeToAsync(string urlTopic, params byte[][] answer) =>
        PublishSubscribeRun.RunAsync(
            new Dictionary<int, Func<Mqtt5WirePacket, byte[]>> { [Mqtt5WirePacket.Subscribe] = _ => [.. answer.SelectMany(packet => packet)] },
            null,
            "-m",
            "3",
            $"mqtt5://localhost/{urlTopic}");
}
