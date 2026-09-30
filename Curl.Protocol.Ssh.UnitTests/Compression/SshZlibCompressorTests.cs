namespace Curl.Protocol.Ssh.Compression;

[TestClass]
public sealed class SshZlibCompressorTests
{
    [TestMethod]
    public void Compress_FirstPayload_StartsTheZlibStreamAndEndsWithASyncFlush()
    {
        SshZlibCompressor compressor = new();

        byte[] compressed = compressor.Compress("hello"u8);

        CollectionAssert.AreEqual(Convert.FromHexString("789CCA48CDC9C907000000FFFF"), compressed, "zlib header at the default level, then the sync flush's empty stored block");
    }

    [TestMethod]
    public void Compress_SeveralPayloads_EachInflatesFromItsOwnPacketAndLaterOnesReferToEarlierOnes()
    {
        SshZlibCompressor compressor = new();
        SshZlibDecompressor decompressor = new();

        byte[] first = compressor.Compress("hello"u8);
        byte[] second = compressor.Compress("hello world"u8);
        byte[] third = compressor.Compress("hello world"u8);

        CollectionAssert.AreEqual("hello"u8.ToArray(), decompressor.Decompress(first));
        CollectionAssert.AreEqual("hello world"u8.ToArray(), decompressor.Decompress(second));
        CollectionAssert.AreEqual("hello world"u8.ToArray(), decompressor.Decompress(third));
        Assert.IsLessThan(11, third.Length, "the stream lasts across packets, so a repeat is a back-reference");
    }
}
