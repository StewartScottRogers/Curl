namespace Curl.Extension.Kafka.Wire;

/// <summary>Checks the writer and reader against the protocol guide's "Protocol Primitive Types" and CRC-32C.</summary>
[TestClass]
public sealed class KafkaPrimitiveTests
{
    [TestMethod]
    [DataRow(0, new byte[] { 0x00 })]
    [DataRow(-1, new byte[] { 0x01 })]
    [DataRow(1, new byte[] { 0x02 })]
    [DataRow(-64, new byte[] { 0x7F })]
    [DataRow(64, new byte[] { 0x80, 0x01 })]
    [DataRow(int.MaxValue, new byte[] { 0xFE, 0xFF, 0xFF, 0xFF, 0x0F })]
    [DataRow(int.MinValue, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F })]
    public void VarInt_IsZigZagLittleEndianBase128AndReadsBack(int value, byte[] expected)
    {
        byte[] written = new KafkaWriter().VarInt(value).ToArray();

        CollectionAssert.AreEqual(expected, written);
        Assert.AreEqual(value, new KafkaReader(written).VarInt());
    }

    [TestMethod]
    [DataRow(long.MaxValue)]
    [DataRow(long.MinValue)]
    [DataRow(300L)]
    public void VarLong_ReadsBack(long value) =>
        Assert.AreEqual(value, new KafkaReader(new KafkaWriter().VarLong(value).ToArray()).VarLong());

    [TestMethod]
    public void FixedWidthTypes_AreBigEndianAndReadBack()
    {
        byte[] written = new KafkaWriter().Int8(-2).Int16(0x0102).Int32(0x03040506).UInt32(0xFFFFFFFE).Int64(0x0708).ToArray();

        CollectionAssert.AreEqual(new byte[] { 0xFE, 1, 2, 3, 4, 5, 6, 0xFF, 0xFF, 0xFF, 0xFE, 0, 0, 0, 0, 0, 0, 7, 8 }, written);
        KafkaReader reader = new(written);
        Assert.AreEqual((sbyte)-2, reader.Int8());
        Assert.AreEqual((short)0x0102, reader.Int16());
        Assert.AreEqual(0x03040506, reader.Int32());
        Assert.AreEqual(0xFFFFFFFE, reader.UInt32());
        Assert.AreEqual(0x0708L, reader.Int64());
        Assert.AreEqual(0, reader.Remaining);
    }

    [TestMethod]
    public void StringsAndBytes_CarryTheirLengthsAndMinusOneIsNull()
    {
        byte[] written = new KafkaWriter().String("ab").NullString().Bytes([9]).Int32(-1).VarInt(1).Raw([7]).VarInt(-1).ToArray();

        CollectionAssert.AreEqual(new byte[] { 0, 2, (byte)'a', (byte)'b', 0xFF, 0xFF, 0, 0, 0, 1, 9, 0xFF, 0xFF, 0xFF, 0xFF, 2, 7, 1 }, written);
        KafkaReader reader = new(written);
        Assert.AreEqual("ab", reader.NullableString());
        Assert.IsNull(reader.NullableString());
        CollectionAssert.AreEqual(new byte[] { 9 }, reader.NullableBytes()!.Value.ToArray());
        Assert.IsNull(reader.NullableBytes());
        CollectionAssert.AreEqual(new byte[] { 7 }, reader.NullableVarBytes()!.Value.ToArray());
        Assert.IsNull(reader.NullableVarBytes());
    }

    [TestMethod]
    public void Reader_PastTheEndOrNegativeLength_ThrowsTruncated()
    {
        Assert.AreEqual(KafkaReader.Truncated, Assert.ThrowsExactly<FormatException>(() => new KafkaReader(new byte[3]).Int32()).Message);
        Assert.AreEqual(KafkaReader.Truncated, Assert.ThrowsExactly<FormatException>(() => new KafkaReader(new byte[3]).Take(-2)).Message);
    }

    [TestMethod]
    public void VarLong_LongerThanTenBytes_Throws() =>
        Assert.AreEqual(KafkaReader.VarIntTooLong, Assert.ThrowsExactly<FormatException>(() => new KafkaReader(Enumerable.Repeat((byte)0x80, 11).ToArray()).VarLong()).Message);

    [TestMethod]
    [DataRow(int.MaxValue + 1L)]
    [DataRow(int.MinValue - 1L)]
    public void VarInt_OutsideInt32_Throws(long value) =>
        Assert.AreEqual(KafkaReader.VarIntOutOfRange, Assert.ThrowsExactly<FormatException>(() => new KafkaReader(new KafkaWriter().VarLong(value).ToArray()).VarInt()).Message);

    [TestMethod]
    public void Crc32C_MatchesTheCastagnoliCheckValue()
    {
        Assert.AreEqual(0xE3069283u, Crc32C.Compute("123456789"u8));
        Assert.AreEqual(0u, Crc32C.Compute([]));
    }
}
