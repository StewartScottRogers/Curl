using System.Buffers.Binary;
using System.Text;

namespace Curl.Conformance.Extended.Grpc.Reflection;

/// <summary>
/// Writes a Protocol Buffers message in the binary wire format (protobuf.dev "Encoding"), field by field in the order
/// written: the reference reflection service's responses and the descriptors and messages a case builds by hand.
/// </summary>
public sealed class ProtobufMessageWriter
{
    private const int VarintWireType = 0;

    private const int Fixed64WireType = 1;

    private const int LengthDelimitedWireType = 2;

    private const int Fixed32WireType = 5;

    private readonly List<byte> bytes = [];

    /// <summary>Writes a varint field: an integer, a bool or an enum; a negative <c>int32</c> or <c>int64</c> as its 64-bit two's complement.</summary>
    /// <param name="fieldNumber">The field number.</param>
    /// <param name="value">The value.</param>
    /// <returns>This writer.</returns>
    public ProtobufMessageWriter Varint(int fieldNumber, long value)
    {
        WriteTag(fieldNumber, VarintWireType);
        WriteVarint((ulong)value);
        return this;
    }

    /// <summary>Writes a 64-bit fixed-width field: <c>fixed64</c>, <c>sfixed64</c> or <c>double</c>, little-endian.</summary>
    /// <param name="fieldNumber">The field number.</param>
    /// <param name="value">The value's eight bytes as an integer.</param>
    /// <returns>This writer.</returns>
    public ProtobufMessageWriter Fixed64(int fieldNumber, ulong value)
    {
        WriteTag(fieldNumber, Fixed64WireType);
        Span<byte> encoded = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(encoded, value);
        bytes.AddRange(encoded);
        return this;
    }

    /// <summary>Writes a 32-bit fixed-width field: <c>fixed32</c>, <c>sfixed32</c> or <c>float</c>, little-endian.</summary>
    /// <param name="fieldNumber">The field number.</param>
    /// <param name="value">The value's four bytes as an integer.</param>
    /// <returns>This writer.</returns>
    public ProtobufMessageWriter Fixed32(int fieldNumber, uint value)
    {
        WriteTag(fieldNumber, Fixed32WireType);
        Span<byte> encoded = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        bytes.AddRange(encoded);
        return this;
    }

    /// <summary>Writes a length-delimited field holding <paramref name="value" />.</summary>
    /// <param name="fieldNumber">The field number.</param>
    /// <param name="value">The field's bytes.</param>
    /// <returns>This writer.</returns>
    public ProtobufMessageWriter Bytes(int fieldNumber, ReadOnlySpan<byte> value)
    {
        WriteTag(fieldNumber, LengthDelimitedWireType);
        WriteVarint((ulong)value.Length);
        bytes.AddRange(value);
        return this;
    }

    /// <summary>Writes a <c>string</c> field, UTF-8 encoded.</summary>
    /// <param name="fieldNumber">The field number.</param>
    /// <param name="value">The string.</param>
    /// <returns>This writer.</returns>
    public ProtobufMessageWriter String(int fieldNumber, string value) => Bytes(fieldNumber, Encoding.UTF8.GetBytes(value));

    /// <summary>Writes an embedded message field.</summary>
    /// <param name="fieldNumber">The field number.</param>
    /// <param name="message">The embedded message.</param>
    /// <returns>This writer.</returns>
    public ProtobufMessageWriter Message(int fieldNumber, ProtobufMessageWriter message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return Bytes(fieldNumber, message.ToArray());
    }

    /// <summary>Gets the message written so far.</summary>
    /// <returns>Its binary form.</returns>
    public byte[] ToArray() => [.. bytes];

    private void WriteTag(int fieldNumber, int wireType) => WriteVarint((ulong)((fieldNumber << 3) | wireType));

    private void WriteVarint(ulong value)
    {
        while (value >= 0x80)
        {
            bytes.Add((byte)(value | 0x80));
            value >>= 7;
        }

        bytes.Add((byte)value);
    }
}
