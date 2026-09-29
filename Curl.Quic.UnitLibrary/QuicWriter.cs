using System.Buffers;

namespace Curl.Quic;

/// <summary>
/// Writes QUIC fields to the end of a growing buffer.
/// </summary>
internal sealed class QuicWriter
{
    private readonly ArrayBufferWriter<byte> buffer = new();

    public void WriteByte(byte value) => WriteBytes([value]);

    public void WriteVariableLengthInteger(ulong value) =>
        buffer.Advance(QuicVariableLengthInteger.Write(value, buffer.GetSpan(8)));

    public void WriteVariableLengthInteger(ulong value, int encodedLength) =>
        buffer.Advance(QuicVariableLengthInteger.Write(value, encodedLength, buffer.GetSpan(8)));

    public void WriteUInt(uint value, int length)
    {
        for (var index = length - 1; index >= 0; index--)
        {
            WriteByte((byte)(value >> (8 * index)));
        }
    }

    public void WriteBytes(ReadOnlySpan<byte> bytes) => buffer.Write(bytes);

    public void WriteLengthPrefixedBytes(ReadOnlySpan<byte> bytes)
    {
        WriteVariableLengthInteger((ulong)bytes.Length);
        WriteBytes(bytes);
    }

    public byte[] ToArray() => buffer.WrittenSpan.ToArray();
}
