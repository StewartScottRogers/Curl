using System.Buffers.Binary;
using System.Text;

namespace Curl.Extension.Kafka.Wire;

/// <summary>
/// Writes the Kafka protocol guide's primitive types ("Protocol Primitive Types"): big-endian integers, <c>STRING</c>
/// and <c>NULLABLE_STRING</c> with an <c>INT16</c> length, <c>BYTES</c> with an <c>INT32</c> length, and the record
/// format's zig-zag <c>VARINT</c> and <c>VARLONG</c>.
/// </summary>
internal sealed class KafkaWriter
{
    private readonly MemoryStream buffer = new();

    /// <summary>Writes an <c>INT8</c>.</summary>
    internal KafkaWriter Int8(sbyte value)
    {
        buffer.WriteByte((byte)value);
        return this;
    }

    /// <summary>Writes a big-endian <c>INT16</c>.</summary>
    internal KafkaWriter Int16(short value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(short)];
        BinaryPrimitives.WriteInt16BigEndian(bytes, value);
        return Raw(bytes);
    }

    /// <summary>Writes a big-endian <c>INT32</c>.</summary>
    internal KafkaWriter Int32(int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        return Raw(bytes);
    }

    /// <summary>Writes a big-endian <c>UINT32</c>.</summary>
    internal KafkaWriter UInt32(uint value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return Raw(bytes);
    }

    /// <summary>Writes a big-endian <c>INT64</c>.</summary>
    internal KafkaWriter Int64(long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return Raw(bytes);
    }

    /// <summary>Writes a <c>STRING</c>: its UTF-8 length as an <c>INT16</c>, then its bytes.</summary>
    internal KafkaWriter String(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        return Int16((short)bytes.Length).Raw(bytes);
    }

    /// <summary>Writes a null <c>NULLABLE_STRING</c>: length -1.</summary>
    internal KafkaWriter NullString() => Int16(-1);

    /// <summary>Writes <c>BYTES</c>: the length as an <c>INT32</c>, then the bytes.</summary>
    internal KafkaWriter Bytes(ReadOnlySpan<byte> value) => Int32(value.Length).Raw(value);

    /// <summary>Writes a zig-zag <c>VARINT</c>.</summary>
    internal KafkaWriter VarInt(int value) => UnsignedVarLong((uint)((value << 1) ^ (value >> 31)));

    /// <summary>Writes a zig-zag <c>VARLONG</c>.</summary>
    internal KafkaWriter VarLong(long value) => UnsignedVarLong((ulong)((value << 1) ^ (value >> 63)));

    /// <summary>Writes <paramref name="value"/> as it is.</summary>
    internal KafkaWriter Raw(ReadOnlySpan<byte> value)
    {
        buffer.Write(value);
        return this;
    }

    /// <summary>Gives the bytes written so far.</summary>
    internal byte[] ToArray() => buffer.ToArray();

    private KafkaWriter UnsignedVarLong(ulong value)
    {
        for (; value >= 0x80; value >>= 7)
        {
            buffer.WriteByte((byte)(value | 0x80));
        }

        buffer.WriteByte((byte)value);
        return this;
    }
}
