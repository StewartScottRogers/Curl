using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Mqtt5;
using Curl.Conformance.Extended.Mqtt5.Connack;
using Curl.Conformance.Extended.Mqtt5.PublishSubscribe;

namespace Curl.Conformance.Extended.UnitTests.Mqtt5.PublishSubscribe;

/// <summary>
/// PINGREQ and PINGRESP while a subscribing curl waits (OASIS MQTT Version 5.0 sections 3.1.2.10, 3.12 and 3.13,
/// BL-4485), on a CONNACK's Server Keep Alive of one second, so curl pings every half second.
/// </summary>
[TestClass]
public sealed class KeepAliveCases
{
    private static readonly byte[] KeepAliveOneSecond =
        Mqtt5ConnAckBytes.Build(0, 0, Mqtt5ConnAckBytes.TwoByteProperty(Mqtt5ConnAckBytes.ServerKeepAlive, 1));

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT5-3.12-1")]
    public async Task Subscribe_ServerKeepAlive_PingsAgainEachIntervalAndAcceptsEachPingresp()
    {
        int pings = 0;
        Dictionary<int, Func<Mqtt5WirePacket, byte[]>> responders = new()
        {
            [Mqtt5WirePacket.Subscribe] = _ => Mqtt5PublishSubscribeBytes.SubAck(1, 0x00),
            [PublishSubscribeRun.PingReq] = _ => ++pings == 2 ? PublishSubscribeRun.NormalDisconnect : Mqtt5PublishSubscribeBytes.PingResp,
        };
        (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishSubscribeRun.RunAsync(responders, KeepAliveOneSecond, "-m", "5", "mqtt5://localhost/t");

        PublishSubscribeRun.AssertSucceeded(result, string.Empty);
        CollectionAssert.AreEqual(
            new[] { Mqtt5WirePacket.Connect, Mqtt5WirePacket.Subscribe, PublishSubscribeRun.PingReq, PublishSubscribeRun.PingReq },
            PublishSubscribeRun.TypesOf(broker));
        CollectionAssert.AreEqual(new byte[] { 0xC0, 0x00, 0xC0, 0x00 }, result.Exchanges.Single().ClientBytes[^4..]);
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT5-3.1.2.10-1")]
    public Task Subscribe_NoPingrespWithinTheInterval_ClosesTheConnectionWithDisconnect0x8D() =>
        PublishSubscribeRun.Known.RunCaseAsync(["Mqtt5:MQTT5-3.1.2.10-1"], async () =>
        {
            Dictionary<int, Func<Mqtt5WirePacket, byte[]>> responders = new()
            {
                [Mqtt5WirePacket.Subscribe] = _ => Mqtt5PublishSubscribeBytes.SubAck(1, 0x00),
            };
            (CurlRunResult result, Mqtt5ReferenceBroker broker) = await PublishSubscribeRun.RunAsync(responders, KeepAliveOneSecond, "-m", "3", "mqtt5://localhost/t");

            Assert.AreEqual(8, result.ExitCode, System.Text.Encoding.UTF8.GetString(result.StandardError));
            CollectionAssert.AreEqual(
                new[] { Mqtt5WirePacket.Connect, Mqtt5WirePacket.Subscribe, PublishSubscribeRun.PingReq, Mqtt5WirePacket.Disconnect },
                PublishSubscribeRun.TypesOf(broker));
            PublishSubscribeRun.AssertEndedWithDisconnect(broker, 0x8D);
        });
}
