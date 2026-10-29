using System.Buffers;
using System.Buffers.Binary;

namespace Curl.Extension.Amqp0.Wire;

/// <summary>
/// Writes the AMQP 0-9-1 data types of section 4.2.5 in network byte order: octet, short, long, long-long, packed
/// bits, shortstr, longstr, timestamp and field table.
/// </summary>
internal sealed class Amqp0Writer
{
    private readonly ArrayBufferWriter<byte> buffer = new();

    /// <summary>Writes one octet.</summary>
    public void WriteOctet(byte value) => buffer.Write([value]);

    /// <summary>Writes a 16-bit short.</summary>
    public void WriteShort(ushort value) => BinaryPrimitives.WriteUInt16BigEndian(Reserve(2), value);

    /// <summary>Writes a 32-bit long.</summary>
    public void WriteLong(uint value) => BinaryPrimitives.WriteUInt32BigEndian(Reserve(4), value);

    /// <summary>Writes a 64-bit long-long.</summary>
    public void WriteLongLong(ulong value) => BinaryPrimitives.WriteUInt64BigEndian(Reserve(8), value);

    /// <summary>Writes a timestamp: seconds since the Unix epoch as a long-long (section 4.2.5.4).</summary>
    public void WriteTimestamp(DateTimeOffset value) => WriteLongLong((ulong)value.ToUnixTimeSeconds());

    /// <summary>Packs up to eight consecutive bit fields into one octet, the first in the lowest bit (section 4.2.5.2).</summary>
    public void WriteBits(params ReadOnlySpan<bool> bits)
    {
        int octet = 0;
        for (int index = 0; index < bits.Length; index++)
        {
            octet |= bits[index] ? 1 << index : 0;
        }

        WriteOctet((byte)octet);
    }

    /// <summary>Writes a short string: a length octet and at most 255 bytes of UTF-8 (section 4.2.5.3).</summary>
    public void WriteShortString(string value)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(value);
        if (bytes.Length > byte.MaxValue)
        {
            throw Amqp0WireFormatException.SyntaxError("A short string is longer than 255 bytes.");
        }

        WriteOctet((byte)bytes.Length);
        WriteBytes(bytes);
    }

    /// <summary>Writes a long string: a 32-bit length and the bytes (section 4.2.5.3).</summary>
    public void WriteLongString(ReadOnlySpan<byte> value)
    {
        WriteLong((uint)value.Length);
        WriteBytes(value);
    }

    /// <summary>Writes a field table (section 4.2.5.5): its encoded fields as a long string.</summary>
    public void WriteFieldTable(Amqp0FieldTable table) => WriteLongString(Amqp0FieldTableCodec.Encode(table));

    /// <summary>Writes bytes as they are.</summary>
    public void WriteBytes(ReadOnlySpan<byte> bytes) => buffer.Write(bytes);

    /// <summary>The bytes written so far.</summary>
    public byte[] ToArray() => buffer.WrittenSpan.ToArray();

    private Span<byte> Reserve(int count)
    {
        Span<byte> span = buffer.GetSpan(count)[..count];
        buffer.Advance(count);
        return span;
    }
}
