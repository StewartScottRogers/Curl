using System.Text;
using System.Text.RegularExpressions;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Mqtt5;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Mqtt5;

/// <summary>
/// Runs curl's <c>mqtt5://</c> CONNECT against the reference broker and checks it against OASIS MQTT
/// Version 5.0 section 3.1 (BL-4517). The broker checks every CONNECT it reads, so a case also fails on any
/// section 3.1 statement it breaks.
/// </summary>
[TestClass]
public sealed partial class Mqtt5ConnectTests
{
    private const int Port = 1883;

    private static readonly InProcessCurlRunner Curl = InProcessCurl.RunAsync;

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-3.1.0-1")]
    [SpecRequirement("Mqtt5:MQTT-3.1.2-1")]
    [SpecRequirement("Mqtt5:MQTT-3.1.2-3")]
    [SpecRequirement("Mqtt5:MQTT-3.1.2-4")]
    [SpecRequirement("Mqtt5:MQTT-3.1.2-11")]
    [SpecRequirement("Mqtt5:MQTT-3.1.2-13")]
    [SpecRequirement("Mqtt5:MQTT-3.1.2-16")]
    [SpecRequirement("Mqtt5:MQTT-3.1.2-18")]
    [SpecRequirement("Mqtt5:MQTT-3.1.3-1")]
    [SpecRequirement("Mqtt5:MQTT-3.1.3-2")]
    [SpecRequirement("Mqtt5:MQTT-3.1.3-3")]
    [SpecRequirement("Mqtt5:MQTT-3.1.3-4")]
    [SpecRequirement("Mqtt5:MQTT-3.1.3-5")]
    [SpecRequirement("Mqtt5:MQTT-2.2.2-1")]
    [SpecRequirement("Mqtt5:MQTT5-3.1.2.2-1")]
    public async Task Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst()
    {
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishAsync([]);

        AssertSucceededSilently(result);
        Mqtt5ReceivedConnect connect = broker.Connect!;
        Assert.IsTrue(ClientIdentifierPattern().IsMatch(connect.ClientIdentifier), connect.ClientIdentifier);
        byte[] expected = Mqtt5WirePacket.Frame(0x10, [0, 4, .. "MQTT"u8, 5, 0x02, 0, 0, 0, 0, 12, .. Encoding.ASCII.GetBytes(connect.ClientIdentifier)]);
        CollectionAssert.AreEqual(expected, result.Exchanges.Single().ClientBytes[..expected.Length]);
        CollectionAssert.AreEqual(new[] { Mqtt5WirePacket.Connect, Mqtt5WirePacket.Publish, Mqtt5WirePacket.Disconnect }, broker.ReceivedPackets.Select(packet => packet.PacketType).ToArray());
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-3.1.2-20")]
    public async Task Connect_KeepAlive_IsZeroSoNoPingIsOwed()
    {
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishAsync([]);

        AssertSucceededSilently(result);
        Assert.AreEqual(0, broker.Connect!.KeepAlive);
        Assert.AreEqual(0, broker.ReceivedPackets.Count(packet => packet.PacketType == 12));
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-3.1.2-17")]
    [SpecRequirement("Mqtt5:MQTT-3.1.2-19")]
    [SpecRequirement("Mqtt5:MQTT-3.1.3-12")]
    public async Task Connect_UserAndPassword_SetsBothFlagsAndSendsBothFieldsLast()
    {
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishAsync(["-u", "fred:secret"]);

        AssertSucceededSilently(result);
        Mqtt5ReceivedConnect connect = broker.Connect!;
        Assert.AreEqual(0xC2, connect.Flags);
        Assert.AreEqual("fred", connect.UserName);
        CollectionAssert.AreEqual("secret"u8.ToArray(), connect.Password);
        byte[] body = broker.ReceivedPackets[0].Body;
        byte[] credentials = [0, 4, .. "fred"u8, 0, 6, .. "secret"u8];
        CollectionAssert.AreEqual(credentials, body[^14..]);
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-3.1.2-18")]
    public async Task Connect_UserWithEmptyPassword_SendsUserNameOnly()
    {
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishAsync(["-u", "fred:"]);

        AssertSucceededSilently(result);
        Assert.AreEqual(0x82, broker.Connect!.Flags);
        Assert.AreEqual("fred", broker.Connect.UserName);
        Assert.IsNull(broker.Connect.Password);
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-1.5.4-1")]
    public async Task Connect_NonAsciiUserName_IsWellFormedUtf8()
    {
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishAsync(["-u", "jürgen€:pw"]);

        AssertSucceededSilently(result);
        Assert.AreEqual("jürgen€", broker.Connect!.UserName);
    }

    internal static async Task<(CurlRunResult Result, Mqtt5ReferenceBroker Broker)> PublishAsync(IReadOnlyList<string> options, string payload = "hi", string topic = "t")
    {
        Mqtt5ReferenceBroker broker = new();
        CurlRunResult result = await ExtendedConformanceHarness.RunAsync(
            Curl, [ReferenceRoute.Tcp(Port, new ReadUntilClosedServer(broker))], ["-s", .. options, "-d", payload, $"mqtt5://localhost/{topic}"]);
        return (result, broker);
    }

    internal static void AssertSucceededSilently(CurlRunResult result)
    {
        Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
        Assert.AreEqual(0, result.StandardOutput.Length);
        Assert.AreEqual(0, result.StandardError.Length);
    }

    [GeneratedRegex("^curl[A-Za-z0-9]{8}$")]
    private static partial Regex ClientIdentifierPattern();
}
