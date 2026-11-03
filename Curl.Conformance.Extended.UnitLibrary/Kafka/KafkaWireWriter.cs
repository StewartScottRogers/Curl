using System.Buffers.Binary;
using System.Text;

namespace Curl.Conformance.Extended.Kafka;

/// <summary>
/// Writes the Kafka protocol guide's primitive types big-endian: the reference broker's own codec, sharing nothing with
/// the client library, so a client bug is not mirrored here.
/// </summary>
public sealed class KafkaWireWriter
{
    private readonly MemoryStream buffer = new();

    /// <summary>Writes an INT8 (a BOOLEAN is an INT8 of 0 or 1).</summary>
    /// <param name="value">The value.</param>
    /// <returns>This writer.</returns>
    public KafkaWireWriter Int8(sbyte value)
    {
        buffer.WriteByte((byte)value);
        return this;
    }

    /// <summary>Writes an INT16.</summary>
    /// <param name="value">The value.</param>
    /// <returns>This writer.</returns>
    public KafkaWireWriter Int16(short value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(short)];
        BinaryPrimitives.WriteInt16BigEndian(bytes, value);
        return Raw(bytes);
    }

    /// <summary>Writes an INT32.</summary>
    /// <param name="value">The value.</param>
    /// <returns>This writer.</returns>
    public KafkaWireWriter Int32(int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        return Raw(bytes);
    }

    /// <summary>Writes a STRING (INT16 length, UTF-8 bytes), or a NULLABLE_STRING's null as length -1.</summary>
    /// <param name="value">The value, or null.</param>
    /// <returns>This writer.</returns>
    public KafkaWireWriter String(string? value)
    {
        if (value is null)
        {
            return Int16(-1);
        }

        byte[] bytes = Encoding.UTF8.GetBytes(value);
        return Int16((short)bytes.Length).Raw(bytes);
    }

    /// <summary>Writes BYTES: an INT32 length and the bytes.</summary>
    /// <param name="value">The bytes.</param>
    /// <returns>This writer.</returns>
    public KafkaWireWriter Bytes(ReadOnlySpan<byte> value) => Int32(value.Length).Raw(value);

    /// <summary>Writes bytes as they are.</summary>
    /// <param name="value">The bytes.</param>
    /// <returns>This writer.</returns>
    public KafkaWireWriter Raw(ReadOnlySpan<byte> value)
    {
        buffer.Write(value);
        return this;
    }

    /// <summary>The bytes written so far.</summary>
    /// <returns>A copy of the bytes.</returns>
    public byte[] ToArray() => buffer.ToArray();
}
