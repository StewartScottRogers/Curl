using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Mqtt5;
using Curl.Conformance.Extended.Mqtt5.Connack;
using Curl.Conformance.Extended.Mqtt5.DisconnectAuth;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Mqtt5;

/// <summary>
/// Runs curl's <c>mqtt5://</c> against the reference broker and checks its DISCONNECT and AUTH handling against OASIS
/// MQTT Version 5.0 sections 3.14, 3.15 and 4.12 (BL-4519): the DISCONNECT curl sends after a PUBLISH, a DISCONNECT
/// from the server, and an AUTH from a server curl never asked to authenticate with a method.
/// </summary>
[TestClass]
public sealed class DisconnectAuthCases
{
    private const int Port = 1883;

    /// <summary>The AUTH packet type.</summary>
    private const int Auth = 15;

    private static readonly InProcessCurlRunner Curl = InProcessCurl.RunAsync;

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-3.14.2-1")]
    [SpecRequirement("Mqtt5:MQTT-3.14.4-1")]
    [SpecRequirement("Mqtt5:MQTT-3.14.4-2")]
    public async Task Publish_EndsWithANormalDisconnectAndSendsNothingAfterIt()
    {
        Mqtt5ReferenceBroker broker = new();
        CurlRunResult result = await ExtendedConformanceHarness.RunAsync(Curl, [ReferenceRoute.Tcp(Port, broker)], ["-sS", "-d", "hi", "mqtt5://localhost/t"]);

        Mqtt5ConnectTests.AssertSucceededSilently(result);
        CollectionAssert.AreEqual(
            new[] { Mqtt5WirePacket.Connect, Mqtt5WirePacket.Publish, Mqtt5WirePacket.Disconnect },
            broker.ReceivedPackets.Select(packet => packet.PacketType).ToArray());

        // Reason Code 0x00 Normal disconnection with no properties may be left out entirely (section 3.14.2.1).
        Assert.AreEqual(0, broker.ReceivedPackets[^1].Body.Length);
        CollectionAssert.AreEqual(new byte[] { 0xE0, 0x00 }, result.Exchanges.Single().ClientBytes[^2..]);
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT5-3.14.4-1")]
    public async Task Subscribe_ServerDisconnectsNormally_WritesTheMessageAndEndsWithExit0()
    {
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await SubscribeAsync(
            Mqtt5DisconnectAuthBytes.Publish("t", "hello"u8.ToArray()),
            Mqtt5DisconnectAuthBytes.Disconnect(0, 0x00));

        Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
        Assert.AreEqual("hello", Encoding.UTF8.GetString(result.StandardOutput));
        Assert.AreEqual(0, result.StandardError.Length);
        AssertSentNothingAfterSubscribe(broker);
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-4.13.2-1")]
    public async Task Subscribe_ServerDisconnectsWithServerShuttingDown_EndsTheTransferWithExit8()
    {
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await SubscribeAsync(
            Mqtt5DisconnectAuthBytes.Disconnect(0, 0x8B, Mqtt5ConnAckBytes.StringProperty(Mqtt5DisconnectAuthBytes.ReasonString, "maintenance")));

        AssertFailed(result, 8, $"curl: (8) MQTT 5 DISCONNECT reason code 0x8B ServerShuttingDown{Environment.NewLine}");
        AssertSentNothingAfterSubscribe(broker);
    }

    [TestMethod]
    public async Task Subscribe_DisconnectWithReservedFlagSet_IsAMalformedPacketWithExit8()
    {
        (CurlRunResult result, _) = await SubscribeAsync(Mqtt5DisconnectAuthBytes.Disconnect(0x01, 0x00));

        AssertFailedAsMalformed(result);
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-3.14.1-1")]
    public Task Subscribe_DisconnectWithReservedFlagSet_IsAnsweredByDisconnect0x81() =>
        Known.RunCaseAsync(["Mqtt5:MQTT-3.14.1-1"], async () =>
        {
            (CurlRunResult result, Mqtt5ReferenceBroker broker) = await SubscribeAsync(Mqtt5DisconnectAuthBytes.Disconnect(0x01, 0x00));

            AssertFailedAsMalformed(result);
            AssertEndedWithDisconnect(result, broker, 0x81);
        });

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-4.12.0-7")]
    public async Task Connect_AnsweredByAuthWithAMethodCurlDidNotOffer_SendsNoAuthAndExits8()
    {
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishAnsweredByAsync(UnknownMethodAuth());

        AssertFailed(result, 8, $"curl: (8) Weird server reply{Environment.NewLine}");
        Assert.IsFalse(broker.ReceivedPackets.Any(packet => packet.PacketType is Auth or Mqtt5WirePacket.Publish));
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT5-3.15-1")]
    public Task Connect_AnsweredByAuthWithAMethodCurlDidNotOffer_IsAProtocolErrorAnsweredByDisconnect0x82() =>
        Known.RunCaseAsync(["Mqtt5:MQTT5-3.15-1"], async () =>
        {
            (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishAnsweredByAsync(UnknownMethodAuth());

            AssertFailed(result, 8, $"curl: (8) Weird server reply{Environment.NewLine}");
            AssertEndedWithDisconnect(result, broker, 0x82);
        });

    [TestMethod]
    public async Task Subscribe_AuthWithReservedFlagSet_IsAMalformedPacketWithExit8()
    {
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await SubscribeAsync(Mqtt5DisconnectAuthBytes.Auth(0x01, 0x18));

        AssertFailedAsMalformed(result);
        Assert.IsFalse(broker.ReceivedPackets.Any(packet => packet.PacketType == Auth));
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-3.15.1-1")]
    public Task Subscribe_AuthWithReservedFlagSet_IsAnsweredByDisconnect0x81() =>
        Known.RunCaseAsync(["Mqtt5:MQTT-3.15.1-1"], async () =>
        {
            (CurlRunResult result, Mqtt5ReferenceBroker broker) = await SubscribeAsync(Mqtt5DisconnectAuthBytes.Auth(0x01, 0x18));

            AssertFailedAsMalformed(result);
            AssertEndedWithDisconnect(result, broker, 0x81);
        });

    [TestMethod]
    public void Disconnect_NullProperties_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => Mqtt5DisconnectAuthBytes.Disconnect(0, 0, null!));

    [TestMethod]
    public void Publish_NullTopicOrPayload_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Mqtt5DisconnectAuthBytes.Publish(null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => Mqtt5DisconnectAuthBytes.Publish("t", null!));
    }

    [TestMethod]
    public void RawStringProperty_NullValue_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => Mqtt5DisconnectAuthBytes.RawStringProperty(0x1F, null!));

    /// <summary>Runs <c>curl -sS mqtt5://localhost/t</c>; the broker answers SUBSCRIBE with a SUBACK and then <paramref name="afterSubAck"/>.</summary>
    internal static async Task<(CurlRunResult Result, Mqtt5ReferenceBroker Broker)> SubscribeAsync(params byte[][] afterSubAck)
    {
        byte[] answer = [.. Mqtt5DisconnectAuthBytes.SubAckGrantingQos0(), .. afterSubAck.SelectMany(packet => packet)];
        Mqtt5ReferenceBroker broker = new(responders: new Dictionary<int, Func<Mqtt5WirePacket, byte[]>> { [Mqtt5WirePacket.Subscribe] = _ => answer });
        CurlRunResult result = await ExtendedConformanceHarness.RunAsync(Curl, [ReferenceRoute.Tcp(Port, broker)], ["-sS", "mqtt5://localhost/t"]);
        return (result, broker);
    }

    /// <summary>Runs <c>curl -sS -d hi mqtt5://localhost/t</c> against a broker answering CONNECT with <paramref name="connectAnswer"/>.</summary>
    internal static async Task<(CurlRunResult Result, Mqtt5ReferenceBroker Broker)> PublishAnsweredByAsync(byte[] connectAnswer)
    {
        Mqtt5ReferenceBroker broker = new(connectAnswer);
        CurlRunResult result = await ExtendedConformanceHarness.RunAsync(Curl, [ReferenceRoute.Tcp(Port, broker)], ["-sS", "-d", "hi", "mqtt5://localhost/t"]);
        return (result, broker);
    }

    /// <summary>Asserts the exit code, no output and exactly <paramref name="standardError"/>.</summary>
    internal static void AssertFailed(CurlRunResult result, int exitCode, string standardError)
    {
        Assert.AreEqual(exitCode, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
        Assert.AreEqual(0, result.StandardOutput.Length);
        Assert.AreEqual(standardError, Encoding.UTF8.GetString(result.StandardError));
    }

    /// <summary>Asserts exit 8, no output and curl's Malformed Packet message.</summary>
    internal static void AssertFailedAsMalformed(CurlRunResult result)
    {
        Assert.AreEqual(8, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
        Assert.AreEqual(0, result.StandardOutput.Length);
        StringAssert.StartsWith(Encoding.UTF8.GetString(result.StandardError), "curl: (8) Weird server reply: malformed MQTT 5 packet: ");
    }

    /// <summary>Asserts curl's last packet is a DISCONNECT with <paramref name="reasonCode"/> and that nothing follows it (sections 3.14.4 and 4.13.1).</summary>
    internal static void AssertEndedWithDisconnect(CurlRunResult result, Mqtt5ReferenceBroker broker, byte reasonCode)
    {
        Mqtt5WirePacket last = broker.ReceivedPackets[^1];
        Assert.AreEqual(Mqtt5WirePacket.Disconnect, last.PacketType, "curl closed the connection without a DISCONNECT carrying the reason code.");
        Assert.AreEqual(reasonCode, last.Body[0]);
        byte[] frame = Mqtt5WirePacket.Frame(last.FirstByte, last.Body);
        CollectionAssert.AreEqual(frame, result.Exchanges.Single().ClientBytes[^frame.Length..]);
    }

    private static void AssertSentNothingAfterSubscribe(Mqtt5ReferenceBroker broker) =>
        CollectionAssert.AreEqual(new[] { Mqtt5WirePacket.Connect, Mqtt5WirePacket.Subscribe }, broker.ReceivedPackets.Select(packet => packet.PacketType).ToArray());

    private static byte[] UnknownMethodAuth() =>
        Mqtt5DisconnectAuthBytes.Auth(0, 0x18, Mqtt5ConnAckBytes.StringProperty(Mqtt5DisconnectAuthBytes.AuthenticationMethod, "SCRAM-SHA-1"));
}
