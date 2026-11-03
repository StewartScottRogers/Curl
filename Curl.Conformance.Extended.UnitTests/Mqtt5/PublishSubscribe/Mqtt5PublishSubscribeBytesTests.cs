using Curl.Conformance.Extended.Mqtt5.PublishSubscribe;

namespace Curl.Conformance.Extended.UnitTests.Mqtt5.PublishSubscribe;

[TestClass]
public sealed class Mqtt5PublishSubscribeBytesTests
{
    [TestMethod]
    public void PubRel_PacketIdentifier_SetsTheReservedFlags0010AndLeavesOutReasonCode0()
    {
        byte[] pubRel = Mqtt5PublishSubscribeBytes.PubRel(0x0102);

        CollectionAssert.AreEqual(new byte[] { 0x62, 0x02, 0x01, 0x02 }, pubRel);
    }

    [TestMethod]
    public void Publish_Qos1_CarriesThePacketIdentifierBeforeTheProperties()
    {
        byte[] publish = Mqtt5PublishSubscribeBytes.Publish(1, 7, "t", "x"u8.ToArray(), [Mqtt5PublishSubscribeBytes.PayloadFormatIndicator, 0x01]);

        CollectionAssert.AreEqual(new byte[] { 0x32, 0x09, 0x00, 0x01, (byte)'t', 0x00, 0x07, 0x02, 0x01, 0x01, (byte)'x' }, publish);
    }
}
