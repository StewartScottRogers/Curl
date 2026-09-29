using System.Buffers.Binary;
using System.Text;

namespace Curl.Kerberos;

/// <summary>Builds big-endian test files field by field, for the cases no recorded file covers.</summary>
internal sealed class BigEndianBytes
{
    private readonly List<byte> bytes = [];

    public BigEndianBytes Byte(byte value)
    {
        bytes.Add(value);
        return this;
    }

    public BigEndianBytes UInt16(ushort value)
    {
        byte[] field = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(field, value);
        return Raw(field);
    }

    public BigEndianBytes Int32(int value)
    {
        byte[] field = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(field, value);
        return Raw(field);
    }

    public BigEndianBytes UInt32(uint value)
    {
        byte[] field = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(field, value);
        return Raw(field);
    }

    public BigEndianBytes Raw(params byte[] value)
    {
        bytes.AddRange(value);
        return this;
    }

    public BigEndianBytes Bytes32(params byte[] value) => UInt32((uint)value.Length).Raw(value);

    public BigEndianBytes Bytes16(params byte[] value) => UInt16((ushort)value.Length).Raw(value);

    public BigEndianBytes String32(string value) => Bytes32(Encoding.UTF8.GetBytes(value));

    public BigEndianBytes String16(string value) => Bytes16(Encoding.UTF8.GetBytes(value));

    public byte[] ToArray() => [.. bytes];
}
