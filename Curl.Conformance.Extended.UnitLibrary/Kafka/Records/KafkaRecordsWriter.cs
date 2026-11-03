using System.Buffers.Binary;
using System.Text;

namespace Curl.Conformance.Extended.Kafka.Records;

/// <summary>
/// Writes the types Produce, Fetch and record batch v2 use, big-endian, with zig-zag VARINT and VARLONG: the reference
/// broker's own codec, written from the protocol guide, sharing nothing with the client library.
/// </summary>
public sealed class KafkaRecordsWriter
{
    private readonly MemoryStream buffer = new();

    /// <summary>Writes an INT8.</summary>
    /// <param name="value">The value.</param>
    /// <returns>This writer.</returns>
    public KafkaRecordsWriter Int8(sbyte value)
    {
        buffer.WriteByte((byte)value);
        return this;
    }

    /// <summary>Writes an INT16.</summary>
    /// <param name="value">The value.</param>
    /// <returns>This writer.</returns>
    public KafkaRecordsWriter Int16(short value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(short)];
        BinaryPrimitives.WriteInt16BigEndian(bytes, value);
        return Raw(bytes);
    }

    /// <summary>Writes an INT32.</summary>
    /// <param name="value">The value.</param>
    /// <returns>This writer.</returns>
    public KafkaRecordsWriter Int32(int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        return Raw(bytes);
    }

    /// <summary>Writes an INT64.</summary>
    /// <param name="value">The value.</param>
    /// <returns>This writer.</returns>
    public KafkaRecordsWriter Int64(long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return Raw(bytes);
    }

    /// <summary>Writes a UINT32.</summary>
    /// <param name="value">The value.</param>
    /// <returns>This writer.</returns>
    public KafkaRecordsWriter UInt32(uint value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return Raw(bytes);
    }

    /// <summary>Writes a zig-zag VARLONG (a VARINT is written the same way).</summary>
    /// <param name="value">The value.</param>
    /// <returns>This writer.</returns>
    public KafkaRecordsWriter VarLong(long value)
    {
        ulong raw = (ulong)((value << 1) ^ (value >> 63));
        for (; raw >= 0x80; raw >>= 7)
        {
            buffer.WriteByte((byte)(raw | 0x80));
        }

        buffer.WriteByte((byte)raw);
        return this;
    }

    /// <summary>Writes a STRING: an INT16 length and UTF-8 bytes.</summary>
    /// <param name="value">The value.</param>
    /// <returns>This writer.</returns>
    public KafkaRecordsWriter String(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        return Int16((short)bytes.Length).Raw(bytes);
    }

    /// <summary>Writes NULLABLE_BYTES: an INT32 length and the bytes, or -1 for null.</summary>
    /// <param name="value">The bytes, or null.</param>
    /// <returns>This writer.</returns>
    public KafkaRecordsWriter NullableBytes(byte[]? value) => value is null ? Int32(-1) : Int32(value.Length).Raw(value);

    /// <summary>Writes a record's VARINT-length bytes, or a length of -1 for null.</summary>
    /// <param name="value">The bytes, or null.</param>
    /// <returns>This writer.</returns>
    public KafkaRecordsWriter NullableVarBytes(byte[]? value) => value is null ? VarLong(-1) : VarLong(value.Length).Raw(value);

    /// <summary>Writes bytes as they are.</summary>
    /// <param name="value">The bytes.</param>
    /// <returns>This writer.</returns>
    public KafkaRecordsWriter Raw(ReadOnlySpan<byte> value)
    {
        buffer.Write(value);
        return this;
    }

    /// <summary>The bytes written so far.</summary>
    /// <returns>A copy of the bytes.</returns>
    public byte[] ToArray() => buffer.ToArray();
}
