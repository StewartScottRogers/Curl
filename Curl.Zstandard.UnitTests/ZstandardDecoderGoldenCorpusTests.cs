using System.Buffers;
using System.Text;

namespace Curl.Zstandard;

/// <summary>
/// Decodes the reference implementation's golden frames (github.com/facebook/zstd
/// <c>tests/golden-decompression/</c> and <c>tests/golden-decompression-errors/</c>, copied
/// with their provenance and licence in <c>GoldenDecompression/README.md</c>) that hold no
/// sequences (BL-859). They are embedded resources, so the tests touch no file.
/// </summary>
[TestClass]
public sealed class ZstandardDecoderGoldenCorpusTests
{
    public static IEnumerable<object[]> DecodableFrames =>
    [
        ["zeroSeq_2B.zst", Encoding.ASCII.GetBytes("Hello World!\n")],
        ["empty-block.zst", Array.Empty<byte>()],
        ["block-128k.zst", new byte[131068]],
        ["rle-first-block.zst", new byte[1024 * 1024]],
    ];

    [TestMethod]
    [DynamicData(nameof(DecodableFrames))]
    public void TryDecompress_GoldenFrame_WritesItsContent(string name, byte[] expected)
    {
        var destination = new byte[expected.Length];

        var decoded = ZstandardDecoder.TryDecompress(ReadResource(name), destination, out var bytesWritten);

        Assert.IsTrue(decoded, name);
        Assert.AreEqual(expected.Length, bytesWritten, name);
        CollectionAssert.AreEqual(expected, destination, name);
    }

    [TestMethod]
    [DataRow("truncated_huff_state.zst")]
    [DataRow("zeroSeq_extraneous.zst")]
    public void Decompress_GoldenErrorFrame_IsCorruptionDetected(string name)
    {
        var decoder = new ZstandardDecoder();

        var status = decoder.Decompress(ReadResource(name), new byte[64], out _, out _);

        Assert.AreEqual(OperationStatus.InvalidData, status, name);
        Assert.AreEqual(ZstandardDecodeError.CorruptionDetected, decoder.LastError, name);
    }

    private static byte[] ReadResource(string name)
    {
        using var stream = typeof(ZstandardDecoderGoldenCorpusTests).Assembly.GetManifestResourceStream(name)!;
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}
