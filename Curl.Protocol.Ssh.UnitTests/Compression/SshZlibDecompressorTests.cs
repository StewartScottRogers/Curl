using Curl.Testing;

namespace Curl.Protocol.Ssh.Compression;

[TestClass]
public sealed class SshZlibDecompressorTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Decompress_OpenSshPartialFlushes_InflatesEachPacketAsItArrives()
    {
        // What OpenSSH's deflate(Z_PARTIAL_FLUSH) sends for "a" then "b", built bit by bit:
        // a fixed-Huffman block with the literal, then the empty fixed block the partial
        // flush adds. The first packet ends four bits short of a byte; the second packet's
        // first byte carries them.
        SshZlibDecompressor decompressor = new();
        Diagnostics.Arrange("packets", "78 01 4a 04 08, then a0 24 80 00");

        byte[] first = decompressor.Decompress([0x78, 0x01, 0x4A, 0x04, 0x08]);
        byte[] second = decompressor.Decompress([0xA0, 0x24, 0x80, 0x00]);

        Diagnostics.Bytes("first payload", first);
        Diagnostics.Bytes("second payload", second);
        Diagnostics.Act("payload lengths", $"{first.Length}, {second.Length}");
        Diagnostics.Diff("first payload", "a"u8, first);
        Diagnostics.Diff("second payload", "b"u8, second);
        CollectionAssert.AreEqual("a"u8.ToArray(), first);
        CollectionAssert.AreEqual("b"u8.ToArray(), second);
    }

    [TestMethod]
    public void Decompress_NotAZlibStream_Throws()
    {
        SshZlibDecompressor decompressor = new();
        Diagnostics.Arrange("packet", "ff ff ff ff");

        InvalidDataException exception = Assert.ThrowsExactly<InvalidDataException>(() => decompressor.Decompress([0xFF, 0xFF, 0xFF, 0xFF]));

        Diagnostics.Act("exception", exception.Message);
        Diagnostics.Assert("exception type", nameof(InvalidDataException), exception.GetType().Name);
    }

    [TestMethod]
    public void Decompress_HeaderAskingForAPresetDictionary_ThrowsInvalidDataExceptionNotZLibException()
    {
        // AF-0062: 78 20 sets FDICT, which the BCL inflater refuses with its internal
        // ZLibException, an IOException, rather than an InvalidDataException.
        SshZlibDecompressor decompressor = new();
        Diagnostics.Arrange("packet", "78 20 12 61 64 62 66 61");

        InvalidDataException exception = Assert.ThrowsExactly<InvalidDataException>(() => decompressor.Decompress([0x78, 0x20, 0x12, 0x61, 0x64, 0x62, 0x66, 0x61]));

        Diagnostics.Act("exception", exception.Message);
        Diagnostics.Diff("exception", "The SSH packet is not a valid continuation of the zlib stream.", exception.Message);
        Assert.AreEqual("The SSH packet is not a valid continuation of the zlib stream.", exception.Message);
        Assert.IsInstanceOfType<IOException>(exception.InnerException);
    }

    [TestMethod]
    public void Decompress_BytesThatCompleteNoPayloadByte_Throws()
    {
        SshZlibDecompressor decompressor = new();
        Diagnostics.Arrange("packet", "78 01");

        InvalidDataException exception = Assert.ThrowsExactly<InvalidDataException>(() => decompressor.Decompress([0x78, 0x01]));

        Diagnostics.Act("exception", exception.Message);
        Diagnostics.Diff("exception", "The SSH packet inflates to no payload.", exception.Message);
        Assert.AreEqual("The SSH packet inflates to no payload.", exception.Message);
    }

    [TestMethod]
    public void Decompress_MoreThanTheMaximumPayload_ThrowsAsLibssh2RefusesExcessiveGrowth()
    {
        byte[] bomb = new SshZlibCompressor().Compress(new byte[SshZlibDecompressor.MaximumPayloadLength + 1]);
        SshZlibDecompressor decompressor = new();
        Diagnostics.Arrange("payload length", SshZlibDecompressor.MaximumPayloadLength + 1);
        Diagnostics.Arrange("compressed length", bomb.Length);

        InvalidDataException exception = Assert.ThrowsExactly<InvalidDataException>(() => decompressor.Decompress(bomb));

        Diagnostics.Act("exception", exception.Message);
        Diagnostics.Diff("exception", "The SSH packet inflates to more than 40000 bytes.", exception.Message);
        Assert.AreEqual("The SSH packet inflates to more than 40000 bytes.", exception.Message);
    }

    [TestMethod]
    public void Decompress_TheMaximumPayload_Inflates()
    {
        byte[] payload = new byte[SshZlibDecompressor.MaximumPayloadLength];
        byte[] compressed = new SshZlibCompressor().Compress(payload);
        Diagnostics.Arrange("payload length", payload.Length);
        Diagnostics.Arrange("compressed length", compressed.Length);

        byte[] inflated = new SshZlibDecompressor().Decompress(compressed);

        Diagnostics.Act("inflated length", inflated.Length);
        Diagnostics.Assert("inflated length", payload.Length, inflated.Length);
        Assert.HasCount(payload.Length, inflated);
    }
}
