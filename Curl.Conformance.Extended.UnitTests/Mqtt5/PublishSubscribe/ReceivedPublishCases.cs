using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Mqtt5;
using Curl.Conformance.Extended.Mqtt5.PublishSubscribe;

namespace Curl.Conformance.Extended.UnitTests.Mqtt5.PublishSubscribe;

/// <summary>
/// The PUBLISH packets a subscribing curl receives (OASIS MQTT Version 5.0 sections 3.3, 4.3, 4.6 and 4.7, BL-4485):
/// each QoS acknowledged in order, the PUBLISH properties, a Topic Alias the client never allowed, a wildcard topic
/// name, the malformed QoS 3 and a message larger than any limit curl announces.
/// </summary>
[TestClass]
public sealed class ReceivedPublishCases
{
    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-3.3.4-1")]
    [SpecRequirement("Mqtt5:MQTT-4.5.0-2")]
    public async Task Received_Qos0Messages_AreWrittenInOrderAndNotAcknowledged()
    {
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishSubscribeRun.SubscribeAsync(
            null,
            Mqtt5PublishSubscribeBytes.Publish(0, 0, "t", "one,"u8.ToArray()),
            Mqtt5PublishSubscribeBytes.Publish(0, 0, "t", "two"u8.ToArray()),
            PublishSubscribeRun.NormalDisconnect);

        PublishSubscribeRun.AssertSucceeded(result, "one,two");
        CollectionAssert.AreEqual(new[] { Mqtt5WirePacket.Connect, Mqtt5WirePacket.Subscribe }, PublishSubscribeRun.TypesOf(broker));
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-4.3.2-4")]
    [SpecRequirement("Mqtt5:MQTT-4.6.0-2")]
    public Task Received_Qos1Messages_AreEachAcknowledgedWithAPubackInOrder() =>
        PublishSubscribeRun.Known.RunCaseAsync(["Mqtt5:MQTT-4.3.2-4", "Mqtt5:MQTT-4.6.0-2"], async () =>
        {
            int acknowledged = 0;
            Dictionary<int, Func<Mqtt5WirePacket, byte[]>> responders = new()
            {
                [PublishSubscribeRun.PubAck] = _ => ++acknowledged == 2 ? PublishSubscribeRun.NormalDisconnect : [],
            };
            (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishSubscribeRun.SubscribeAsync(
                responders,
                Mqtt5PublishSubscribeBytes.Publish(1, 7, "t", "one,"u8.ToArray()),
                Mqtt5PublishSubscribeBytes.Publish(1, 3, "t", "two"u8.ToArray()));

            PublishSubscribeRun.AssertSucceeded(result, "one,two");
            byte[][] frames = PublishSubscribeRun.FramesAfterSubscribe(broker);
            Assert.HasCount(2, frames);
            CollectionAssert.AreEqual(new byte[] { 0x40, 0x02, 0x00, 0x07 }, frames[0]);
            CollectionAssert.AreEqual(new byte[] { 0x40, 0x02, 0x00, 0x03 }, frames[1]);
        });

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-4.3.3-8")]
    [SpecRequirement("Mqtt5:MQTT-4.3.3-11")]
    [SpecRequirement("Mqtt5:MQTT-4.6.0-3")]
    public Task Received_Qos2Message_IsAcknowledgedWithPubrecThenPubcomp() =>
        PublishSubscribeRun.Known.RunCaseAsync(["Mqtt5:MQTT-4.3.3-8", "Mqtt5:MQTT-4.3.3-11", "Mqtt5:MQTT-4.6.0-3"], async () =>
        {
            Dictionary<int, Func<Mqtt5WirePacket, byte[]>> responders = new()
            {
                [PublishSubscribeRun.PubRec] = _ => Mqtt5PublishSubscribeBytes.PubRel(9),
                [PublishSubscribeRun.PubComp] = _ => PublishSubscribeRun.NormalDisconnect,
            };
            (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishSubscribeRun.SubscribeAsync(
                responders, Mqtt5PublishSubscribeBytes.Publish(2, 9, "t", "once"u8.ToArray()));

            PublishSubscribeRun.AssertSucceeded(result, "once");
            byte[][] frames = PublishSubscribeRun.FramesAfterSubscribe(broker);
            Assert.HasCount(2, frames);
            CollectionAssert.AreEqual(new byte[] { 0x50, 0x02, 0x00, 0x09 }, frames[0]);
            CollectionAssert.AreEqual(new byte[] { 0x70, 0x02, 0x00, 0x09 }, frames[1]);
        });

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT5-3.3.2.3.4-1")]
    public Task Received_TopicAliasSetAndReused_IsRefusedWithDisconnect0x94() =>
        PublishSubscribeRun.Known.RunCaseAsync(["Mqtt5:MQTT5-3.3.2.3.4-1"], async () =>
        {
            byte[] alias = [Mqtt5PublishSubscribeBytes.TopicAlias];
            (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishSubscribeRun.SubscribeAsync(
                null,
                Mqtt5PublishSubscribeBytes.Publish(0, 0, "t", "set,"u8.ToArray(), [.. alias, 0x00, 0x01]),
                Mqtt5PublishSubscribeBytes.Publish(0, 0, string.Empty, "reused"u8.ToArray(), [.. alias, 0x00, 0x01]),
                PublishSubscribeRun.NormalDisconnect);

            Assert.AreEqual(8, result.ExitCode, System.Text.Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual(0, result.StandardOutput.Length);
            PublishSubscribeRun.AssertEndedWithDisconnect(broker, 0x94);
        });

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT5-3.3.2.1-1")]
    [DataRow("a/+")]
    [DataRow("a/#")]
    public async Task Received_WildcardInTheTopicName_IsAMalformedPacketWithExit8(string topic)
    {
        (CurlRunResult result, _) = await PublishSubscribeRun.SubscribeAsync(
            null, Mqtt5PublishSubscribeBytes.Publish(0, 0, topic, "x"u8.ToArray()), PublishSubscribeRun.NormalDisconnect);

        PublishSubscribeRun.AssertFailed(
            result, 8, $"Weird server reply: malformed MQTT 5 packet: \"{topic}\" is not a topic name: empty or holds a wildcard.");
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-3.3.1-4")]
    public async Task Received_BothQosBitsSet_IsAMalformedPacketWithExit8()
    {
        (CurlRunResult result, _) = await PublishSubscribeRun.SubscribeAsync(
            null, Mqtt5PublishSubscribeBytes.Publish(3, 1, "t", "x"u8.ToArray()), PublishSubscribeRun.NormalDisconnect);

        PublishSubscribeRun.AssertFailed(result, 8, "Weird server reply: malformed MQTT 5 packet: PUBLISH has QoS 3.");
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT5-3.3.2.3-1")]
    public async Task Received_PublishWithEveryProperty_WritesThePayload()
    {
        byte[] publish = Mqtt5PublishSubscribeBytes.Publish(
            0,
            0,
            "t",
            "body"u8.ToArray(),
            [Mqtt5PublishSubscribeBytes.PayloadFormatIndicator, 0x01],
            [Mqtt5PublishSubscribeBytes.MessageExpiryInterval, 0x00, 0x00, 0x00, 0x3C],
            Mqtt5PublishSubscribeBytes.StringsProperty(Mqtt5PublishSubscribeBytes.ContentType, "text/plain"),
            Mqtt5PublishSubscribeBytes.StringsProperty(Mqtt5PublishSubscribeBytes.ResponseTopic, "reply/to"),
            Mqtt5PublishSubscribeBytes.StringsProperty(Mqtt5PublishSubscribeBytes.CorrelationData, "id-1"),
            Mqtt5PublishSubscribeBytes.StringsProperty(Mqtt5PublishSubscribeBytes.UserProperty, "k", "v"),
            [Mqtt5PublishSubscribeBytes.SubscriptionIdentifier, 0x05]);
        (CurlRunResult result, _) = await PublishSubscribeRun.SubscribeAsync(null, publish, PublishSubscribeRun.NormalDisconnect);

        PublishSubscribeRun.AssertSucceeded(result, "body");
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT5-3.1.2.11.4-1")]
    public async Task Received_MessageLargerThan64KiB_IsWrittenWholeSinceCurlAnnouncesNoMaximumPacketSize()
    {
        string payload = new('m', 70_000);
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishSubscribeRun.SubscribeAsync(
            null, Mqtt5PublishSubscribeBytes.Publish(0, 0, "t", System.Text.Encoding.ASCII.GetBytes(payload)), PublishSubscribeRun.NormalDisconnect);

        PublishSubscribeRun.AssertSucceeded(result, payload);
        const int maximumPacketSizeProperty = 0x27;
        CollectionAssert.DoesNotContain(broker.Connect!.PropertyIdentifiers.ToList(), maximumPacketSizeProperty);
    }
}
