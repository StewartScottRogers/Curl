namespace Curl.Extension.CompressionDictionary.Brotli;

/// <summary>Looks up a word of the static dictionary and applies one of its 121 transforms (RFC 7932 section 8 and Appendix B).</summary>
internal static class BrotliStaticDictionary
{
    /// <summary>The shortest word length.</summary>
    public const int MinimumWordLength = 4;

    /// <summary>The longest word length.</summary>
    public const int MaximumWordLength = 24;

    private const int TransformCount = 121;

    // NDBITS of section 8, by word length.
    private static readonly int[] WordIdBits = [0, 0, 0, 0, 10, 10, 11, 11, 10, 10, 10, 10, 10, 9, 9, 8, 7, 7, 8, 7, 7, 6, 6, 5, 5];

    private static readonly int[] WordOffsets = BuildWordOffsets();

    private static readonly byte[][] Affixes =
    [
        " "u8.ToArray(), ", "u8.ToArray(), " of the "u8.ToArray(), " of "u8.ToArray(), "s "u8.ToArray(), "."u8.ToArray(),
        " and "u8.ToArray(), " in "u8.ToArray(), "\""u8.ToArray(), " to "u8.ToArray(), "\">"u8.ToArray(), "\n"u8.ToArray(),
        ". "u8.ToArray(), "]"u8.ToArray(), " for "u8.ToArray(), " a "u8.ToArray(), " that "u8.ToArray(), "'"u8.ToArray(),
        " with "u8.ToArray(), " from "u8.ToArray(), " by "u8.ToArray(), "("u8.ToArray(), ". The "u8.ToArray(), " on "u8.ToArray(),
        " as "u8.ToArray(), " is "u8.ToArray(), "ing "u8.ToArray(), "\n\t"u8.ToArray(), ":"u8.ToArray(), "ed "u8.ToArray(),
        "=\""u8.ToArray(), " at "u8.ToArray(), "ly "u8.ToArray(), ","u8.ToArray(), "='"u8.ToArray(), ".com/"u8.ToArray(),
        ". This "u8.ToArray(), " not "u8.ToArray(), "er "u8.ToArray(), "al "u8.ToArray(), "ful "u8.ToArray(), "ive "u8.ToArray(),
        "less "u8.ToArray(), "est "u8.ToArray(), "ize "u8.ToArray(), " "u8.ToArray(), "ous "u8.ToArray(), " the "u8.ToArray(),
        "e "u8.ToArray(), [],
    ];

    // Appendix B as (prefix affix, transform type, suffix affix) triples. Types: 0 Identity, 1-9 OmitLast1-9,
    // 10 UppercaseFirst, 11 UppercaseAll, 12-20 OmitFirst1-9.
    private static readonly byte[] Transforms =
    [
        49, 0, 49, 49, 0, 0, 0, 0, 0, 49, 12, 49, 49, 10, 0, 49, 0, 47, 0, 0, 49, 4, 0, 0, 49, 0, 3, 49, 10, 49, 49, 0, 6, 49, 13, 49,
        49, 1, 49, 1, 0, 0, 49, 0, 1, 0, 10, 0, 49, 0, 7, 49, 0, 9, 48, 0, 0, 49, 0, 8, 49, 0, 5, 49, 0, 10, 49, 0, 11, 49, 3, 49,
        49, 0, 13, 49, 0, 14, 49, 14, 49, 49, 2, 49, 49, 0, 15, 49, 0, 16, 0, 10, 49, 49, 0, 12, 5, 0, 49, 0, 0, 1, 49, 15, 49, 49,
        0, 18, 49, 0, 17, 49, 0, 19, 49, 0, 20, 49, 16, 49, 49, 17, 49, 47, 0, 49, 49, 4, 49, 49, 0, 22, 49, 11, 49, 49, 0, 23, 49, 0,
        24, 49, 0, 25, 49, 7, 49, 49, 1, 26, 49, 0, 27, 49, 0, 28, 0, 0, 12, 49, 0, 29, 49, 20, 49, 49, 18, 49, 49, 6, 49, 49, 0, 21,
        49, 10, 1, 49, 8, 49, 49, 0, 31, 49, 0, 32, 47, 0, 3, 49, 5, 49, 49, 9, 49, 0, 10, 1, 49, 10, 8, 5, 0, 21, 49, 11, 0, 49, 10,
        10, 49, 0, 30, 0, 0, 5, 35, 0, 49, 47, 0, 2, 49, 10, 17, 49, 0, 36, 49, 0, 33, 5, 0, 0, 49, 10, 21, 49, 10, 5, 49, 0, 37, 0,
        0, 30, 49, 0, 38, 0, 11, 0, 49, 0, 39, 0, 11, 49, 49, 0, 34, 49, 11, 8, 49, 10, 12, 0, 0, 21, 49, 0, 40, 0, 10, 12, 49, 0,
        41, 49, 0, 42, 49, 11, 17, 49, 0, 43, 0, 10, 5, 49, 11, 10, 0, 0, 34, 49, 10, 33, 49, 0, 44, 49, 11, 5, 45, 0, 49, 0, 0, 33,
        49, 10, 30, 49, 11, 30, 49, 0, 46, 49, 11, 1, 49, 10, 34, 0, 10, 33, 0, 11, 30, 0, 11, 1, 49, 11, 33, 49, 11, 21, 49, 11, 12,
        0, 11, 5, 49, 11, 34, 0, 11, 12, 0, 10, 30, 0, 11, 34, 0, 10, 34,
    ];

    private static readonly byte[] Words = Convert.FromBase64String(BrotliStaticDictionaryData.Base64);

    /// <summary>The 122,784 dictionary bytes.</summary>
    public static ReadOnlySpan<byte> Data => Words;

    /// <summary>Writes the transformed word that a <paramref name="reference"/> past the reachable history names, for a copy of <paramref name="length"/> bytes.</summary>
    public static void WriteWord(BrotliOutput output, int length, int reference)
    {
        if (length < MinimumWordLength || length > MaximumWordLength)
        {
            throw new BrotliFormatException("A Brotli distance names the static dictionary with a copy length outside 4 to 24 (RFC 7932 section 8).");
        }

        var wordId = reference & ((1 << WordIdBits[length]) - 1);
        var transformId = reference >> WordIdBits[length];
        if (transformId >= TransformCount)
        {
            throw new BrotliFormatException("A Brotli static dictionary reference names a transform past 120 (RFC 7932 section 8).");
        }

        var word = Data.Slice(WordOffsets[length] + (wordId * length), length);
        output.Append(Affixes[Transforms[transformId * 3]]);
        output.Append(Transform(word, Transforms[(transformId * 3) + 1]));
        output.Append(Affixes[Transforms[(transformId * 3) + 2]]);
    }

    private static ReadOnlySpan<byte> Transform(ReadOnlySpan<byte> word, int type)
    {
        var (start, end) = KeptRange(word.Length, type);

        // Two spare bytes let the uppercasing of a multi-byte character run past a cut-short word, as section 8's pseudo-code does.
        var body = new byte[end - start + 2];
        word[start..end].CopyTo(body);
        var bodyLength = end - start;
        var uppercaseLength = UppercaseLength(bodyLength, type);
        for (var index = 0; index < uppercaseLength;)
        {
            index += UppercaseAt(body, index);
        }

        return body.AsSpan(0, bodyLength);
    }

    // OmitLast1-9 are types 1-9, OmitFirst1-9 types 12-20.
    private static (int Start, int End) KeptRange(int length, int type)
    {
        var omitLast = type is >= 1 and <= 9 ? type : 0;
        var start = Math.Min(type >= 12 ? type - 11 : 0, length);
        return (start, Math.Max(length - omitLast, start));
    }

    // UppercaseFirst is type 10, UppercaseAll type 11.
    private static int UppercaseLength(int length, int type) => type switch
    {
        10 => Math.Min(length, 1),
        11 => length,
        _ => 0,
    };

    // ToUpperCase of section 8: ASCII letters flip bit 5, two-byte UTF-8 flips the second byte's bit 5, longer ones the third byte's bits 0 and 2.
    private static int UppercaseAt(byte[] body, int index)
    {
        var first = body[index];
        if (first >= 0xE0)
        {
            body[index + 2] ^= 5;
            return 3;
        }

        if (first >= 0xC0)
        {
            body[index + 1] ^= 32;
            return 2;
        }

        body[index] ^= (byte)(first is >= (byte)'a' and <= (byte)'z' ? 32 : 0);
        return 1;
    }

    private static int[] BuildWordOffsets()
    {
        var offsets = new int[MaximumWordLength + 1];
        for (var length = MinimumWordLength; length < MaximumWordLength; length++)
        {
            offsets[length + 1] = offsets[length] + (length << WordIdBits[length]);
        }

        return offsets;
    }
}
