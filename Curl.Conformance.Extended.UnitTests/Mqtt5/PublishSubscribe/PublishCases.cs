using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Mqtt5;
using Curl.Conformance.Extended.Mqtt5.PublishSubscribe;

namespace Curl.Conformance.Extended.UnitTests.Mqtt5.PublishSubscribe;

/// <summary>
/// The PUBLISH curl sends with <c>-d</c> (OASIS MQTT Version 5.0 sections 3.3, 4.3.1 and 4.7, BL-4485): QoS 0, no DUP,
/// no RETAIN, no properties, no wait for an acknowledgement, and a topic name with a wildcard refused.
/// </summary>
[TestClass]
public sealed class PublishCases
{
    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-3.3.1-1")]
    [SpecRequirement("Mqtt5:MQTT-3.3.1-2")]
    [SpecRequirement("Mqtt5:MQTT-4.3.1-1")]
    [SpecRequirement("Mqtt5:MQTT-3.3.2-1")]
    [SpecRequirement("Mqtt5:MQTT-3.3.2-8")]
    [SpecRequirement("Mqtt5:MQTT-3.3.2-9")]
    [SpecRequirement("Mqtt5:MQTT-3.3.4-6")]
    [SpecRequirement("Mqtt5:MQTT-3.3.4-7")]
    public async Task Publish_SendsQos0WithNoDupRetainOrPropertiesThenDisconnects()
    {
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishSubscribeRun.RunAsync(
            new Dictionary<int, Func<Mqtt5WirePacket, byte[]>>(), null, "-d", "hi", "mqtt5://localhost/a/b");

        PublishSubscribeRun.AssertSucceeded(result, string.Empty);
        CollectionAssert.AreEqual(new[] { Mqtt5WirePacket.Connect, Mqtt5WirePacket.Publish, Mqtt5WirePacket.Disconnect }, PublishSubscribeRun.TypesOf(broker));
        byte[] publish = Mqtt5PublishSubscribeBytes.Publish(0, 0, "a/b", "hi"u8.ToArray());
        CollectionAssert.AreEqual((byte[])[.. publish, .. PublishSubscribeRun.NormalDisconnect], result.Exchanges.Single().ClientBytes[^(publish.Length + 2)..]);
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT5-4.3.1-1")]
    [DataRow((byte)0x00)]
    [DataRow((byte)0x10)]
    [DataRow((byte)0x80)]
    [DataRow((byte)0x83)]
    [DataRow((byte)0x87)]
    [DataRow((byte)0x90)]
    [DataRow((byte)0x91)]
    [DataRow((byte)0x97)]
    [DataRow((byte)0x99)]
    public async Task Publish_Qos0AnsweredWithAPuback_DoesNotWaitForItAndExits0(byte reasonCode)
    {
        Dictionary<int, Func<Mqtt5WirePacket, byte[]>> responders = new()
        {
            [Mqtt5WirePacket.Publish] = _ => Mqtt5PublishSubscribeBytes.PubAck(1, reasonCode),
        };
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishSubscribeRun.RunAsync(responders, null, "-d", "hi", "mqtt5://localhost/t");

        PublishSubscribeRun.AssertSucceeded(result, string.Empty);
        CollectionAssert.AreEqual(new[] { Mqtt5WirePacket.Connect, Mqtt5WirePacket.Publish, Mqtt5WirePacket.Disconnect }, PublishSubscribeRun.TypesOf(broker));
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-3.3.2-2")]
    [SpecRequirement("Mqtt5:MQTT-4.7.0-1")]
    [DataRow("a/+", "a/+")]
    [DataRow("a/%23", "a/#")]
    [DataRow("%2B", "+")]
    public async Task Publish_WildcardInTheTopicName_IsRefusedBeforeAnyPacketWithExit3(string urlTopic, string topic)
    {
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishSubscribeRun.RunAsync(
            new Dictionary<int, Func<Mqtt5WirePacket, byte[]>>(), null, "-d", "hi", $"mqtt5://localhost/{urlTopic}");

        PublishSubscribeRun.AssertFailed(result, 3, $"Invalid MQTT 5 topic: \"{topic}\" is not a topic name: empty or holds a wildcard.");
        Assert.IsEmpty(broker.ReceivedPackets);
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-4.7.3-1")]
    public async Task Publish_EmptyTopic_IsRefusedBeforeAnyPacketWithExit3()
    {
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishSubscribeRun.RunAsync(
            new Dictionary<int, Func<Mqtt5WirePacket, byte[]>>(), null, "-d", "hi", "mqtt5://localhost/");

        Assert.AreEqual(3, result.ExitCode);
        Assert.IsEmpty(broker.ReceivedPackets);
    }
}
