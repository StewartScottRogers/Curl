namespace Curl.Protocol.Ssh.Compression;

[TestClass]
public sealed class SshZlibDecompressorTests
{
    [TestMethod]
    public void Decompress_OpenSshPartialFlushes_InflatesEachPacketAsItArrives()
    {
        // What OpenSSH's deflate(Z_PARTIAL_FLUSH) sends for "a" then "b", built bit by bit:
        // a fixed-Huffman block with the literal, then the empty fixed block the partial
        // flush adds. The first packet ends four bits short of a byte; the second packet's
        // first byte carries them.
        SshZlibDecompressor decompressor = new();

        byte[] first = decompressor.Decompress([0x78, 0x01, 0x4A, 0x04, 0x08]);
        byte[] second = decompressor.Decompress([0xA0, 0x24, 0x80, 0x00]);

        CollectionAssert.AreEqual("a"u8.ToArray(), first);
        CollectionAssert.AreEqual("b"u8.ToArray(), second);
    }

    [TestMethod]
    public void Decompress_NotAZlibStream_Throws()
    {
        SshZlibDecompressor decompressor = new();

        Assert.ThrowsExactly<InvalidDataException>(() => decompressor.Decompress([0xFF, 0xFF, 0xFF, 0xFF]));
    }

    [TestMethod]
    public void Decompress_BytesThatCompleteNoPayloadByte_Throws()
    {
        SshZlibDecompressor decompressor = new();

        InvalidDataException exception = Assert.ThrowsExactly<InvalidDataException>(() => decompressor.Decompress([0x78, 0x01]));

        Assert.AreEqual("The SSH packet inflates to no payload.", exception.Message);
    }

    [TestMethod]
    public void Decompress_MoreThanTheMaximumPayload_ThrowsAsLibssh2RefusesExcessiveGrowth()
    {
        byte[] bomb = new SshZlibCompressor().Compress(new byte[SshZlibDecompressor.MaximumPayloadLength + 1]);
        SshZlibDecompressor decompressor = new();

        InvalidDataException exception = Assert.ThrowsExactly<InvalidDataException>(() => decompressor.Decompress(bomb));

        Assert.AreEqual("The SSH packet inflates to more than 40000 bytes.", exception.Message);
    }

    [TestMethod]
    public void Decompress_TheMaximumPayload_Inflates()
    {
        byte[] payload = new byte[SshZlibDecompressor.MaximumPayloadLength];
        byte[] compressed = new SshZlibCompressor().Compress(payload);

        byte[] inflated = new SshZlibDecompressor().Decompress(compressed);

        Assert.HasCount(payload.Length, inflated);
    }
}
