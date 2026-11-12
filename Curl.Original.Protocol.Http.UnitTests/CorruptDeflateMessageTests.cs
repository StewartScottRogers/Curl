using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins BL-4931: a corrupt first deflate block fails with zlib's own text for gzip and
/// deflate bodies (measured, curl 8.22.0, <c>--compressed</c> against a loopback server).
/// </summary>
[TestClass]
public sealed class CorruptDeflateMessageTests
{
    private const string Prefix = "Error while processing content unencoding: ";

    [TestMethod]
    public void Decode_GzipHeaderThenReservedBlockType_FailsWithInvalidBlockType()
    {
        byte[] body = [.. new byte[] { 0x1F, 0x8B, 8, 0, 0, 0, 0, 0, 0, 3 }, .. "garbagegarbage"u8.ToArray()];

        Assert.AreEqual(Prefix + "invalid block type", Fail(HttpContentCoding.Gzip, body));
    }

    [TestMethod]
    public void Decode_ShortGzipHeaderThenDynamicBlock_FailsWithInvalidCodeLengthsSet()
    {
        byte[] body = [0x1F, 0x8B, 8, 0, .. "garbagegarbage"u8.ToArray()];

        Assert.AreEqual(Prefix + "invalid code lengths set", Fail(HttpContentCoding.Gzip, body));
    }

    [TestMethod]
    public void Decode_ZLibHeaderThenGarbageAsDeflate_FailsWithInvalidStoredBlockLengths()
    {
        byte[] body = [0x78, 0x9C, 0xFF, 0xFF, 0xFF, .. "garbage"u8.ToArray()];

        Assert.AreEqual(Prefix + "invalid stored block lengths", Fail(HttpContentCoding.Deflate, body));
    }

    [TestMethod]
    public void Decode_GzipHeaderWithFlagsThenGarbage_FailsWithGenericText()
    {
        byte[] body = [0x1F, 0x8B, 8, 8, 0, 0, 0, 0, 0, 3, 0x61, 0, 0xFF, 0xFF, 0xFF, 0xFF];

        Assert.AreEqual(HttpTransferMessages.BadContentEncoding, Fail(HttpContentCoding.Gzip, body));
    }

    [TestMethod]
    public void Decode_ZLibHeaderInGzipThenReservedBlock_FailsWithInvalidBlockType()
    {
        byte[] body = [0x78, 0x9C, 0x07, 0, 0, .. "garbage"u8.ToArray()];

        Assert.AreEqual(Prefix + "invalid block type", Fail(HttpContentCoding.Gzip, body));
    }

    [TestMethod]
    public void Decode_GzipHeaderRefusedBeforeTenBytes_FailsWithGenericText()
    {
        Assert.AreEqual(HttpTransferMessages.BadContentEncoding, Fail(HttpContentCoding.Gzip, [0x1F, 0x8B, 8, 0xE0, 0, 0, 0, 0, 0]));
    }

    [TestMethod]
    public void Decode_FixedBlockWithDistanceTooFarBack_FailsWithGenericText()
    {
        Assert.AreEqual(HttpTransferMessages.BadContentEncoding, Fail(HttpContentCoding.Gzip, [0x78, 0x9C, 0x03, 0x02, 0, 0]));
    }

    [TestMethod]
    public void Decode_StoredBlockWithAgreeingLengthsThenReservedBlock_FailsWithGenericText()
    {
        Assert.AreEqual(HttpTransferMessages.BadContentEncoding, Fail(HttpContentCoding.Gzip, [0x78, 0x9C, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0x07, 0, 0]));
    }

    private static string Fail(HttpContentCoding coding, byte[] body)
    {
        using HttpContentCodingDecoder decoder = new(coding);
        HttpTransferException error = Assert.ThrowsExactly<HttpTransferException>(() => _ = decoder.Decode(body).ToList());
        Assert.AreEqual(CurlExitCode.BadContentEncoding, error.ExitCode);
        return error.Message;
    }
}
