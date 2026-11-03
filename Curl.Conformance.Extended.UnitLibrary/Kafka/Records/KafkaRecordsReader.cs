using System.Buffers.Binary;
using System.Text;

namespace Curl.Conformance.Extended.Kafka.Records;

/// <summary>Reads the types Produce, Fetch and record batch v2 use from bytes the client sent.</summary>
/// <param name="bytes">The bytes to read.</param>
public sealed class KafkaRecordsReader(ReadOnlyMemory<byte> bytes)
{
    private int position;

    /// <summary>Reads an INT8.</summary>
    /// <returns>The value.</returns>
    public sbyte Int8() => (sbyte)Take(1).Span[0];

    /// <summary>Reads an INT16.</summary>
    /// <returns>The value.</returns>
    public short Int16() => BinaryPrimitives.ReadInt16BigEndian(Take(sizeof(short)).Span);

    /// <summary>Reads an INT32.</summary>
    /// <returns>The value.</returns>
    public int Int32() => BinaryPrimitives.ReadInt32BigEndian(Take(sizeof(int)).Span);

    /// <summary>Reads an INT64.</summary>
    /// <returns>The value.</returns>
    public long Int64() => BinaryPrimitives.ReadInt64BigEndian(Take(sizeof(long)).Span);

    /// <summary>Reads a UINT32.</summary>
    /// <returns>The value.</returns>
    public uint UInt32() => BinaryPrimitives.ReadUInt32BigEndian(Take(sizeof(uint)).Span);

    /// <summary>Reads a zig-zag VARLONG (a VARINT reads the same way).</summary>
    /// <returns>The value.</returns>
    public long VarLong()
    {
        ulong raw = 0;
        int shift = 0;
        byte next;
        do
        {
            next = Take(1).Span[0];
            raw |= (ulong)(next & 0x7F) << shift;
            shift += 7;
        }
        while ((next & 0x80) != 0);

        return (long)(raw >> 1) ^ -(long)(raw & 1);
    }

    /// <summary>Reads a NULLABLE_STRING: an INT16 length, -1 for null, and UTF-8 bytes.</summary>
    /// <returns>The value, or null.</returns>
    public string? NullableString()
    {
        short length = Int16();
        return length < 0 ? null : Encoding.UTF8.GetString(Take(length).Span);
    }

    /// <summary>Reads NULLABLE_BYTES: an INT32 length, -1 for null, and the bytes.</summary>
    /// <returns>The bytes, or null.</returns>
    public byte[]? NullableBytes()
    {
        int length = Int32();
        return length < 0 ? null : Take(length).ToArray();
    }

    /// <summary>Reads a record's VARINT-length bytes, a length of -1 being null.</summary>
    /// <returns>The bytes, or null.</returns>
    public byte[]? NullableVarBytes()
    {
        int length = (int)VarLong();
        return length < 0 ? null : Take(length).ToArray();
    }

    /// <summary>Takes the next bytes as they are.</summary>
    /// <param name="count">How many.</param>
    /// <returns>The bytes.</returns>
    public ReadOnlyMemory<byte> Take(int count)
    {
        ReadOnlyMemory<byte> taken = bytes.Slice(position, count);
        position += count;
        return taken;
    }
}
