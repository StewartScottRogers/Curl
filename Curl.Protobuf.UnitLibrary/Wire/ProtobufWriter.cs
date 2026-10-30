using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Curl.Protobuf.Wire;

/// <summary>Writes Protocol Buffers wire format into a growing buffer.</summary>
public sealed class ProtobufWriter
{
    private const int MaximumVarintLength = 10;

    private readonly ArrayBufferWriter<byte> output = new();

    /// <summary>The bytes written so far.</summary>
    public ReadOnlySpan<byte> WrittenBytes => output.WrittenSpan;

    /// <summary>Copies the bytes written so far into a new array.</summary>
    public byte[] ToArray() => output.WrittenSpan.ToArray();

    /// <summary>Writes a base-128 varint, least significant group first.</summary>
    public void WriteVarint(ulong value)
    {
        var span = output.GetSpan(MaximumVarintLength);
        var count = 0;
        while (value >= 0x80)
        {
            span[count++] = (byte)(value | 0x80);
            value >>= 7;
        }

        span[count++] = (byte)value;
        output.Advance(count);
    }

    /// <summary>Writes four little-endian bytes.</summary>
    public void WriteFixed32(uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(output.GetSpan(sizeof(uint)), value);
        output.Advance(sizeof(uint));
    }

    /// <summary>Writes eight little-endian bytes.</summary>
    public void WriteFixed64(ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(output.GetSpan(sizeof(ulong)), value);
        output.Advance(sizeof(ulong));
    }

    /// <summary>Writes a varint length and then the bytes.</summary>
    public void WriteLengthDelimited(ReadOnlySpan<byte> payload)
    {
        WriteVarint((ulong)payload.Length);
        output.Write(payload);
    }

    /// <summary>Writes a field tag; the field number must be from 1 to <see cref="ProtobufReader.MaximumFieldNumber" />.</summary>
    public void WriteTag(int fieldNumber, ProtobufWireType wireType)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(fieldNumber, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(fieldNumber, ProtobufReader.MaximumFieldNumber);
        WriteVarint(((ulong)fieldNumber << 3) | (ulong)wireType);
    }

    /// <summary>Writes a varint field (uint32, uint64, bool, or an already widened value).</summary>
    public void WriteVarintField(int fieldNumber, ulong value)
    {
        WriteTag(fieldNumber, ProtobufWireType.Varint);
        WriteVarint(value);
    }

    /// <summary>Writes an <c>int32</c> or <c>enum</c> field; a negative value is sign-extended to 10 bytes.</summary>
    public void WriteInt32Field(int fieldNumber, int value) => WriteVarintField(fieldNumber, (ulong)(long)value);

    /// <summary>Writes an <c>int64</c> field; a negative value takes 10 bytes.</summary>
    public void WriteInt64Field(int fieldNumber, long value) => WriteVarintField(fieldNumber, (ulong)value);

    /// <summary>Writes a ZigZag-encoded <c>sint32</c> field.</summary>
    public void WriteSInt32Field(int fieldNumber, int value) => WriteVarintField(fieldNumber, ProtobufZigZag.Encode32(value));

    /// <summary>Writes a ZigZag-encoded <c>sint64</c> field.</summary>
    public void WriteSInt64Field(int fieldNumber, long value) => WriteVarintField(fieldNumber, ProtobufZigZag.Encode64(value));

    /// <summary>Writes a fixed32 field (fixed32, sfixed32, or a float's bits).</summary>
    public void WriteFixed32Field(int fieldNumber, uint value)
    {
        WriteTag(fieldNumber, ProtobufWireType.Fixed32);
        WriteFixed32(value);
    }

    /// <summary>Writes a fixed64 field (fixed64, sfixed64, or a double's bits).</summary>
    public void WriteFixed64Field(int fieldNumber, ulong value)
    {
        WriteTag(fieldNumber, ProtobufWireType.Fixed64);
        WriteFixed64(value);
    }

    /// <summary>Writes a length-delimited field (bytes, or an embedded message's encoding).</summary>
    public void WriteBytesField(int fieldNumber, ReadOnlySpan<byte> payload)
    {
        WriteTag(fieldNumber, ProtobufWireType.LengthDelimited);
        WriteLengthDelimited(payload);
    }

    /// <summary>Writes a <c>string</c> field as UTF-8.</summary>
    public void WriteStringField(int fieldNumber, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        WriteBytesField(fieldNumber, Encoding.UTF8.GetBytes(value));
    }

    /// <summary>Writes a packed repeated varint field.</summary>
    public void WritePackedVarintsField(int fieldNumber, ReadOnlySpan<ulong> values)
    {
        var packed = new ProtobufWriter();
        foreach (var value in values)
        {
            packed.WriteVarint(value);
        }

        WriteBytesField(fieldNumber, packed.WrittenBytes);
    }

    /// <summary>Writes a packed repeated fixed32 field.</summary>
    public void WritePackedFixed32Field(int fieldNumber, ReadOnlySpan<uint> values)
    {
        var packed = new ProtobufWriter();
        foreach (var value in values)
        {
            packed.WriteFixed32(value);
        }

        WriteBytesField(fieldNumber, packed.WrittenBytes);
    }

    /// <summary>Writes a packed repeated fixed64 field.</summary>
    public void WritePackedFixed64Field(int fieldNumber, ReadOnlySpan<ulong> values)
    {
        var packed = new ProtobufWriter();
        foreach (var value in values)
        {
            packed.WriteFixed64(value);
        }

        WriteBytesField(fieldNumber, packed.WrittenBytes);
    }

    /// <summary>
    /// Writes back a field read by <see cref="ProtobufReader.TryReadField" />, a group with
    /// every field inside it: how unknown fields survive a round trip.
    /// </summary>
    public void WriteField(ProtobufField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        switch (field.WireType)
        {
            case ProtobufWireType.Varint:
                WriteVarintField(field.FieldNumber, field.Scalar);
                break;
            case ProtobufWireType.Fixed64:
                WriteFixed64Field(field.FieldNumber, field.Scalar);
                break;
            case ProtobufWireType.Fixed32:
                WriteFixed32Field(field.FieldNumber, (uint)field.Scalar);
                break;
            case ProtobufWireType.LengthDelimited:
                WriteBytesField(field.FieldNumber, field.Payload.Span);
                break;
            case ProtobufWireType.StartGroup:
                WriteGroup(field);
                break;
            default:
                throw new ArgumentException($"A field cannot have wire type {field.WireType}.", nameof(field));
        }
    }

    private void WriteGroup(ProtobufField group)
    {
        WriteTag(group.FieldNumber, ProtobufWireType.StartGroup);
        foreach (var field in group.GroupFields)
        {
            WriteField(field);
        }

        WriteTag(group.FieldNumber, ProtobufWireType.EndGroup);
    }
}
