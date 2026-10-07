using Curl.Testing;

namespace Curl.Protocol.Ssh.Compression;

[TestClass]
public sealed class SshZlibCompressorTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Compress_FirstPayload_StartsTheZlibStreamAndEndsWithASyncFlush()
    {
        SshZlibCompressor compressor = new();
        Diagnostics.Arrange("payload", "hello");

        byte[] compressed = compressor.Compress("hello"u8);

        Diagnostics.Bytes("compressed", compressed);
        Diagnostics.Act("compressed length", compressed.Length);
        Diagnostics.Diff("compressed", Convert.FromHexString("789CCA48CDC9C907000000FFFF"), compressed);
        CollectionAssert.AreEqual(Convert.FromHexString("789CCA48CDC9C907000000FFFF"), compressed, "zlib header at the default level, then the sync flush's empty stored block");
    }

    [TestMethod]
    public void Compress_SeveralPayloads_EachInflatesFromItsOwnPacketAndLaterOnesReferToEarlierOnes()
    {
        SshZlibCompressor compressor = new();
        SshZlibDecompressor decompressor = new();
        Diagnostics.Arrange("payloads", "hello, hello world, hello world");

        byte[] first = compressor.Compress("hello"u8);
        byte[] second = compressor.Compress("hello world"u8);
        byte[] third = compressor.Compress("hello world"u8);

        Diagnostics.Bytes("first packet", first);
        Diagnostics.Bytes("second packet", second);
        Diagnostics.Bytes("third packet", third);
        Diagnostics.Act("third packet length", third.Length);
        Diagnostics.Assert("third packet shorter than 11 bytes", true, third.Length < 11);
        CollectionAssert.AreEqual("hello"u8.ToArray(), decompressor.Decompress(first));
        CollectionAssert.AreEqual("hello world"u8.ToArray(), decompressor.Decompress(second));
        CollectionAssert.AreEqual("hello world"u8.ToArray(), decompressor.Decompress(third));
        Assert.IsLessThan(11, third.Length, "the stream lasts across packets, so a repeat is a back-reference");
    }
}
