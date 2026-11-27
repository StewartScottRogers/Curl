using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins BL-5889: a <c>deflate</c> zlib stream that turns corrupt in the first piece of the
/// body hands out nothing and fails with zlib's raw deflate text, as curl 8.22.0 does
/// (measured, <c>--compressed -o</c> against a loopback server).
/// </summary>
[TestClass]
public sealed class CorruptZLibDeflateOutputTests
{
    private const string Text = "The quick brown fox jumps over the lazy dog";

    private static readonly byte[] Compressed = Convert.FromHexString(
        "789C0BC94855282CCD4CCE56482ACA2FCF5348CBAF50C82ACD2D2856C82F4B2D5228014AE72456552AA4E4A703005BDC0FDA");

    private static readonly byte[] Garbage = [5, 5, 5, 5, 5, 5, 5, 5];

    [TestMethod]
    public void Decode_ZLibDeflateCorruptAfterTenBytes_HandsOutNothingAndNamesStoredBlockLengths()
    {
        byte[] body = [.. Compressed[..10], .. Garbage, .. Compressed[10..]];
        using HttpContentCodingDecoder decoder = new(HttpContentCoding.Deflate);
        List<byte> decoded = [];

        HttpTransferException error = Assert.ThrowsExactly<HttpTransferException>(() => Collect(decoder, body, decoded));

        Assert.AreEqual(CurlExitCode.BadContentEncoding, error.ExitCode);
        Assert.AreEqual("Error while processing content unencoding: invalid stored block lengths", error.Message);
        Assert.IsEmpty(decoded);
    }

    [TestMethod]
    public void Decode_ZLibDeflateCorruptInASecondPiece_KeepsTheFirstPiecesBytes()
    {
        using HttpContentCodingDecoder decoder = new(HttpContentCoding.Deflate);
        List<byte> decoded = [];
        Collect(decoder, Compressed[..10], decoded);
        int handedOut = decoded.Count;

        HttpTransferException error = Assert.ThrowsExactly<HttpTransferException>(
            () => Collect(decoder, Garbage, decoded));

        Assert.IsGreaterThan(0, handedOut);
        Assert.AreEqual(HttpTransferMessages.InvalidDistanceTooFarBack, error.Message);
    }

    [TestMethod]
    public void Decode_ZLibDeflateWithBadChecksum_HandsOutEveryByteBeforeFailing()
    {
        byte[] body = [.. Compressed[..^1], (byte)(Compressed[^1] ^ 1)];
        using HttpContentCodingDecoder decoder = new(HttpContentCoding.Deflate);
        List<byte> decoded = [];

        HttpTransferException error = Assert.ThrowsExactly<HttpTransferException>(() => Collect(decoder, body, decoded));

        Assert.AreEqual(HttpTransferMessages.IncorrectDataCheck, error.Message);
        Assert.AreEqual(Text, System.Text.Encoding.ASCII.GetString([.. decoded]));
    }

    [TestMethod]
    public void Decode_ZLibDeflateLongerThanOnePiece_HandsOutEveryByte()
    {
        byte[] plain = new byte[(HttpContentCodingDecoder.OutputSize * 2) + 100];
        new Random(1).NextBytes(plain);
        using MemoryStream compressed = new();
        using (System.IO.Compression.ZLibStream zlib = new(compressed, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
        {
            zlib.Write(plain);
        }

        using HttpContentCodingDecoder decoder = new(HttpContentCoding.Deflate);
        List<byte> decoded = [];

        Collect(decoder, compressed.ToArray(), decoded);

        CollectionAssert.AreEqual(plain, decoded);
    }

    private static void Collect(HttpContentCodingDecoder decoder, byte[] encoded, List<byte> decoded)
    {
        foreach (ReadOnlyMemory<byte> piece in decoder.Decode(encoded))
        {
            decoded.AddRange(piece.Span);
        }
    }
}
