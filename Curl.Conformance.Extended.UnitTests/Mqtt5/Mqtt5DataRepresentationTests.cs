using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Mqtt5;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Mqtt5;

/// <summary>
/// Runs curl's <c>mqtt5://</c> transfers against the reference broker and checks the data representation of
/// OASIS MQTT Version 5.0 chapters 1.5 and 2 on the wire (BL-4517).
/// </summary>
[TestClass]
public sealed class Mqtt5DataRepresentationTests
{
    private static readonly InProcessCurlRunner Curl = InProcessCurl.RunAsync;

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-1.5.5-1")]
    [SpecRequirement("Mqtt5:MQTT-2.2.1-2")]
    public async Task Publish_LongPayloadAtQos0_FramesItWithAMinimalLengthAndNoPacketIdentifier()
    {
        string payload = new('x', 200);

        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await Mqtt5ConnectTests.PublishAsync([], payload);

        Mqtt5ConnectTests.AssertSucceededSilently(result);
        Mqtt5WirePacket publish = broker.ReceivedPackets[1];
        Assert.AreEqual(0x30, publish.FirstByte);
        CollectionAssert.AreEqual(Mqtt5WirePacket.Frame(0x30, [0, 1, (byte)'t', 0, .. System.Text.Encoding.ASCII.GetBytes(payload)]), Mqtt5WirePacket.Frame(publish.FirstByte, publish.Body));
        Assert.AreEqual(204, publish.Body.Length);
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-2.1.3-1")]
    [SpecRequirement("Mqtt5:MQTT-2.2.1-3")]
    public async Task Subscribe_WithoutData_SendsReservedFlagsAndANonZeroPacketIdentifier()
    {
        Mqtt5ReferenceBroker broker = new(responders: new Dictionary<int, Func<Mqtt5WirePacket, byte[]>>
        {
            [Mqtt5WirePacket.Subscribe] = subscribe =>
                [.. Mqtt5WirePacket.Frame(0x90, subscribe.Body[0], subscribe.Body[1], 0, 0), .. Mqtt5WirePacket.Frame(0x30, 0, 1, (byte)'t', 0, (byte)'m'), .. Mqtt5WirePacket.Frame(0xE0)],
        });

        CurlRunResult result = await ExtendedConformanceHarness.RunAsync(Curl, [ReferenceRoute.Tcp(1883, broker)], ["-s", "mqtt5://localhost/t"]);

        Assert.AreEqual(0, result.ExitCode, System.Text.Encoding.UTF8.GetString(result.StandardError));
        Assert.AreEqual("m", System.Text.Encoding.ASCII.GetString(result.StandardOutput));
        Mqtt5WirePacket subscribe = broker.ReceivedPackets[1];
        Assert.AreEqual(0x82, subscribe.FirstByte);
        Assert.AreNotEqual(0, (subscribe.Body[0] << 8) | subscribe.Body[1]);
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-1.5.4-2")]
    public async Task Publish_TopicWithNullCharacter_IsRefusedBeforeAnyPacket()
    {
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await Mqtt5ConnectTests.PublishAsync([], topic: "a%00b");

        Assert.AreEqual(3, result.ExitCode);
        Assert.IsEmpty(broker.ReceivedPackets);
    }
}
