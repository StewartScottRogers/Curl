namespace Curl.Extension.CompressionDictionary.Brotli;

/// <summary>The literal context ID from the last two bytes under each context mode (RFC 7932 section 7.1).</summary>
internal static class BrotliLiteralContext
{
    private static readonly byte[] Utf8LastByte = BuildUtf8LastByte();

    private static readonly byte[] Utf8SecondLastByte = BuildUtf8SecondLastByte();

    private static readonly byte[] SignedByte = BuildSignedByte();

    /// <summary>The context ID, 0 to 63, for mode LSB6 (0), MSB6 (1), UTF8 (2) or Signed (3).</summary>
    public static int Of(int mode, byte last, byte secondLast) => mode switch
    {
        0 => last & 0x3F,
        1 => last >> 2,
        2 => Utf8LastByte[last] | Utf8SecondLastByte[secondLast],
        _ => (SignedByte[last] << 3) | SignedByte[secondLast],
    };

    // Lut0 of section 7.1.
    private static byte[] BuildUtf8LastByte()
    {
        byte[] ascii =
        [
            0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 4, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            8, 12, 16, 12, 12, 20, 12, 16, 24, 28, 12, 12, 32, 12, 36, 12, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 32, 32, 24, 40, 28, 12,
            12, 48, 52, 52, 52, 48, 52, 52, 52, 48, 52, 52, 52, 52, 52, 48, 52, 52, 52, 52, 52, 48, 52, 52, 52, 52, 52, 24, 12, 28, 12, 12,
            12, 56, 60, 60, 60, 56, 60, 60, 60, 56, 60, 60, 60, 60, 60, 56, 60, 60, 60, 60, 60, 56, 60, 60, 60, 60, 60, 24, 12, 28, 12, 0,
        ];
        var table = new byte[256];
        ascii.CopyTo(table, 0);
        for (var value = 128; value < 256; value++)
        {
            table[value] = (byte)((value & 1) + (value >= 192 ? 2 : 0));
        }

        return table;
    }

    // Lut1 of section 7.1.
    private static byte[] BuildUtf8SecondLastByte()
    {
        byte[] ascii =
        [
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1,
            1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 1, 1, 1,
            1, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 1, 1, 1, 1, 0,
        ];
        var table = new byte[256];
        ascii.CopyTo(table, 0);
        table.AsSpan(192).Fill(2);
        return table;
    }

    // Lut2 of section 7.1: 0, then 1 x15, 2 x48, 3 x64, 4 x64, 5 x48, 6 x15, 7.
    private static byte[] BuildSignedByte()
    {
        int[] firstValueOfEachClass = [0, 1, 16, 64, 128, 192, 240, 255];
        var table = new byte[256];
        for (var signedClass = 1; signedClass < firstValueOfEachClass.Length; signedClass++)
        {
            table.AsSpan(firstValueOfEachClass[signedClass]).Fill((byte)signedClass);
        }

        return table;
    }
}
