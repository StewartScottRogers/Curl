using System.Text;

namespace Curl.Extension.Mqtt5.Encoding;

/// <summary>The UTF-8 Encoded String of MQTT 5 section 1.5.4: well-formed UTF-8, no U+0000, at most 65,535 bytes.</summary>
internal static class Mqtt5Utf8String
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Encodes <paramref name="value"/>; a lone surrogate, U+0000 or more than 65,535 bytes is refused.</summary>
    public static byte[] Encode(string value)
    {
        byte[] bytes;
        try
        {
            bytes = StrictUtf8.GetBytes(value);
        }
        catch (EncoderFallbackException exception)
        {
            throw Mqtt5MalformedPacketException.Malformed($"A UTF-8 Encoded String holds a lone surrogate: {exception.Message}");
        }

        CheckNoNullCharacter(value);
        return bytes.Length <= ushort.MaxValue
            ? bytes
            : throw Mqtt5MalformedPacketException.Malformed($"A UTF-8 Encoded String is {bytes.Length} bytes, more than 65,535.");
    }

    /// <summary>Decodes <paramref name="bytes"/>; ill-formed UTF-8 or U+0000 is a Malformed Packet.</summary>
    public static string Decode(ReadOnlySpan<byte> bytes)
    {
        string value;
        try
        {
            value = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw Mqtt5MalformedPacketException.Malformed($"A UTF-8 Encoded String is not well-formed UTF-8: {exception.Message}");
        }

        CheckNoNullCharacter(value);
        return value;
    }

    private static void CheckNoNullCharacter(string value)
    {
        if (value.Contains('\0', StringComparison.Ordinal))
        {
            throw Mqtt5MalformedPacketException.Malformed("A UTF-8 Encoded String holds U+0000.");
        }
    }
}
