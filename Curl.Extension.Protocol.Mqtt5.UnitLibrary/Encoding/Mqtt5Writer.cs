namespace Curl.Extension.Protocol.Mqtt5.Encoding;

/// <summary>Writes the MQTT 5 data types of section 1.5 into a growing byte buffer, every integer big-endian.</summary>
internal sealed class Mqtt5Writer
{
    private readonly List<byte> bytes = [];

    /// <summary>How many bytes have been written.</summary>
    public int Length => bytes.Count;

    /// <summary>Writes one byte.</summary>
    public void WriteByte(byte value) => bytes.Add(value);

    /// <summary>Writes a Two Byte Integer (section 1.5.2).</summary>
    public void WriteTwoByteInteger(ushort value)
    {
        bytes.Add((byte)(value >> 8));
        bytes.Add((byte)value);
    }

    /// <summary>Writes a Four Byte Integer (section 1.5.3).</summary>
    public void WriteFourByteInteger(uint value)
    {
        WriteTwoByteInteger((ushort)(value >> 16));
        WriteTwoByteInteger((ushort)value);
    }

    /// <summary>Writes a Variable Byte Integer (section 1.5.5).</summary>
    public void WriteVariableByteInteger(int value) => Mqtt5VariableByteInteger.Write(value, bytes);

    /// <summary>Writes a UTF-8 Encoded String (section 1.5.4): its two-byte length, then its bytes.</summary>
    public void WriteUtf8String(string value) => WriteBinaryData(Mqtt5Utf8String.Encode(value));

    /// <summary>Writes Binary Data (section 1.5.6): its two-byte length, then the bytes, at most 65,535 of them.</summary>
    public void WriteBinaryData(ReadOnlySpan<byte> value)
    {
        if (value.Length > ushort.MaxValue)
        {
            throw Mqtt5MalformedPacketException.Malformed($"Binary Data is {value.Length} bytes, more than 65,535.");
        }

        WriteTwoByteInteger((ushort)value.Length);
        WriteBytes(value);
    }

    /// <summary>Writes a UTF-8 String Pair (section 1.5.7): the name string, then the value string.</summary>
    public void WriteUtf8StringPair(string name, string value)
    {
        WriteUtf8String(name);
        WriteUtf8String(value);
    }

    /// <summary>Writes bytes as they are, with no length before them.</summary>
    public void WriteBytes(ReadOnlySpan<byte> value) => bytes.AddRange(value);

    /// <summary>Returns the bytes written so far.</summary>
    public byte[] ToArray() => [.. bytes];
}
