namespace Curl.Extension.ObliviousDoh.Wire;

[TestClass]
public sealed class LengthPrefixedReaderTests
{
    [TestMethod]
    public void TryReadUInt16_ExactlyTwoBytesLeft_ReadsTheBigEndianValueAndEmptiesTheReader()
    {
        LengthPrefixedReader reader = new([0x12, 0x34]);

        bool read = reader.TryReadUInt16(out ushort value);

        Assert.IsTrue(read);
        Assert.AreEqual((ushort)0x1234, value);
        Assert.IsTrue(reader.IsEmpty);
    }

    [TestMethod]
    public void TryReadUInt16_FinalFieldAfterAnotherInteger_ReadsItsValue()
    {
        LengthPrefixedReader reader = new([0x00, 0x20, 0x00, 0x01]);

        Assert.IsTrue(reader.TryReadUInt16(out ushort first));
        Assert.IsTrue(reader.TryReadUInt16(out ushort last));

        Assert.AreEqual((ushort)0x0020, first);
        Assert.AreEqual((ushort)0x0001, last);
    }

    [TestMethod]
    public void TryReadUInt16_OneByteLeft_FailsWithZero()
    {
        LengthPrefixedReader reader = new([0x12]);

        bool read = reader.TryReadUInt16(out ushort value);

        Assert.IsFalse(read);
        Assert.AreEqual((ushort)0, value);
        Assert.IsFalse(reader.IsEmpty);
    }

    [TestMethod]
    public void TryReadByte_LastByte_ReadsItAndEmptiesTheReader()
    {
        LengthPrefixedReader reader = new([0xAB]);

        Assert.IsTrue(reader.TryReadByte(out byte value));
        Assert.AreEqual((byte)0xAB, value);
        Assert.IsTrue(reader.IsEmpty);
    }

    [TestMethod]
    public void TryReadVector_VectorEndsTheInput_ReadsItsBytes()
    {
        LengthPrefixedReader reader = new([0x00, 0x02, 0xCC, 0xDD]);

        Assert.IsTrue(reader.TryReadVector(out ReadOnlySpan<byte> value));
        CollectionAssert.AreEqual(new byte[] { 0xCC, 0xDD }, value.ToArray());
        Assert.IsTrue(reader.IsEmpty);
    }
}
