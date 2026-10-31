namespace Curl.Extended.Protocol.Mqtt5.Encoding;

/// <summary>The Variable Byte Integer of MQTT 5 section 1.5.5: seven bits a byte, at most four bytes.</summary>
internal static class Mqtt5VariableByteInteger
{
    /// <summary>The largest value four bytes hold, 268,435,455.</summary>
    public const int MaximumValue = 268_435_455;

    /// <summary>The most bytes one Variable Byte Integer takes.</summary>
    public const int MaximumLength = 4;

    /// <summary>Appends <paramref name="value"/> in the fewest bytes that hold it.</summary>
    public static void Write(int value, List<byte> destination)
    {
        if (value is < 0 or > MaximumValue)
        {
            throw Mqtt5MalformedPacketException.Malformed($"A Variable Byte Integer holds 0 to {MaximumValue}, not {value}.");
        }

        do
        {
            byte encoded = (byte)(value % 128);
            value /= 128;
            destination.Add(value > 0 ? (byte)(encoded | 0x80) : encoded);
        }
        while (value > 0);
    }

    /// <summary>
    /// Decodes the Variable Byte Integer at the start of <paramref name="source"/>; false when the bytes end before
    /// it does. A fifth byte, or more bytes than the value needs, is a Malformed Packet.
    /// </summary>
    public static bool TryRead(ReadOnlySpan<byte> source, out int value, out int length)
    {
        value = 0;
        for (length = 1; length <= source.Length; length++)
        {
            byte encoded = source[length - 1];
            value |= (encoded & 0x7F) << (7 * (length - 1));
            if (encoded < 0x80)
            {
                return IsInFewestBytes(encoded, length);
            }

            if (length == MaximumLength)
            {
                throw Mqtt5MalformedPacketException.Malformed("A Variable Byte Integer is longer than four bytes.");
            }
        }

        value = 0;
        length = 0;
        return false;
    }

    private static bool IsInFewestBytes(byte lastByte, int length)
    {
        if (lastByte == 0 && length > 1)
        {
            throw Mqtt5MalformedPacketException.Malformed("A Variable Byte Integer is not in the fewest bytes that hold it.");
        }

        return true;
    }
}
