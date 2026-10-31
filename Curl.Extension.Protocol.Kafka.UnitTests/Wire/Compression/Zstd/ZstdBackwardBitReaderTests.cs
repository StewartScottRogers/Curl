namespace Curl.Extension.Protocol.Kafka.Wire.Compression.Zstd;

/// <summary>Checks the backward bit reader against RFC 8878 section 4.1's description of the stream.</summary>
[TestClass]
public sealed class ZstdBackwardBitReaderTests
{
    [TestMethod]
    public void ReadBits_OneByteStream_ReadsFromBelowTheEndMarkerDownward()
    {
        // 0x31 = 0b0011_0001: the marker is bit 5, the data bits below it read 1, 0, 0, 0, 1.
        ZstdBackwardBitReader reader = new([0x31]);

        Assert.AreEqual(5, reader.RemainingBits);
        Assert.AreEqual(2u, reader.ReadBits(2));
        Assert.AreEqual(1u, reader.ReadBits(3));
        Assert.AreEqual(0, reader.RemainingBits);
        Assert.IsFalse(reader.Overflowed);
    }

    [TestMethod]
    public void ReadBits_TwoByteStream_ContinuesFromTheLastByteIntoTheFirst()
    {
        // The last byte 0x03 has its marker at bit 1, leaving bit 0 (1); then 0xA5 from bit 7 down: 1,0,1,0,0,1,0,1.
        ZstdBackwardBitReader reader = new([0xA5, 0x03]);

        Assert.AreEqual(9, reader.RemainingBits);
        Assert.AreEqual(0x1A5u, reader.PeekBits(9));
        Assert.AreEqual(9, reader.RemainingBits);
        Assert.AreEqual(0x1A5u, reader.ReadBits(9));
    }

    [TestMethod]
    public void ReadBits_PastTheStart_GivesZeroBitsAndOverflows()
    {
        ZstdBackwardBitReader reader = new([0x31]);
        reader.SkipBits(4);

        Assert.AreEqual(4u, reader.ReadBits(3));
        Assert.IsTrue(reader.Overflowed);
        Assert.AreEqual(-2, reader.RemainingBits);
        Assert.AreEqual(0u, reader.ReadBits(2));
    }

    [TestMethod]
    public void Constructor_EmptyStream_ThrowsFormatExceptionNamingZstd()
    {
        FormatException exception = Assert.ThrowsExactly<FormatException>(() => new ZstdBackwardBitReader([]));

        StringAssert.Contains(exception.Message, "zstd");
    }

    [TestMethod]
    public void Constructor_LastByteZero_ThrowsFormatExceptionNamingZstd()
    {
        FormatException exception = Assert.ThrowsExactly<FormatException>(() => new ZstdBackwardBitReader([0x01, 0x00]));

        StringAssert.Contains(exception.Message, "zstd");
    }
}
