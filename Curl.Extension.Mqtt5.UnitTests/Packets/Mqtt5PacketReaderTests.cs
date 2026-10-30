using static Curl.Extension.Mqtt5.Encoding.Mqtt5DataTypeTests;

namespace Curl.Extension.Mqtt5.Packets;

[TestClass]
public sealed class Mqtt5PacketReaderTests
{
    private const string FullConnect = "1029" + "00044D515454" + "05" + "EE" + "003C" + "08" + "1100000E10" + "210014" + "00026331"
        + "05" + "1800000005" + "000177" + "000178" + "000175" + "000170";

    [TestMethod]
    public void Connect_WithWillCredentialsSessionExpiryAndReceiveMaximum_WritesAndReadsRecordedBytes_Section3_1()
    {
        var connect = new Mqtt5Connect
        {
            ClientIdentifier = "c1",
            CleanStart = true,
            KeepAlive = 60,
            Properties = [Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.SessionExpiryInterval, 3600), Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.ReceiveMaximum, 20)],
            Will = new Mqtt5Will { Topic = "w", Payload = "x"u8.ToArray(), QualityOfService = Mqtt5QualityOfService.AtLeastOnce, Retain = true, Properties = [Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.WillDelayInterval, 5)] },
            UserName = "u",
            Password = "p"u8.ToArray(),
        };

        Assert.AreEqual(FullConnect, Convert.ToHexString(connect.ToBytes()));
        var read = (Mqtt5Connect)Read(FullConnect);
        Assert.AreEqual("c1", read.ClientIdentifier);
        Assert.IsTrue(read.CleanStart);
        Assert.AreEqual(60, read.KeepAlive);
        Assert.AreEqual(3600u, read.Properties[0].IntegerValue);
        Assert.AreEqual(20u, read.Properties[1].IntegerValue);
        Assert.AreEqual("w", read.Will!.Topic);
        CollectionAssert.AreEqual("x"u8.ToArray(), read.Will.Payload);
        Assert.AreEqual(Mqtt5QualityOfService.AtLeastOnce, read.Will.QualityOfService);
        Assert.IsTrue(read.Will.Retain);
        Assert.AreEqual(5u, read.Will.Properties[0].IntegerValue);
        Assert.AreEqual("u", read.UserName);
        CollectionAssert.AreEqual("p"u8.ToArray(), read.Password);
    }

    [TestMethod]
    public void Connect_Minimal_HasNoWillOrCredentials_Section3_1()
    {
        const string Minimal = "100D00044D51545405000000000000";

        Assert.AreEqual(Minimal, Convert.ToHexString(new Mqtt5Connect().ToBytes()));
        var read = (Mqtt5Connect)Read(Minimal);
        Assert.IsNull(read.Will);
        Assert.IsNull(read.UserName);
        Assert.IsNull(read.Password);
        Assert.IsFalse(read.CleanStart);
        Assert.AreEqual("1013" + "00044D515454" + "05" + "04" + "0000" + "00" + "0000" + "00" + "000174" + "0000", Convert.ToHexString(new Mqtt5Connect { Will = new Mqtt5Will { Topic = "t" } }.ToBytes()));
    }

    [TestMethod]
    [DataRow("100D00044D51545405010000000000", 0x81)]
    [DataRow("100D00044D51545805000000000000", 0x84)]
    [DataRow("100D00044D51545404000000000000", 0x84)]
    [DataRow("100D00044D51545405080000000000", 0x81)]
    [DataRow("100D00044D51545405200000000000", 0x81)]
    [DataRow("100D00044D515454051C0000000000", 0x81)]
    [DataRow("1013" + "00044D515454" + "05" + "04" + "0000" + "00" + "0000" + "00" + "00012B" + "0000", 0x90)]
    public void Connect_ReservedFlagWrongProtocolOrBadWillFlags_IsRefused_Section3_1_2(string hex, int reasonCode) =>
        AssertFault((Mqtt5ReasonCode)reasonCode, () => Read(hex));

    [TestMethod]
    public void Connect_WritingWillQos3_IsMalformed_Section3_1_2_6() =>
        AssertFault(Mqtt5ReasonCode.MalformedPacket, () => new Mqtt5Connect { Will = new Mqtt5Will { Topic = "t", QualityOfService = (Mqtt5QualityOfService)3 } }.ToBytes());

    [TestMethod]
    [DataRow("2003010000", true, 0x00)]
    [DataRow("2003008700", false, 0x87)]
    public void ConnAck_SessionPresentAndReasonCode_WritesAndReadsRecordedBytes_Section3_2(string hex, bool sessionPresent, int reasonCode)
    {
        var connAck = (Mqtt5ConnAck)Read(hex);

        Assert.AreEqual(sessionPresent, connAck.SessionPresent);
        Assert.AreEqual((Mqtt5ReasonCode)reasonCode, connAck.ReasonCode);
        Assert.AreEqual(hex, Convert.ToHexString(new Mqtt5ConnAck { SessionPresent = sessionPresent, ReasonCode = (Mqtt5ReasonCode)reasonCode }.ToBytes()));
    }

    [TestMethod]
    public void ConnAck_AssignedClientIdentifierAndReservedFlags_Section3_2()
    {
        var connAck = (Mqtt5ConnAck)Read("2008000005120002" + "6964");

        Assert.AreEqual("id", connAck.Properties[0].StringValue);
        AssertFault(Mqtt5ReasonCode.MalformedPacket, () => Read("2003020000"));
    }

    [TestMethod]
    [DataRow("30080003612F62006869")]
    [DataRow("3B06000174000A00")]
    [DataRow("340900000001032300057A")]
    public void Publish_Qos0To2RetainDupAndTopicAlias_RoundTripsRecordedBytes_Section3_3(string hex)
    {
        var publish = (Mqtt5Publish)Read(hex);

        var copy = new Mqtt5Publish
        {
            Duplicate = publish.Duplicate,
            QualityOfService = publish.QualityOfService,
            Retain = publish.Retain,
            TopicName = publish.TopicName,
            PacketIdentifier = publish.PacketIdentifier,
            Properties = publish.Properties,
            Payload = publish.Payload,
        };
        Assert.AreEqual(hex, Convert.ToHexString(copy.ToBytes()));
    }

    [TestMethod]
    public void Publish_FieldsOfRecordedBytes_Section3_3()
    {
        var publish = (Mqtt5Publish)Read("3B06000174000A00");

        Assert.IsTrue(publish.Duplicate);
        Assert.IsTrue(publish.Retain);
        Assert.AreEqual(Mqtt5QualityOfService.AtLeastOnce, publish.QualityOfService);
        Assert.AreEqual("t", publish.TopicName);
        Assert.AreEqual(10, publish.PacketIdentifier);
        Assert.IsEmpty(publish.Payload);
    }

    [TestMethod]
    [DataRow("3606000174000A00", 0x81)]
    [DataRow("380400017400", 0x82)]
    [DataRow("3206000174000000", 0x82)]
    [DataRow("3006000003230000", 0x94)]
    [DataRow("3003000000", 0x90)]
    [DataRow("300700012B03230001", 0x90)]
    public void Publish_Qos3DupAtQos0ZeroIdentifierOrBadTopic_IsRefused_Section3_3(string hex, int reasonCode) =>
        AssertFault((Mqtt5ReasonCode)reasonCode, () => Read(hex));

    [TestMethod]
    public void Publish_WritingQos3_IsMalformed_Section3_3_1_2() =>
        AssertFault(Mqtt5ReasonCode.MalformedPacket, () => new Mqtt5Publish { TopicName = "t", QualityOfService = (Mqtt5QualityOfService)3 }.ToBytes());

    [TestMethod]
    [DataRow("4002000A", 4, 0x00)]
    [DataRow("5003000A10", 5, 0x10)]
    [DataRow("6202000A", 6, 0x00)]
    [DataRow("7008000A00041F000172", 7, 0x00)]
    public void PublishResponses_ShortenedReasonAndProperties_RoundTripRecordedBytes_Section3_4To3_7(string hex, int packetType, int reasonCode)
    {
        var response = (Mqtt5PublishResponse)Read(hex);

        Assert.AreEqual((Mqtt5PacketType)packetType, response.PacketType);
        Assert.AreEqual(10, response.PacketIdentifier);
        Assert.AreEqual((Mqtt5ReasonCode)reasonCode, response.ReasonCode);
        var copy = new Mqtt5PublishResponse(response.PacketType) { PacketIdentifier = 10, ReasonCode = response.ReasonCode, Properties = response.Properties };
        Assert.AreEqual(hex, Convert.ToHexString(copy.ToBytes()));
    }

    [TestMethod]
    public void Subscribe_SubscriptionOptions_RoundTripRecordedBytes_Section3_8()
    {
        const string Hex = "820B0001020B010003612F2B2D";

        var subscribe = (Mqtt5Subscribe)Read(Hex);

        Assert.AreEqual(1, subscribe.PacketIdentifier);
        Mqtt5Subscription subscription = subscribe.Subscriptions[0];
        Assert.AreEqual("a/+", subscription.TopicFilter);
        Assert.AreEqual(Mqtt5QualityOfService.AtLeastOnce, subscription.MaximumQualityOfService);
        Assert.IsTrue(subscription.NoLocal);
        Assert.IsTrue(subscription.RetainAsPublished);
        Assert.AreEqual(Mqtt5RetainHandling.DoNotSend, subscription.RetainHandling);
        var copy = new Mqtt5Subscribe { PacketIdentifier = 1, Properties = subscribe.Properties, Subscriptions = subscribe.Subscriptions };
        Assert.AreEqual(Hex, Convert.ToHexString(copy.ToBytes()));
        Assert.AreEqual("8209000200000361" + "2F2300", Convert.ToHexString(new Mqtt5Subscribe { PacketIdentifier = 2, Subscriptions = [new Mqtt5Subscription { TopicFilter = "a/#" }] }.ToBytes()));
    }

    [TestMethod]
    [DataRow("8203000100", 0x82)]
    [DataRow("820700010000016140", 0x81)]
    [DataRow("820700010000016103", 0x82)]
    [DataRow("820700010000016130", 0x82)]
    [DataRow("82100001" + "00" + "000A" + "247368617265" + "2F672F61" + "04", 0x82)]
    [DataRow("82080001000002612300", 0x8F)]
    public void Subscribe_EmptyReservedBitsQos3RetainHandling3OrSharedNoLocal_IsRefused_Section3_8(string hex, int reasonCode) =>
        AssertFault((Mqtt5ReasonCode)reasonCode, () => Read(hex));

    [TestMethod]
    public void Subscribe_WritingNoSubscriptions_IsProtocolError_Section3_8_3() =>
        AssertFault(Mqtt5ReasonCode.ProtocolError, () => new Mqtt5Subscribe { PacketIdentifier = 1 }.ToBytes());

    [TestMethod]
    [DataRow("90050001000180", 9)]
    [DataRow("B00400020011", 11)]
    public void SubAckAndUnsubAck_ReasonCodes_RoundTripRecordedBytes_Section3_9And3_11(string hex, int packetType)
    {
        var acknowledgement = (Mqtt5SubscriptionAcknowledgement)Read(hex);

        Assert.AreEqual((Mqtt5PacketType)packetType, acknowledgement.PacketType);
        var copy = new Mqtt5SubscriptionAcknowledgement(acknowledgement.PacketType) { PacketIdentifier = acknowledgement.PacketIdentifier, ReasonCodes = acknowledgement.ReasonCodes };
        Assert.AreEqual(hex, Convert.ToHexString(copy.ToBytes()));
        AssertFault(Mqtt5ReasonCode.ProtocolError, () => Read(hex[..2] + "03000100"));
        AssertFault(Mqtt5ReasonCode.ProtocolError, () => new Mqtt5SubscriptionAcknowledgement(acknowledgement.PacketType) { PacketIdentifier = 1 }.ToBytes());
    }

    [TestMethod]
    public void Unsubscribe_TopicFilters_RoundTripRecordedBytes_Section3_10()
    {
        const string Hex = "A2080002000003612F23";

        var unsubscribe = (Mqtt5Unsubscribe)Read(Hex);

        Assert.AreEqual("a/#", unsubscribe.TopicFilters[0]);
        Assert.AreEqual(Hex, Convert.ToHexString(new Mqtt5Unsubscribe { PacketIdentifier = 2, TopicFilters = ["a/#"] }.ToBytes()));
        AssertFault(Mqtt5ReasonCode.ProtocolError, () => Read("A203000200"));
        AssertFault(Mqtt5ReasonCode.TopicFilterInvalid, () => Read("A20700020000026123"));
        AssertFault(Mqtt5ReasonCode.ProtocolError, () => new Mqtt5Unsubscribe { PacketIdentifier = 2 }.ToBytes());
        AssertFault(Mqtt5ReasonCode.TopicFilterInvalid, () => new Mqtt5Unsubscribe { PacketIdentifier = 2, TopicFilters = ["a#"] }.ToBytes());
    }

    [TestMethod]
    [DataRow("C000", 12)]
    [DataRow("D000", 13)]
    public void Ping_IsFixedHeaderAlone_Section3_12And3_13(string hex, int packetType)
    {
        Assert.AreEqual((Mqtt5PacketType)packetType, Read(hex).PacketType);
        Assert.AreEqual(hex, Convert.ToHexString(new Mqtt5Ping((Mqtt5PacketType)packetType).ToBytes()));
    }

    [TestMethod]
    [DataRow("E000", 0x00)]
    [DataRow("E00104", 0x04)]
    [DataRow("E00700051100000000", 0x00)]
    public void Disconnect_ReasonCodeAndProperties_RoundTripRecordedBytes_Section3_14(string hex, int reasonCode)
    {
        var disconnect = (Mqtt5Disconnect)Read(hex);

        Assert.AreEqual((Mqtt5ReasonCode)reasonCode, disconnect.ReasonCode);
        Assert.AreEqual(hex, Convert.ToHexString(new Mqtt5Disconnect { ReasonCode = disconnect.ReasonCode, Properties = disconnect.Properties }.ToBytes()));
    }

    [TestMethod]
    [DataRow("F000", 0x00)]
    [DataRow("F00618041500016D", 0x18)]
    public void Auth_ReasonCodeAndAuthenticationMethod_RoundTripRecordedBytes_Section3_15(string hex, int reasonCode)
    {
        var auth = (Mqtt5Auth)Read(hex);

        Assert.AreEqual((Mqtt5ReasonCode)reasonCode, auth.ReasonCode);
        Assert.AreEqual(hex, Convert.ToHexString(new Mqtt5Auth { ReasonCode = auth.ReasonCode, Properties = auth.Properties }.ToBytes()));
    }

    [TestMethod]
    public void Constructors_PacketTypeOfAnotherClass_AreRefused_Section2_1_2()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Mqtt5PublishResponse(Mqtt5PacketType.Publish));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Mqtt5SubscriptionAcknowledgement(Mqtt5PacketType.Subscribe));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Mqtt5Ping(Mqtt5PacketType.Auth));
    }

    [TestMethod]
    public void TryRead_StreamOfTwoPackets_ReadsEachOnceComplete_Section2_1_4()
    {
        byte[] stream = Convert.FromHexString("C000" + "4002000A");
        Assert.IsFalse(Mqtt5PacketReader.TryRead(ReadOnlyMemory<byte>.Empty, out _, out _));

        Assert.IsFalse(Mqtt5PacketReader.TryRead(stream.AsMemory(2, 3), out Mqtt5Packet? partial, out int partialLength));
        Assert.IsNull(partial);
        Assert.AreEqual(0, partialLength);
        Assert.IsTrue(Mqtt5PacketReader.TryRead(stream, out Mqtt5Packet? first, out int firstLength));
        Assert.AreEqual(Mqtt5PacketType.PingReq, first.PacketType);
        Assert.AreEqual(2, firstLength);
        Assert.IsTrue(Mqtt5PacketReader.TryRead(stream.AsMemory(2), out Mqtt5Packet? second, out int secondLength));
        Assert.AreEqual(Mqtt5PacketType.PubAck, second.PacketType);
        Assert.AreEqual(4, secondLength);
    }

    [TestMethod]
    [DataRow("C00100")]
    [DataRow("C00000")]
    [DataRow("4003000A")]
    [DataRow("400400")]
    [DataRow("40020000")]
    public void Read_BadRemainingLengthLeftoverBytesOrZeroIdentifier_IsMalformedOrProtocolError_Section4_13(string hex)
    {
        Mqtt5MalformedPacketException exception = Assert.ThrowsExactly<Mqtt5MalformedPacketException>(() => Read(hex));

        Assert.IsTrue(Mqtt5ReasonCodes.IsFailure(exception.ReasonCode));
    }

    private static Mqtt5Packet Read(string hex) => Mqtt5PacketReader.Read(Convert.FromHexString(hex));
}
