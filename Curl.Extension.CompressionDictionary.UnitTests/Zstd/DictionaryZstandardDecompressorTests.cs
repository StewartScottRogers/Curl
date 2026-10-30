using System.Text;
using Curl.Extension.CompressionDictionary.Zstd;
using Curl.Zstandard;

namespace Curl.Extension.CompressionDictionary.UnitTests.Zstd;

/// <summary>
/// Checks <c>dcz</c> frame decoding (RFC 9842 section 5) against frames real libzstd 1.5.7
/// compressed: <c>ZSTD_compress_usingDict</c> at level 3 (as <c>zstd -D</c> does) and
/// <c>ZSTD_CCtx_refPrefix</c> at level 19 with a checksum (as <c>zstd --patch-from</c> does), each
/// with <see cref="Dictionary" /> as a raw content dictionary, and the same content with no
/// dictionary.
/// </summary>
[TestClass]
public sealed class DictionaryZstandardDecompressorTests
{
    // libzstd ZSTD_compress_usingDict(level 3) of Content against Dictionary.
    internal const string UsingDictionaryFrame =
        "28B52FFD60E105050400B4033030207761732065646974656420666F722076657273696F6E2074776F7265736F7572" +
        "63653132303336373839313031313132363738393230323118001672330514A07810A102942D4065017A0B501E44E6" +
        "D1385341018A15A07810A102942D4065017A0B283FB7227BA8331A14A0A800650B9038291FAFC5E770E10101";

    // libzstd ZSTD_CCtx_refPrefix(Dictionary) with compressionLevel 19 and checksumFlag 1, then ZSTD_compress2 of Content.
    private const string PrefixFrame =
        "28B52FFD64E1054D02003202080ED0E784C8FCF6F24E5A0C1010DA02152FB6DEE7485CF61ADE83D42B2A2D1C100B00" +
        "6C723305010443FF661502088634CE2A04100CD59C2A9CA31018823A15B7C56FACED6621613A79AE";

    // libzstd ZSTD_compress2 of Content, compressionLevel 19 and checksumFlag 1, no dictionary.
    private const string PlainFrame =
        "28B52FFD64E1055D050052481B1950B50E0F6281D1E9515645AD4C91A97F2894A5ACC64C11530310C2071D440EA32006" +
        "01BEF7DC430EA32006013A2DAF6D51AA7CF52D3559D0B36E4EB1AECB23665A55A2D5487B5E66CA4BAEF1B10F0898A956" +
        "5BAEE7E7BEE937764F7BDACAD76B37977CA7E4DB88DDB9339D4A1BA810B0DBA9CF3207809736C301101A2230E2FFFF19" +
        "137C360E0D9D4E5BEB7AC2F51A0B6A9A6E971B1273EE7B0140699928FA24B5CBCF195DD752613A79AE";

    internal static readonly byte[] Dictionary = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Range(0, 24).Select(
        line => $"Line {line:D2} of the shared dictionary: the quick brown fox jumps over the lazy dog.\n")));

    // Every fifth line differs from the dictionary; the rest are matches that reach back into it.
    internal static readonly byte[] Content = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Range(0, 24).Select(
        line => line % 5 == 0
            ? $"Line {line:D2} was edited for version two of the resource.\n"
            : $"Line {line:D2} of the shared dictionary: the quick brown fox jumps over the lazy dog.\n")));

    // RFC 8878 section 5: a raw content dictionary primes the history, so offsets reach into it.
    [TestMethod]
    [DataRow(UsingDictionaryFrame)]
    [DataRow(PrefixFrame)]
    public void Decompress_FrameAgainstRawContentDictionary_IsTheContent(string frame) =>
        CollectionAssert.AreEqual(Content, DictionaryZstandardDecompressor.Decompress(Convert.FromHexString(frame), Dictionary));

    // RFC 8878 section 3.1.1: a frame compressed without a dictionary decodes with or without one.
    [TestMethod]
    public void Decompress_FrameWithoutDictionary_IsTheContent()
    {
        CollectionAssert.AreEqual(Content, DictionaryZstandardDecompressor.Decompress(Convert.FromHexString(PlainFrame), ReadOnlyMemory<byte>.Empty));
        CollectionAssert.AreEqual(Content, DictionaryZstandardDecompressor.Decompress(Convert.FromHexString(PlainFrame), Dictionary));
    }

    // RFC 8878 section 3.1.1: frames follow one another; output larger than the first buffer grows it.
    [TestMethod]
    public void Decompress_ManyFrames_IsEveryFrameInTurn()
    {
        var frame = Convert.FromHexString(PrefixFrame);
        var frames = Enumerable.Repeat(frame, 50).SelectMany(bytes => bytes).ToArray();

        var decoded = DictionaryZstandardDecompressor.Decompress(frames, Dictionary);

        CollectionAssert.AreEqual(Enumerable.Repeat(Content, 50).SelectMany(bytes => bytes).ToArray(), decoded);
    }

    [TestMethod]
    public void Decompress_Empty_IsEmpty() =>
        Assert.IsEmpty(DictionaryZstandardDecompressor.Decompress([], Dictionary));

    // Without the dictionary its matches reach back past the start of the frame (RFC 8878 section 3.1.1.5).
    [TestMethod]
    public void Decompress_DictionaryFrameWithoutDictionary_IsCorruptionDetected()
    {
        var exception = Assert.ThrowsExactly<DictionaryZstandardException>(
            () => DictionaryZstandardDecompressor.Decompress(Convert.FromHexString(UsingDictionaryFrame), ReadOnlyMemory<byte>.Empty));

        Assert.AreEqual(ZstandardDecodeError.CorruptionDetected, exception.Error);
    }

    // RFC 8878 section 3.1.1.6: a checksum over other content fails the frame.
    [TestMethod]
    public void Decompress_ChecksumChanged_IsChecksumWrong()
    {
        var frame = Convert.FromHexString(PrefixFrame);
        frame[^1] ^= 0xFF;

        var exception = Assert.ThrowsExactly<DictionaryZstandardException>(() => DictionaryZstandardDecompressor.Decompress(frame, Dictionary));

        Assert.AreEqual(ZstandardDecodeError.ChecksumWrong, exception.Error);
    }

    // RFC 8878 section 3.1.1: the magic number starts every frame.
    [TestMethod]
    public void Decompress_BadMagic_IsPrefixUnknown()
    {
        var exception = Assert.ThrowsExactly<DictionaryZstandardException>(
            () => DictionaryZstandardDecompressor.Decompress([0x00, 0x01, 0x02, 0x03], Dictionary));

        Assert.AreEqual(ZstandardDecodeError.PrefixUnknown, exception.Error);
        StringAssert.Contains(exception.Message, "PrefixUnknown");
    }

    // RFC 9842 section 5: a dcz frame uses the dictionary as raw content, so it names no Dictionary_ID (RFC 8878 section 3.1.1.1.3).
    [TestMethod]
    public void Decompress_FrameNamingDictionaryId_IsDictionaryWrong()
    {
        var exception = Assert.ThrowsExactly<DictionaryZstandardException>(
            () => DictionaryZstandardDecompressor.Decompress([0x28, 0xB5, 0x2F, 0xFD, 0x01, 0x00, 0x07, 0x01, 0x00, 0x00], Dictionary));

        Assert.AreEqual(ZstandardDecodeError.DictionaryWrong, exception.Error);
    }

    [TestMethod]
    public void Decompress_FrameCutShort_ThrowsWithNoError()
    {
        var frame = Convert.FromHexString(PrefixFrame);

        var exception = Assert.ThrowsExactly<DictionaryZstandardException>(() => DictionaryZstandardDecompressor.Decompress(frame[..^1], Dictionary));

        Assert.IsNull(exception.Error);
        StringAssert.Contains(exception.Message, "middle of a frame");
    }

    // RFC 9842 section 5: the window may be 8 MB or 1.25 times the dictionary, whichever is larger.
    [TestMethod]
    [DataRow(0, 8UL << 20)]
    [DataRow(1 << 20, 8UL << 20)]
    [DataRow(8 << 20, 10UL << 20)]
    public void WindowLimit_DictionaryLength_IsTheLargerOf8MiBAndOneAndAQuarterTimes(int dictionaryLength, ulong expected) =>
        Assert.AreEqual(expected, DictionaryZstandardDecompressor.WindowLimit(dictionaryLength));

    // RFC 8878 section 3.1.1.1.2: window descriptor exponent 13 is 2^23 = 8 MiB, each mantissa step adds 1 MiB.
    // The frame is the header and one empty last raw block.
    [TestMethod]
    [DataRow(0, (byte)0x68, true)]
    [DataRow(0, (byte)0x69, false)]
    [DataRow(8 << 20, (byte)0x6A, true)]
    [DataRow(8 << 20, (byte)0x6B, false)]
    public void Decompress_WindowAgainstLimit_IsAcceptedUpToTheLimit(int dictionaryLength, byte windowDescriptor, bool accepted)
    {
        byte[] frame = [0x28, 0xB5, 0x2F, 0xFD, 0x00, windowDescriptor, 0x01, 0x00, 0x00];
        var dictionary = new byte[dictionaryLength];

        if (accepted)
        {
            Assert.IsEmpty(DictionaryZstandardDecompressor.Decompress(frame, dictionary));
            return;
        }

        var exception = Assert.ThrowsExactly<DictionaryZstandardException>(() => DictionaryZstandardDecompressor.Decompress(frame, dictionary));
        Assert.AreEqual(ZstandardDecodeError.FrameParameterWindowTooLarge, exception.Error);
    }
}
