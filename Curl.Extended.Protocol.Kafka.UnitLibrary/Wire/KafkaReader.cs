using System.Buffers.Binary;
using System.Text;

namespace Curl.Extended.Protocol.Kafka.Wire;

/// <summary>
/// Reads the Kafka protocol guide's primitive types ("Protocol Primitive Types") off a received response or record
/// batch. Reading past the end, or a negative length where one is not allowed, throws <see cref="FormatException"/>.
/// </summary>
internal sealed class KafkaReader(ReadOnlyMemory<byte> bytes)
{
    /// <summary>The message a read past the end throws with.</summary>
    internal const string Truncated = "Kafka response is truncated";

    /// <summary>The message a <c>VARLONG</c> longer than ten bytes throws with.</summary>
    internal const string VarIntTooLong = "Kafka varint is longer than ten bytes";

    /// <summary>The message a <c>VARINT</c> outside <see cref="int"/> throws with.</summary>
    internal const string VarIntOutOfRange = "Kafka varint is out of range";

    private int position;

    /// <summary>Gets the bytes not yet read.</summary>
    internal int Remaining => bytes.Length - position;

    /// <summary>Reads an <c>INT8</c>.</summary>
    internal sbyte Int8() => (sbyte)Take(1).Span[0];

    /// <summary>Reads a big-endian <c>INT16</c>.</summary>
    internal short Int16() => BinaryPrimitives.ReadInt16BigEndian(Take(sizeof(short)).Span);

    /// <summary>Reads a big-endian <c>INT32</c>.</summary>
    internal int Int32() => BinaryPrimitives.ReadInt32BigEndian(Take(sizeof(int)).Span);

    /// <summary>Reads a big-endian <c>UINT32</c>.</summary>
    internal uint UInt32() => BinaryPrimitives.ReadUInt32BigEndian(Take(sizeof(uint)).Span);

    /// <summary>Reads a big-endian <c>INT64</c>.</summary>
    internal long Int64() => BinaryPrimitives.ReadInt64BigEndian(Take(sizeof(long)).Span);

    /// <summary>Reads a <c>NULLABLE_STRING</c>: <see langword="null"/> for length -1.</summary>
    internal string? NullableString()
    {
        short length = Int16();
        return length < 0 ? null : Encoding.UTF8.GetString(Take(length).Span);
    }

    /// <summary>Reads <c>NULLABLE_BYTES</c> (and <c>RECORDS</c>): <see langword="null"/> for length -1.</summary>
    internal ReadOnlyMemory<byte>? NullableBytes()
    {
        int length = Int32();
        return length < 0 ? (ReadOnlyMemory<byte>?)null : Take(length);
    }

    /// <summary>Reads a record's bytes with a <c>VARINT</c> length: <see langword="null"/> for length -1.</summary>
    internal ReadOnlyMemory<byte>? NullableVarBytes()
    {
        int length = VarInt();
        return length < 0 ? (ReadOnlyMemory<byte>?)null : Take(length);
    }

    /// <summary>Reads a zig-zag <c>VARINT</c>.</summary>
    internal int VarInt()
    {
        long value = VarLong();
        return value is < int.MinValue or > int.MaxValue ? throw new FormatException(VarIntOutOfRange) : (int)value;
    }

    /// <summary>Reads a zig-zag <c>VARLONG</c>.</summary>
    internal long VarLong()
    {
        ulong raw = 0;
        for (int shift = 0; ; shift += 7)
        {
            if (shift > 63)
            {
                throw new FormatException(VarIntTooLong);
            }

            byte next = Take(1).Span[0];
            raw |= (ulong)(next & 0x7F) << shift;
            if (next < 0x80)
            {
                return (long)(raw >> 1) ^ -(long)(raw & 1);
            }
        }
    }

    /// <summary>Reads the next <paramref name="length"/> bytes.</summary>
    internal ReadOnlyMemory<byte> Take(int length)
    {
        if (length < 0 || length > Remaining)
        {
            throw new FormatException(Truncated);
        }

        ReadOnlyMemory<byte> slice = bytes.Slice(position, length);
        position += length;
        return slice;
    }
}
