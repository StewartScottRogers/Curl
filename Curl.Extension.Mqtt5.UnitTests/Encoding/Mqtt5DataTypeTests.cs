using Curl.Extension.Mqtt5.Packets;

namespace Curl.Extension.Mqtt5.Encoding;

[TestClass]
public sealed class Mqtt5DataTypeTests
{
    [TestMethod]
    [DataRow(0, "00")]
    [DataRow(127, "7F")]
    [DataRow(128, "8001")]
    [DataRow(16_383, "FF7F")]
    [DataRow(16_384, "808001")]
    [DataRow(2_097_151, "FFFF7F")]
    [DataRow(2_097_152, "80808001")]
    [DataRow(268_435_455, "FFFFFF7F")]
    public void VariableByteInteger_SpecificationBoundaryValues_WriteAndReadBack_Section1_5_5(int value, string hex)
    {
        var writer = new Mqtt5Writer();
        writer.WriteVariableByteInteger(value);
        Assert.AreEqual(hex, Convert.ToHexString(writer.ToArray()));

        var reader = new Mqtt5Reader(Convert.FromHexString(hex));
        Assert.AreEqual(value, reader.ReadVariableByteInteger());
        Assert.IsTrue(reader.IsAtEnd);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(268_435_456)]
    public void WriteVariableByteInteger_OutsideZeroTo268435455_IsMalformed_Section1_5_5(int value) =>
        AssertFault(Mqtt5ReasonCode.MalformedPacket, () => new Mqtt5Writer().WriteVariableByteInteger(value));

    [TestMethod]
    [DataRow("FFFFFFFF")]
    [DataRow("FFFFFFFF7F")]
    [DataRow("8000")]
    public void ReadVariableByteInteger_FifthByteOrLongerThanNeeded_IsMalformed_Section1_5_5(string hex) =>
        AssertFault(Mqtt5ReasonCode.MalformedPacket, () => new Mqtt5Reader(Convert.FromHexString(hex)).ReadVariableByteInteger());

    [TestMethod]
    [DataRow("")]
    [DataRow("80")]
    [DataRow("FFFF")]
    public void TryRead_BytesEndInsideTheInteger_ReturnsFalse_Section1_5_5(string hex)
    {
        Assert.IsFalse(Mqtt5VariableByteInteger.TryRead(Convert.FromHexString(hex), out int value, out int length));
        Assert.AreEqual(0, value);
        Assert.AreEqual(0, length);
        AssertFault(Mqtt5ReasonCode.MalformedPacket, () => new Mqtt5Reader(Convert.FromHexString(hex)).ReadVariableByteInteger());
    }

    [TestMethod]
    public void Integers_TwoAndFourByte_AreBigEndian_Section1_5_2And1_5_3()
    {
        var writer = new Mqtt5Writer();
        writer.WriteByte(0x01);
        writer.WriteTwoByteInteger(0x0203);
        writer.WriteFourByteInteger(0x04050607);
        Assert.AreEqual("01020304050607", Convert.ToHexString(writer.ToArray()));
        Assert.AreEqual(7, writer.Length);

        var reader = new Mqtt5Reader(writer.ToArray());
        Assert.AreEqual(0x01, reader.ReadByte());
        Assert.AreEqual(0x0203, reader.ReadTwoByteInteger());
        Assert.AreEqual(0x04050607u, reader.ReadFourByteInteger());
        Assert.AreEqual(0, reader.RemainingByteCount);
    }

    [TestMethod]
    public void Utf8String_SpecificationExample_IsLengthThenUtf8_Section1_5_4()
    {
        var writer = new Mqtt5Writer();
        writer.WriteUtf8String("A\U0002A6D4");
        Assert.AreEqual("000541F0AA9B94", Convert.ToHexString(writer.ToArray()));
        Assert.AreEqual("A\U0002A6D4", new Mqtt5Reader(writer.ToArray()).ReadUtf8String());
    }

    [TestMethod]
    [DataRow(0xD800)]
    [DataRow(0x0000)]
    public void WriteUtf8String_LoneSurrogateOrNullCharacter_IsMalformed_Section1_5_4(int character) =>
        AssertFault(Mqtt5ReasonCode.MalformedPacket, () => new Mqtt5Writer().WriteUtf8String("a" + (char)character + "b"));

    [TestMethod]
    public void WriteUtf8String_MoreThan65535Bytes_IsMalformed_Section1_5_4() =>
        AssertFault(Mqtt5ReasonCode.MalformedPacket, () => new Mqtt5Writer().WriteUtf8String(new string('a', 65_536)));

    [TestMethod]
    [DataRow("0001FF")]
    [DataRow("0002C328")]
    [DataRow("0003EDA080")]
    [DataRow("00026100")]
    [DataRow("000561")]
    public void ReadUtf8String_IllFormedNullOrTruncated_IsMalformed_Section1_5_4(string hex) =>
        AssertFault(Mqtt5ReasonCode.MalformedPacket, () => new Mqtt5Reader(Convert.FromHexString(hex)).ReadUtf8String());

    [TestMethod]
    public void BinaryDataAndStringPair_WriteAndReadBack_Section1_5_6And1_5_7()
    {
        var writer = new Mqtt5Writer();
        writer.WriteBinaryData([0xAA, 0xBB]);
        writer.WriteUtf8StringPair("k", "v");
        writer.WriteBytes([0xCC]);
        Assert.AreEqual("0002AABB00016B000176CC", Convert.ToHexString(writer.ToArray()));

        var reader = new Mqtt5Reader(writer.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB }, reader.ReadBinaryData());
        Assert.AreEqual(new KeyValuePair<string, string>("k", "v"), reader.ReadUtf8StringPair());
        CollectionAssert.AreEqual(new byte[] { 0xCC }, reader.ReadRemainingBytes());
    }

    [TestMethod]
    public void WriteBinaryData_MoreThan65535Bytes_IsMalformed_Section1_5_6() =>
        AssertFault(Mqtt5ReasonCode.MalformedPacket, () => new Mqtt5Writer().WriteBinaryData(new byte[65_536]));

    [TestMethod]
    public void Reader_FieldsPastTheEnd_AreMalformed_Section4_13()
    {
        AssertFault(Mqtt5ReasonCode.MalformedPacket, () => new Mqtt5Reader(Array.Empty<byte>()).ReadByte());
        AssertFault(Mqtt5ReasonCode.MalformedPacket, () => new Mqtt5Reader(new byte[3]).ReadFourByteInteger());
        AssertFault(Mqtt5ReasonCode.MalformedPacket, () => new Mqtt5Reader(new byte[1]).ReadSection(2));
    }

    [TestMethod]
    public void ReadSection_TakesTheBytesAsAReaderOfTheirOwn_Section2_2_2()
    {
        var reader = new Mqtt5Reader(new byte[] { 1, 2, 3 });
        Mqtt5Reader section = reader.ReadSection(2);
        Assert.AreEqual(2, section.RemainingByteCount);
        Assert.AreEqual(3, reader.ReadByte());
    }

    internal static void AssertFault(Mqtt5ReasonCode reasonCode, Action action)
    {
        Mqtt5MalformedPacketException exception = Assert.ThrowsExactly<Mqtt5MalformedPacketException>(action);
        Assert.AreEqual(reasonCode, exception.ReasonCode);
    }
}
