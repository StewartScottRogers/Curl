using System.Text;

namespace Curl.Conformance.Extended.Mqtt5;

/// <summary>
/// Reads the data types of MQTT 5.0 section 1.5 from a packet body, written from the specification and
/// not from the client library, and raises <see cref="Mqtt5ConformanceViolationException"/> for any
/// value the section forbids.
/// </summary>
/// <param name="body">The bytes to read.</param>
public sealed class Mqtt5WireReader(byte[] body)
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private int position;

    /// <summary>Gets whether every byte has been read.</summary>
    public bool AtEnd => position == body.Length;

    /// <summary>Reads a Byte (section 1.5.1).</summary>
    /// <returns>The byte.</returns>
    public byte ReadByte() => Take(1)[0];

    /// <summary>Reads a Two Byte Integer, big-endian (section 1.5.2).</summary>
    /// <returns>The integer.</returns>
    public int ReadTwoByteInteger()
    {
        byte[] bytes = Take(2);
        return (bytes[0] << 8) | bytes[1];
    }

    /// <summary>Reads a Four Byte Integer, big-endian (section 1.5.3).</summary>
    /// <returns>The integer.</returns>
    public long ReadFourByteInteger()
    {
        byte[] bytes = Take(4);
        return ((long)bytes[0] << 24) | ((long)bytes[1] << 16) | ((long)bytes[2] << 8) | bytes[3];
    }

    /// <summary>Reads a Variable Byte Integer (section 1.5.5), which must use the fewest bytes.</summary>
    /// <returns>The integer.</returns>
    public int ReadVariableByteInteger() =>
        DecodeVariableByteInteger(() => ReadByte());

    /// <summary>Reads a UTF-8 Encoded String (section 1.5.4) and checks its rules.</summary>
    /// <returns>The string.</returns>
    public string ReadUtf8String() => DecodeUtf8(ReadBinaryData());

    /// <summary>Reads Binary Data: a Two Byte Integer length and that many bytes (section 1.5.6).</summary>
    /// <returns>The bytes.</returns>
    public byte[] ReadBinaryData() => Take(ReadTwoByteInteger());

    /// <summary>Reads a UTF-8 String Pair (section 1.5.7).</summary>
    /// <returns>The name and the value.</returns>
    public KeyValuePair<string, string> ReadUtf8StringPair()
    {
        string name = ReadUtf8String();
        return new KeyValuePair<string, string>(name, ReadUtf8String());
    }

    /// <summary>Reads the given number of bytes.</summary>
    /// <param name="count">How many.</param>
    /// <returns>The bytes.</returns>
    public byte[] Take(int count)
    {
        if (count > body.Length - position)
        {
            throw new Mqtt5ConformanceViolationException("Mqtt5:MQTT5-1.5-1", $"a field needs {count} bytes and only {body.Length - position} remain.");
        }

        byte[] bytes = body[position..(position + count)];
        position += count;
        return bytes;
    }

    /// <summary>Decodes a Variable Byte Integer from bytes read one at a time (section 1.5.5).</summary>
    /// <param name="next">Reads the next byte.</param>
    /// <returns>The integer.</returns>
    public static int DecodeVariableByteInteger(Func<byte> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        int value = 0;
        for (int index = 0; index < 4; index++)
        {
            byte encoded = next();
            value |= (encoded & 0x7F) << (7 * index);
            if ((encoded & 0x80) == 0)
            {
                return encoded == 0 && index > 0
                    ? throw new Mqtt5ConformanceViolationException("Mqtt5:MQTT-1.5.5-1", $"a Variable Byte Integer of {value} uses {index + 1} bytes, more than it needs.")
                    : value;
            }
        }

        throw new Mqtt5ConformanceViolationException("Mqtt5:MQTT-1.5.5-1", "a Variable Byte Integer runs past four bytes.");
    }

    private static string DecodeUtf8(byte[] bytes)
    {
        string text;
        try
        {
            text = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new Mqtt5ConformanceViolationException("Mqtt5:MQTT-1.5.4-1", $"a UTF-8 Encoded String is ill-formed: {Convert.ToHexString(bytes)}.");
        }

        return text.Contains('\0', StringComparison.Ordinal)
            ? throw new Mqtt5ConformanceViolationException("Mqtt5:MQTT-1.5.4-2", "a UTF-8 Encoded String holds U+0000.")
            : text;
    }
}
