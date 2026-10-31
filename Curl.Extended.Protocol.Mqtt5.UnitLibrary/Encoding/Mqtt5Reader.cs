using System.Buffers.Binary;

namespace Curl.Extended.Protocol.Mqtt5.Encoding;

/// <summary>
/// Reads the MQTT 5 data types of section 1.5 from the front of a byte range. Reading past its end is a Malformed
/// Packet (section 4.13), never an index exception.
/// </summary>
internal sealed class Mqtt5Reader
{
    private readonly ReadOnlyMemory<byte> bytes;
    private int position;

    /// <summary>Creates a reader over <paramref name="bytes"/>.</summary>
    public Mqtt5Reader(ReadOnlyMemory<byte> bytes) => this.bytes = bytes;

    /// <summary>How many bytes are left to read.</summary>
    public int RemainingByteCount => bytes.Length - position;

    /// <summary>Whether every byte has been read.</summary>
    public bool IsAtEnd => position == bytes.Length;

    /// <summary>Reads one byte.</summary>
    public byte ReadByte() => Take(1)[0];

    /// <summary>Reads a Two Byte Integer (section 1.5.2).</summary>
    public ushort ReadTwoByteInteger() => BinaryPrimitives.ReadUInt16BigEndian(Take(2));

    /// <summary>Reads a Four Byte Integer (section 1.5.3).</summary>
    public uint ReadFourByteInteger() => BinaryPrimitives.ReadUInt32BigEndian(Take(4));

    /// <summary>Reads a Variable Byte Integer (section 1.5.5).</summary>
    public int ReadVariableByteInteger()
    {
        if (!Mqtt5VariableByteInteger.TryRead(bytes.Span[position..], out int value, out int length))
        {
            throw Truncated();
        }

        position += length;
        return value;
    }

    /// <summary>Reads a UTF-8 Encoded String (section 1.5.4).</summary>
    public string ReadUtf8String() => Mqtt5Utf8String.Decode(Take(ReadTwoByteInteger()));

    /// <summary>Reads Binary Data (section 1.5.6).</summary>
    public byte[] ReadBinaryData() => Take(ReadTwoByteInteger()).ToArray();

    /// <summary>Reads a UTF-8 String Pair (section 1.5.7) as its name and value.</summary>
    public KeyValuePair<string, string> ReadUtf8StringPair()
    {
        string name = ReadUtf8String();
        return new(name, ReadUtf8String());
    }

    /// <summary>Reads every byte left.</summary>
    public byte[] ReadRemainingBytes() => Take(RemainingByteCount).ToArray();

    /// <summary>Reads the next <paramref name="length"/> bytes as a reader of their own.</summary>
    public Mqtt5Reader ReadSection(int length)
    {
        if (length > RemainingByteCount)
        {
            throw Truncated();
        }

        var section = new Mqtt5Reader(bytes.Slice(position, length));
        position += length;
        return section;
    }

    private ReadOnlySpan<byte> Take(int count)
    {
        if (count > RemainingByteCount)
        {
            throw Truncated();
        }

        ReadOnlySpan<byte> taken = bytes.Span.Slice(position, count);
        position += count;
        return taken;
    }

    private static Mqtt5MalformedPacketException Truncated() =>
        Mqtt5MalformedPacketException.Malformed("The packet ends inside a field.");
}
