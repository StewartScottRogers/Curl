namespace Curl.Extension.CompressionDictionary.Brotli;

/// <summary>
/// A hand-built Brotli decoder (RFC 7932) that can decode against a prefix dictionary, as the <c>dcb</c> coding needs
/// (RFC 9842 section 4); .NET's own Brotli decoder cannot take a dictionary.
/// </summary>
public static class BrotliDictionaryDecoder
{
    /// <summary>Decodes a whole Brotli stream with no prefix dictionary.</summary>
    /// <exception cref="BrotliFormatException">The stream is truncated, corrupt or followed by more bytes.</exception>
    public static byte[] Decode(ReadOnlySpan<byte> compressed) => Decode(compressed, []);

    /// <summary>
    /// Decodes a whole Brotli stream compressed against <paramref name="prefixDictionary"/>: distances past the bytes
    /// decoded so far (up to the window) reach back into the dictionary, then into the static dictionary.
    /// </summary>
    /// <exception cref="BrotliFormatException">The stream is truncated, corrupt or followed by more bytes.</exception>
    public static byte[] Decode(ReadOnlySpan<byte> compressed, ReadOnlySpan<byte> prefixDictionary)
    {
        var reader = new BrotliBitReader(compressed.ToArray());
        var output = new BrotliOutput(prefixDictionary.ToArray());
        var distances = new BrotliDistances();
        var maximumBackwardDistance = BrotliLengthCodes.ReadMaximumBackwardDistance(reader);
        var isLast = false;
        while (!isLast)
        {
            isLast = DecodeMetaBlock(reader, output, distances, maximumBackwardDistance);
        }

        reader.AlignToByte();
        if (!reader.IsAtEnd)
        {
            throw new BrotliFormatException("Bytes follow the Brotli stream's last meta-block (RFC 7932 section 9.2).");
        }

        return output.ToArray();
    }

    // Section 9.2: ISLAST, ISLASTEMPTY, MNIBBLES, then metadata, an uncompressed or a compressed meta-block.
    private static bool DecodeMetaBlock(BrotliBitReader reader, BrotliOutput output, BrotliDistances distances, int maximumBackwardDistance)
    {
        var isLast = reader.ReadBit() == 1;
        if (isLast && reader.ReadBit() == 1)
        {
            return true;
        }

        var nibbleCode = reader.ReadBits(2);
        if (nibbleCode == 3)
        {
            SkipMetadata(reader, isLast);
            return false;
        }

        var length = ReadLength(reader, (nibbleCode + 4) * 4, 4, "MLEN") + 1;
        if (!isLast && reader.ReadBit() == 1)
        {
            reader.AlignToByte();
            output.Append(reader.ReadBytes(length));
        }
        else
        {
            new BrotliCompressedMetaBlock(reader, output, distances, maximumBackwardDistance).Decode(length);
        }

        return isLast;
    }

    private static void SkipMetadata(BrotliBitReader reader, bool isLast)
    {
        if (isLast || reader.ReadBit() != 0)
        {
            throw new BrotliFormatException("A Brotli metadata block is marked last or sets its reserved bit (RFC 7932 section 9.2).");
        }

        var byteCount = reader.ReadBits(2);
        var length = byteCount == 0 ? 0 : ReadLength(reader, byteCount * 8, 8, "MSKIPLEN") + 1;
        reader.AlignToByte();
        reader.ReadBytes(length);
    }

    // A length of more than the shortest must not end in a zero nibble (MLEN) or byte (MSKIPLEN).
    private static int ReadLength(BrotliBitReader reader, int bitCount, int unitBits, string field)
    {
        var value = reader.ReadBits(bitCount);
        if (bitCount > 24 - (2 * unitBits) && value >> (bitCount - unitBits) == 0)
        {
            throw new BrotliFormatException($"A Brotli {field} ends in a needless zero (RFC 7932 section 9.2).");
        }

        return value;
    }
}
