using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Mqtt5;
using Curl.Conformance.Extended.Mqtt5.Connack;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Mqtt5;

/// <summary>
/// Runs curl's <c>mqtt5://</c> against a reference broker answering CONNECT with a chosen CONNACK and checks the
/// client against OASIS MQTT Version 5.0 section 3.2 (BL-4518): each failure Connect Reason Code, the malformed
/// and Session Present cases, and the CONNACK properties a client must honour.
/// </summary>
[TestClass]
public sealed class ConnackCases
{
    private const int Port = 1883;

    private static readonly InProcessCurlRunner Curl = InProcessCurl.RunAsync;

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT5-3.2.2.2-1")]
    [DataRow((byte)0x80, "UnspecifiedError")]
    [DataRow((byte)0x81, "MalformedPacket")]
    [DataRow((byte)0x82, "ProtocolError")]
    [DataRow((byte)0x83, "ImplementationSpecificError")]
    [DataRow((byte)0x84, "UnsupportedProtocolVersion")]
    [DataRow((byte)0x85, "ClientIdentifierNotValid")]
    [DataRow((byte)0x86, "BadUserNameOrPassword")]
    [DataRow((byte)0x87, "NotAuthorized")]
    [DataRow((byte)0x88, "ServerUnavailable")]
    [DataRow((byte)0x89, "ServerBusy")]
    [DataRow((byte)0x8A, "Banned")]
    [DataRow((byte)0x8C, "BadAuthenticationMethod")]
    [DataRow((byte)0x90, "TopicNameInvalid")]
    [DataRow((byte)0x95, "PacketTooLarge")]
    [DataRow((byte)0x97, "QuotaExceeded")]
    [DataRow((byte)0x99, "PayloadFormatInvalid")]
    [DataRow((byte)0x9A, "RetainNotSupported")]
    [DataRow((byte)0x9B, "QosNotSupported")]
    [DataRow((byte)0x9C, "UseAnotherServer")]
    [DataRow((byte)0x9D, "ServerMoved")]
    [DataRow((byte)0x9F, "ConnectionRateExceeded")]
    public async Task Connack_FailureReasonCode_EndsTheTransferAfterConnectWithExit8(byte reasonCode, string name)
    {
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishAsync(Mqtt5ConnAckBytes.Build(0, reasonCode));

        AssertFailed(result, 8, $"MQTT 5 CONNACK reason code 0x{reasonCode:X2} {name}");
        AssertSentOnlyConnect(result, broker);
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-3.2.2-1")]
    public async Task Connack_ReservedAcknowledgeFlagSet_IsAMalformedPacketWithExit8()
    {
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishAsync(Mqtt5ConnAckBytes.Build(0x02, 0));

        AssertFailed(result, 8, "Weird server reply: malformed MQTT 5 packet: CONNACK's reserved acknowledge flags are set.");
        AssertSentConnectThenDisconnect(result, broker, 0x81);
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT5-3.2.2.3-1")]
    public async Task Connack_PropertyTwice_IsRefusedWithExit8()
    {
        byte[] keepAlive = Mqtt5ConnAckBytes.TwoByteProperty(Mqtt5ConnAckBytes.ServerKeepAlive, 30);
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishAsync(Mqtt5ConnAckBytes.Build(0, 0, keepAlive, keepAlive));

        Assert.AreEqual(8, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
        Assert.AreEqual(0, result.StandardOutput.Length);
        StringAssert.StartsWith(Encoding.UTF8.GetString(result.StandardError), "curl: (8) Weird server reply: malformed MQTT 5 packet: ");
        AssertSentConnectThenDisconnect(result, broker, 0x82);
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-3.2.2-4")]
    public Task Connack_SessionPresentAfterCleanStart_ClosesTheConnectionWithExit8() =>
        Known.RunCaseAsync(["Mqtt5:MQTT-3.2.2-4"], async () =>
        {
            (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishAsync(Mqtt5ConnAckBytes.Build(0x01, 0));

            AssertFailed(result, 8, "MQTT 5 CONNACK Session Present on a Clean Start");
            AssertSentOnlyConnect(result, broker);
        });

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-3.2.2-15")]
    public Task Connack_MaximumPacketSizeBelowThePublish_SendsNoPublishAndExits55() =>
        Known.RunCaseAsync(["Mqtt5:MQTT-3.2.2-15"], async () =>
        {
            byte[] maximum = Mqtt5ConnAckBytes.FourByteProperty(Mqtt5ConnAckBytes.MaximumPacketSize, 20);
            (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishAsync(Mqtt5ConnAckBytes.Build(0, 0, maximum), new string('x', 30));

            // PUBLISH: fixed header 2, topic "t" 3, property length 1, payload 30.
            AssertFailed(result, 55, "MQTT 5 PUBLISH of 36 bytes exceeds the server's Maximum Packet Size of 20");
            Assert.IsFalse(broker.ReceivedPackets.Any(packet => packet.PacketType == Mqtt5WirePacket.Publish));
        });

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-3.2.2-15")]
    public async Task Connack_MaximumPacketSizeAboveThePublish_PublishesWithinIt()
    {
        byte[] maximum = Mqtt5ConnAckBytes.FourByteProperty(Mqtt5ConnAckBytes.MaximumPacketSize, 36);
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishAsync(Mqtt5ConnAckBytes.Build(0, 0, maximum), new string('x', 30));

        Mqtt5ConnectTests.AssertSucceededSilently(result);
        Mqtt5WirePacket publish = broker.ReceivedPackets.Single(packet => packet.PacketType == Mqtt5WirePacket.Publish);
        Assert.AreEqual(36, Mqtt5WirePacket.Frame(publish.FirstByte, publish.Body).Length);
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-3.2.2-11")]
    [SpecRequirement("Mqtt5:MQTT-3.2.2-14")]
    [SpecRequirement("Mqtt5:MQTT-3.2.2-18")]
    [SpecRequirement("Mqtt5:MQTT5-3.2.2.3.3-1")]
    public async Task Connack_ReceiveMaximumOneMaximumQos0NoRetainNoTopicAlias_PublishesAtQos0UnretainedWithNoAlias()
    {
        byte[] connAck = Mqtt5ConnAckBytes.Build(
            0,
            0,
            Mqtt5ConnAckBytes.TwoByteProperty(Mqtt5ConnAckBytes.ReceiveMaximum, 1),
            Mqtt5ConnAckBytes.ByteProperty(Mqtt5ConnAckBytes.MaximumQos, 0),
            Mqtt5ConnAckBytes.ByteProperty(Mqtt5ConnAckBytes.RetainAvailable, 0));
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishAsync(connAck);

        Mqtt5ConnectTests.AssertSucceededSilently(result);
        AssertPublishedAtQos0UnretainedWithoutProperties(result, broker);
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-3.2.2-17")]
    public async Task Connack_TopicAliasMaximum_PublishesWithNoTopicAlias()
    {
        byte[] connAck = Mqtt5ConnAckBytes.Build(0, 0, Mqtt5ConnAckBytes.TwoByteProperty(Mqtt5ConnAckBytes.TopicAliasMaximum, 5));
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishAsync(connAck);

        Mqtt5ConnectTests.AssertSucceededSilently(result);
        AssertPublishedAtQos0UnretainedWithoutProperties(result, broker);
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT5-3.2.2.3.7-1")]
    public async Task Connack_AssignedClientIdentifier_IsAcceptedAndThePublishSent()
    {
        byte[] connAck = Mqtt5ConnAckBytes.Build(0, 0, Mqtt5ConnAckBytes.StringProperty(Mqtt5ConnAckBytes.AssignedClientIdentifier, "assigned-by-broker"));
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishAsync(connAck);

        Mqtt5ConnectTests.AssertSucceededSilently(result);
        AssertPublishedAtQos0UnretainedWithoutProperties(result, broker);
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-3.2.2-21")]
    public async Task Connack_ServerKeepAlive_IsUsedSoASubscriberPings()
    {
        byte[] connAck = Mqtt5ConnAckBytes.Build(0, 0, Mqtt5ConnAckBytes.TwoByteProperty(Mqtt5ConnAckBytes.ServerKeepAlive, 1));
        Dictionary<int, Func<Mqtt5WirePacket, byte[]>> responders = new()
        {
            [Mqtt5WirePacket.Subscribe] = _ => [0x90, 0x04, 0x00, 0x01, 0x00, 0x00],
            [12] = _ => [0xE0, 0x00],
        };
        Mqtt5ReferenceBroker broker = new(connAck, responders);

        CurlRunResult result = await ExtendedConformanceHarness.RunAsync(Curl, [ReferenceRoute.Tcp(Port, broker)], ["-sS", "mqtt5://localhost/t"]);

        Mqtt5ConnectTests.AssertSucceededSilently(result);
        Assert.AreEqual(0, broker.Connect!.KeepAlive);
        CollectionAssert.AreEqual(new[] { Mqtt5WirePacket.Connect, Mqtt5WirePacket.Subscribe, 12 }, broker.ReceivedPackets.Select(packet => packet.PacketType).ToArray());
        CollectionAssert.AreEqual(new byte[] { 0xC0, 0x00 }, result.Exchanges.Single().ClientBytes[^2..]);
    }

    [TestMethod]
    public void Build_PropertiesOver127Bytes_FrameTheirLengthInTwoBytes()
    {
        byte[] connAck = Mqtt5ConnAckBytes.Build(0, 0, Mqtt5ConnAckBytes.StringProperty(Mqtt5ConnAckBytes.AssignedClientIdentifier, new string('a', 200)));

        CollectionAssert.AreEqual(new byte[] { 0x20, 0xCF, 0x01, 0x00, 0x00, 0xCB, 0x01, 0x12, 0x00, 0xC8 }, connAck[..10]);
        Assert.AreEqual(210, connAck.Length);
    }

    [TestMethod]
    public void Build_NullProperties_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => Mqtt5ConnAckBytes.Build(0, 0, null!));

    private static async Task<(CurlRunResult Result, Mqtt5ReferenceBroker Broker)> PublishAsync(byte[] connAck, string payload = "hi")
    {
        Mqtt5ReferenceBroker broker = new(connAck);
        CurlRunResult result = await ExtendedConformanceHarness.RunAsync(
            Curl, [ReferenceRoute.Tcp(Port, broker)], ["-sS", "-d", payload, "mqtt5://localhost/t"]);
        return (result, broker);
    }

    private static void AssertFailed(CurlRunResult result, int exitCode, string message)
    {
        Assert.AreEqual(exitCode, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
        Assert.AreEqual(0, result.StandardOutput.Length);
        Assert.AreEqual($"curl: ({exitCode}) {message}{Environment.NewLine}", Encoding.UTF8.GetString(result.StandardError));
    }

    /// <summary>Asserts curl sent CONNECT and then, answering the malformed or protocol-breaking CONNACK, DISCONNECT with <paramref name="reasonCode"/> (section 4.13.1, ADR-1198).</summary>
    private static void AssertSentConnectThenDisconnect(CurlRunResult result, Mqtt5ReferenceBroker broker, byte reasonCode)
    {
        CollectionAssert.AreEqual(new[] { Mqtt5WirePacket.Connect, Mqtt5WirePacket.Disconnect }, broker.ReceivedPackets.Select(packet => packet.PacketType).ToArray());
        DisconnectAuthCases.AssertEndedWithDisconnect(result, broker, reasonCode);
    }

    private static void AssertSentOnlyConnect(CurlRunResult result, Mqtt5ReferenceBroker broker)
    {
        CollectionAssert.AreEqual(new[] { Mqtt5WirePacket.Connect }, broker.ReceivedPackets.Select(packet => packet.PacketType).ToArray());
        Mqtt5WirePacket connect = broker.ReceivedPackets[0];
        CollectionAssert.AreEqual(Mqtt5WirePacket.Frame(connect.FirstByte, connect.Body), result.Exchanges.Single().ClientBytes);
    }

    private static void AssertPublishedAtQos0UnretainedWithoutProperties(CurlRunResult result, Mqtt5ReferenceBroker broker)
    {
        Mqtt5WirePacket publish = broker.ReceivedPackets.Single(packet => packet.PacketType == Mqtt5WirePacket.Publish);
        CollectionAssert.AreEqual(Mqtt5WirePacket.Frame(0x30, [0, 1, (byte)'t', 0, .. "hi"u8]), Mqtt5WirePacket.Frame(publish.FirstByte, publish.Body));
        Assert.AreEqual(1, result.Exchanges.Count);
    }
}
