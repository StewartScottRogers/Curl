using Curl.Extension.Mqtt5.Encoding;
using static Curl.Extension.Mqtt5.Encoding.Mqtt5DataTypeTests;

namespace Curl.Extension.Mqtt5.Packets;

[TestClass]
public sealed class Mqtt5PropertyCodecTests
{
    private const string PublishProperties = "1E" + "0101" + "020000003C" + "03000174" + "090001AA" + "0B8001" + "0B02" + "230007" + "260001" + "6B000176";

    [TestMethod]
    public void ReadAndWrite_EveryDataType_RoundTripsTheRecordedBytes_Section2_2_2()
    {
        var reader = new Mqtt5Reader(Convert.FromHexString(PublishProperties));

        IReadOnlyList<Mqtt5Property> properties = Mqtt5PropertyCodec.Read(reader, Mqtt5PropertyScope.Publish);

        Assert.HasCount(8, properties);
        Assert.AreEqual(1u, properties[0].IntegerValue);
        Assert.AreEqual(60u, properties[1].IntegerValue);
        Assert.AreEqual("t", properties[2].StringValue);
        CollectionAssert.AreEqual(new byte[] { 0xAA }, properties[3].BinaryValue);
        Assert.AreEqual(128u, properties[4].IntegerValue);
        Assert.AreEqual(Mqtt5PropertyIdentifier.SubscriptionIdentifier, properties[5].Identifier);
        Assert.AreEqual(7u, properties[6].IntegerValue);
        Assert.AreEqual("k", properties[7].StringValue);
        Assert.AreEqual("v", properties[7].PairValue);
        var writer = new Mqtt5Writer();
        Mqtt5PropertyCodec.Write(writer, properties, Mqtt5PropertyScope.Publish);
        Assert.AreEqual(PublishProperties, Convert.ToHexString(writer.ToArray()));
    }

    [TestMethod]
    [DataRow("06" + "1100000001" + "11")]
    [DataRow("04" + "0B01" + "0B02")]
    public void Read_SecondCopyOfAPropertyThatMayAppearOnce_IsProtocolError_Section2_2_2_2(string hex) =>
        AssertFault(Mqtt5ReasonCode.ProtocolError, () => Mqtt5PropertyCodec.Read(new Mqtt5Reader(Convert.FromHexString(hex)), Mqtt5PropertyScope.Subscribe | Mqtt5PropertyScope.Connect));

    [TestMethod]
    public void Write_SecondCopyOfAPropertyThatMayAppearOnce_IsProtocolError_Section2_2_2_2()
    {
        Mqtt5Property alias = Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.TopicAlias, 1);

        AssertFault(Mqtt5ReasonCode.ProtocolError, () => Mqtt5PropertyCodec.Write(new Mqtt5Writer(), [alias, alias], Mqtt5PropertyScope.Publish));
    }

    [TestMethod]
    [DataRow("020500")]
    [DataRow("021200")]
    [DataRow("0101")]
    public void Read_UnknownOrMisplacedIdentifierOrTruncated_IsMalformed_Section2_2_2_2(string hex) =>
        AssertFault(Mqtt5ReasonCode.MalformedPacket, () => Mqtt5PropertyCodec.Read(new Mqtt5Reader(Convert.FromHexString(hex)), Mqtt5PropertyScope.Publish));

    [TestMethod]
    public void Definitions_Table2_4_GivesTypeAndScopeOfEveryIdentifier_Section2_2_2_2()
    {
        foreach (Mqtt5PropertyIdentifier identifier in Enum.GetValues<Mqtt5PropertyIdentifier>())
        {
            Assert.IsTrue(Enum.IsDefined(Mqtt5PropertyDefinitions.TypeOf(identifier)));
        }

        Assert.AreEqual(Mqtt5PropertyType.VariableByteInteger, Mqtt5PropertyDefinitions.Get(Mqtt5PropertyIdentifier.SubscriptionIdentifier, Mqtt5PropertyScope.Subscribe).Type);
        Assert.AreEqual(Mqtt5PropertyScope.Will, Mqtt5PropertyDefinitions.Get(Mqtt5PropertyIdentifier.WillDelayInterval, Mqtt5PropertyScope.Will).Scope);
        Assert.AreEqual(Mqtt5PropertyScope.Auth, Mqtt5PropertyDefinitions.ScopeOf(Mqtt5PacketType.Auth));
    }

    [TestMethod]
    public void FromInteger_EachIntegerType_HoldsUpToItsMaximum_Section2_2_2_2()
    {
        Assert.AreEqual(255u, Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.MaximumQos, 255).IntegerValue);
        Assert.AreEqual(65_535u, Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.ReceiveMaximum, 65_535).IntegerValue);
        Assert.AreEqual(uint.MaxValue, Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.SessionExpiryInterval, uint.MaxValue).IntegerValue);
        Assert.AreEqual(268_435_455u, Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.SubscriptionIdentifier, 268_435_455).IntegerValue);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.MaximumQos, 256));
        Assert.ThrowsExactly<ArgumentException>(() => Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.ContentType, 1));
        Assert.ThrowsExactly<ArgumentException>(() => Mqtt5Property.FromString(Mqtt5PropertyIdentifier.CorrelationData, "x"));
        Assert.ThrowsExactly<ArgumentException>(() => Mqtt5Property.FromBinary(Mqtt5PropertyIdentifier.ContentType, []));
        AssertFault(Mqtt5ReasonCode.MalformedPacket, () => Mqtt5Property.FromInteger((Mqtt5PropertyIdentifier)0x05, 1));
    }

    [TestMethod]
    public void ReasonCodes_Table2_6_SplitAt0x80AndUnknownIsMalformed_Section2_4()
    {
        Assert.IsTrue(Mqtt5ReasonCodes.IsSuccess(Mqtt5ReasonCode.GrantedQos2));
        Assert.IsFalse(Mqtt5ReasonCodes.IsFailure(Mqtt5ReasonCode.ReAuthenticate));
        Assert.IsTrue(Mqtt5ReasonCodes.IsFailure(Mqtt5ReasonCode.UnspecifiedError));
        Assert.IsTrue(Mqtt5ReasonCodes.IsFailure(Mqtt5ReasonCode.WildcardSubscriptionsNotSupported));
        Assert.AreEqual(Mqtt5ReasonCode.TopicAliasInvalid, Mqtt5ReasonCodes.Read(new Mqtt5Reader(new byte[] { 0x94 })));
        Assert.AreEqual("PacketTooLarge", Enum.GetName((Mqtt5ReasonCode)0x95));
        Assert.HasCount(43, Enum.GetValues<Mqtt5ReasonCode>().Distinct());

        AssertFault(Mqtt5ReasonCode.MalformedPacket, () => Mqtt5ReasonCodes.Read(new Mqtt5Reader(new byte[] { 0x03 })));
    }

    [TestMethod]
    [DataRow("", false)]
    [DataRow("30", false)]
    [DataRow("3080", false)]
    [DataRow("308001", true)]
    public void FixedHeader_TryRead_ReadsTypeFlagsAndRemainingLengthOnceComplete_Section2_1(string hex, bool complete)
    {
        bool read = Mqtt5FixedHeader.TryRead(Convert.FromHexString(hex), out Mqtt5FixedHeader header, out int headerLength);

        Assert.AreEqual(complete, read);
        Assert.AreEqual(complete ? 3 : 0, headerLength);
        Assert.AreEqual(complete ? 128 : 0, header.RemainingLength);
    }

    [TestMethod]
    [DataRow("0000")]
    [DataRow("1100")]
    [DataRow("6000")]
    [DataRow("C200")]
    public void FixedHeader_ReservedTypeOrFlagsTable2_2DoesNotAllow_IsMalformed_Section2_1(string hex) =>
        AssertFault(Mqtt5ReasonCode.MalformedPacket, () => Mqtt5FixedHeader.TryRead(Convert.FromHexString(hex), out _, out _));
}
