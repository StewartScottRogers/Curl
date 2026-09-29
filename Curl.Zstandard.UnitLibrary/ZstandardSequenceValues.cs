namespace Curl.Zstandard;

/// <summary>
/// Turns a sequence's three codes into their values (RFC 8878 section 3.1.1.3.2.1.1): a
/// baseline for the code plus as many extra bits, read from the sequence bitstream, as the
/// code calls for.
/// </summary>
internal static class ZstandardSequenceValues
{
    /// <summary>Literals length codes 0 to 35: the baseline of each.</summary>
    private static ReadOnlySpan<int> LiteralsLengthBaselines =>
    [
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
        16, 18, 20, 22, 24, 28, 32, 40, 48, 64, 128, 256, 512, 1024, 2048, 4096,
        8192, 16384, 32768, 65536,
    ];

    /// <summary>Literals length codes 0 to 35: the extra bits of each.</summary>
    private static ReadOnlySpan<byte> LiteralsLengthExtraBits =>
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, 1, 1, 2, 2, 3, 3, 4, 6, 7, 8, 9, 10, 11, 12,
        13, 14, 15, 16,
    ];

    /// <summary>Match length codes 0 to 52: the baseline of each.</summary>
    private static ReadOnlySpan<int> MatchLengthBaselines =>
    [
        3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18,
        19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34,
        35, 37, 39, 41, 43, 47, 51, 59, 67, 83, 99, 131, 259, 515, 1027, 2051,
        4099, 8195, 16387, 32771, 65539,
    ];

    /// <summary>Match length codes 0 to 52: the extra bits of each.</summary>
    private static ReadOnlySpan<byte> MatchLengthExtraBits =>
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, 1, 1, 2, 2, 3, 3, 4, 4, 5, 7, 8, 9, 10, 11,
        12, 13, 14, 15, 16,
    ];

    /// <summary>Reads the literals length of <paramref name="code" />.</summary>
    public static int ReadLiteralsLength(int code, ref ZstandardBackwardBitReader bits) =>
        LiteralsLengthBaselines[code] + (int)bits.ReadBits(LiteralsLengthExtraBits[code]);

    /// <summary>Reads the match length of <paramref name="code" />.</summary>
    public static int ReadMatchLength(int code, ref ZstandardBackwardBitReader bits) =>
        MatchLengthBaselines[code] + (int)bits.ReadBits(MatchLengthExtraBits[code]);

    /// <summary>Reads the <c>Offset_Value</c> of <paramref name="code" />: <c>(1 &lt;&lt; code)</c> plus <paramref name="code" /> extra bits.</summary>
    public static long ReadOffsetValue(int code, ref ZstandardBackwardBitReader bits) =>
        (1L << code) + bits.ReadBits(code);
}
