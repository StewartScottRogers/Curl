using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-200 (BL-3729): a <c>deflate</c> body that is plain text fails with zlib's own text
/// (measured, curl 8.22.0: <c>Error while processing content unencoding: invalid code lengths set</c>).
/// </summary>
[TestClass]
public sealed class Cgr200Tests
{
    [TestMethod]
    public void Decode_TextSentAsDeflate_FailsWithInvalidCodeLengthsSet()
    {
        HttpTransferException error = Fail(Encoding.ASCII.GetBytes("this is not compressed at all"));

        Assert.AreEqual(CurlExitCode.BadContentEncoding, error.ExitCode);
        Assert.AreEqual("Error while processing content unencoding: invalid code lengths set", error.Message);
    }

    [TestMethod]
    public void Decode_DynamicBlockCountingTooManySymbols_FailsWithZlibText()
    {
        HttpTransferException error = Fail([0xFC, 0, 0, 0]);

        Assert.AreEqual("Error while processing content unencoding: too many length or distance symbols", error.Message);
    }

    [TestMethod]
    public void Decode_OverSubscribedCodeLengthCode_FailsWithInvalidCodeLengthsSet()
    {
        HttpTransferException error = Fail([0x04, 0x00, 0x92, 0x04]);

        Assert.AreEqual("Error while processing content unencoding: invalid code lengths set", error.Message);
    }

    [TestMethod]
    public void Decode_DynamicBlockHeaderStillArriving_WaitsForMoreBytes()
    {
        using HttpContentCodingDecoder decoder = new(HttpContentCoding.Deflate);

        Assert.AreEqual(0, decoder.Decode(new byte[] { 0x74, 0x68, 0x69 }).Count());
    }

    [TestMethod]
    public void Decode_RealDynamicBlock_StillDecodes()
    {
        byte[] plain = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Range(0, 400).Select(i => "line " + (i * 7919 % 1000) + "\n")));
        using MemoryStream compressed = new();
        using (System.IO.Compression.DeflateStream deflate = new(compressed, System.IO.Compression.CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(plain);
        }

        using HttpContentCodingDecoder decoder = new(HttpContentCoding.Deflate);
        byte[] decoded = [.. decoder.Decode(compressed.ToArray()).SelectMany(chunk => chunk.ToArray())];

        CollectionAssert.AreEqual(plain, decoded);
    }

    private static HttpTransferException Fail(byte[] body)
    {
        using HttpContentCodingDecoder decoder = new(HttpContentCoding.Deflate);
        return Assert.Throws<HttpTransferException>(() => decoder.Decode(body).ToList());
    }
}
