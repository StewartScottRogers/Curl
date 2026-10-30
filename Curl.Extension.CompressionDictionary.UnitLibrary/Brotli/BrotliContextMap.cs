namespace Curl.Extension.CompressionDictionary.Brotli;

/// <summary>Reads a context map, which picks a prefix code for each block type and context (RFC 7932 section 7.3).</summary>
internal static class BrotliContextMap
{
    /// <summary>Reads NTREES and a map of <paramref name="size"/> entries, each below NTREES.</summary>
    public static byte[] Read(BrotliBitReader reader, int size, out int treeCount)
    {
        treeCount = BrotliLengthCodes.ReadVariableLengthByte(reader) + 1;
        var map = new byte[size];
        if (treeCount == 1)
        {
            return map;
        }

        var maximumRunLengthPrefix = reader.ReadBit() == 1 ? reader.ReadBits(4) + 1 : 0;
        var code = BrotliPrefixCodeReader.Read(reader, treeCount + maximumRunLengthPrefix);
        for (var index = 0; index < size;)
        {
            var symbol = code.Decode(reader);
            if ((uint)(symbol - 1) < (uint)maximumRunLengthPrefix)
            {
                index += ReadZeroRun(reader, symbol, size - index);
            }
            else
            {
                map[index++] = (byte)Math.Max(symbol - maximumRunLengthPrefix, 0);
            }
        }

        if (reader.ReadBit() == 1)
        {
            InverseMoveToFront(map);
        }

        return map;
    }

    private static int ReadZeroRun(BrotliBitReader reader, int symbol, int room)
    {
        var run = (1 << symbol) + reader.ReadBits(symbol);
        if (run > room)
        {
            throw new BrotliFormatException("A Brotli context map's run of zeros runs past its end (RFC 7932 section 7.3).");
        }

        return run;
    }

    private static void InverseMoveToFront(byte[] map)
    {
        var order = new byte[256];
        for (var value = 0; value < order.Length; value++)
        {
            order[value] = (byte)value;
        }

        for (var index = 0; index < map.Length; index++)
        {
            var position = map[index];
            var value = order[position];
            map[index] = value;
            Array.Copy(order, 0, order, 1, position);
            order[0] = value;
        }
    }
}
