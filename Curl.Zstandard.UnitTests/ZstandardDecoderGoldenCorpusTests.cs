using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace Curl.Zstandard;

/// <summary>
/// Decodes every frame of the reference implementation's golden corpus (github.com/facebook/zstd
/// <c>tests/golden-decompression/</c> and <c>tests/golden-decompression-errors/</c>, copied
/// with their provenance and licence in <c>GoldenDecompression/README.md</c>), and frames
/// libzstd 1.5.7 compressed from known content (<c>LibzstdFrames/README.md</c>), whole and
/// a byte at a time (BL-859, BL-860). They are embedded resources, so the tests touch no file.
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

    public static IEnumerable<object[]> ErrorFrames =>
    [
        ["off0.bin.zst"],
        ["truncated_huff_state.zst"],
        ["zeroSeq_extraneous.zst"],
    ];

    /// <summary>Each libzstd frame, the length of the content it was compressed from, and that content's SHA-256.</summary>
    public static IEnumerable<object[]> LibzstdFrames =>
    [
        ["readme-level3.zst", 2765, "012d4024b8945fd43d47613cd45aadd7f52a7704b07dbc509a57acecdc03fbc6"],
        ["source-level19.zst", 61181, "9907bf43d276cc05bb9b8eb4cf22dfdaa2466e6aa868f6af0e76ec420b7d5e00"],
        ["source-level-5.zst", 61181, "9907bf43d276cc05bb9b8eb4cf22dfdaa2466e6aa868f6af0e76ec420b7d5e00"],
        ["decisions-level3.zst", 400000, "9fed8db9b344171a577aa1201857b14cea58010f94a5b5885d2cd0db4a3743ab"],
        ["binary-level9.zst", 100000, "fbb59356c9bd0270705ae453f01ac0bad5ea910a495ccfa9d45af408a28abdc8"],
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
    [DynamicData(nameof(DecodableFrames))]
    public void Decompress_GoldenFrameOneByteAtATime_WritesItsContent(string name, byte[] expected)
    {
        CollectionAssert.AreEqual(expected, DecompressOneByteAtATime(ReadResource(name), expected.Length, out var status), name);
        Assert.AreEqual(OperationStatus.Done, status, name);
    }

    [TestMethod]
    [DynamicData(nameof(ErrorFrames))]
    public void TryDecompress_GoldenErrorFrame_IsFalse(string name)
    {
        Assert.IsFalse(ZstandardDecoder.TryDecompress(ReadResource(name), new byte[64], out _), name);
    }

    [TestMethod]
    [DynamicData(nameof(ErrorFrames))]
    public void Decompress_GoldenErrorFrameOneByteAtATime_IsCorruptionDetected(string name)
    {
        var decoder = new ZstandardDecoder();
        var source = ReadResource(name);
        var status = OperationStatus.NeedMoreData;
        for (var position = 0; position < source.Length && status != OperationStatus.InvalidData; position++)
        {
            status = decoder.Decompress(source.AsSpan(position, 1), new byte[64], out _, out _);
        }

        Assert.AreEqual(OperationStatus.InvalidData, status, name);
        Assert.AreEqual(ZstandardDecodeError.CorruptionDetected, decoder.LastError, name);
    }

    [TestMethod]
    [DynamicData(nameof(LibzstdFrames))]
    public void TryDecompress_LibzstdFrame_WritesTheContentItWasCompressedFrom(string name, int length, string sha256)
    {
        var destination = new byte[length];

        var decoded = ZstandardDecoder.TryDecompress(ReadResource(name), destination, out var bytesWritten);

        Assert.IsTrue(decoded, name);
        Assert.AreEqual(length, bytesWritten, name);
        Assert.AreEqual(sha256, Convert.ToHexStringLower(SHA256.HashData(destination)), name);
    }

    [TestMethod]
    [DynamicData(nameof(LibzstdFrames))]
    public void Decompress_LibzstdFrameOneByteAtATime_WritesTheContentItWasCompressedFrom(string name, int length, string sha256)
    {
        var output = DecompressOneByteAtATime(ReadResource(name), length, out var status);

        Assert.AreEqual(OperationStatus.Done, status, name);
        Assert.AreEqual(length, output.Length, name);
        Assert.AreEqual(sha256, Convert.ToHexStringLower(SHA256.HashData(output)), name);
    }

    /// <summary>Feeds <paramref name="source" /> one byte at a time, taking output in pieces of at most 4 KiB.</summary>
    private static byte[] DecompressOneByteAtATime(byte[] source, int length, out OperationStatus status)
    {
        var decoder = new ZstandardDecoder();
        var output = new byte[length];
        var written = 0;
        var position = 0;
        do
        {
            var piece = output.AsSpan(written, Math.Min(4096, length - written));
            status = decoder.Decompress(source.AsSpan(position, Math.Min(1, source.Length - position)), piece, out var consumed, out var pieceWritten);
            position += consumed;
            written += pieceWritten;
        }
        while (status != OperationStatus.InvalidData && (position < source.Length || status == OperationStatus.DestinationTooSmall));

        return output[..written];
    }

    private static byte[] ReadResource(string name)
    {
        using var stream = typeof(ZstandardDecoderGoldenCorpusTests).Assembly.GetManifestResourceStream(name)!;
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}
