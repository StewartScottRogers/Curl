using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Curl.Protocol.Ssh.Transport;

/// <summary>
/// Writes the data types of RFC 4251 section 5 into a message payload.
/// </summary>
internal sealed class SshWireWriter
{
    private readonly ArrayBufferWriter<byte> buffer = new();

    /// <summary>
    /// Appends one <c>byte</c>.
    /// </summary>
    /// <param name="value">The byte.</param>
    internal void WriteByte(byte value) => WriteBytes([value]);

    /// <summary>
    /// Appends a <c>boolean</c>: one byte, 1 for true and 0 for false.
    /// </summary>
    /// <param name="value">The value.</param>
    internal void WriteBoolean(bool value) => WriteByte(value ? (byte)1 : (byte)0);

    /// <summary>
    /// Appends a <c>uint32</c>, most significant byte first.
    /// </summary>
    /// <param name="value">The value.</param>
    internal void WriteUInt32(uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(buffer.GetSpan(4), value);
        buffer.Advance(4);
    }

    /// <summary>
    /// Appends raw bytes with no length prefix.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    internal void WriteBytes(ReadOnlySpan<byte> bytes) => buffer.Write(bytes);

    /// <summary>
    /// Appends a <c>string</c>: its length as a <c>uint32</c>, then its bytes.
    /// </summary>
    /// <param name="bytes">The string's bytes.</param>
    internal void WriteString(ReadOnlySpan<byte> bytes)
    {
        WriteUInt32((uint)bytes.Length);
        WriteBytes(bytes);
    }

    /// <summary>
    /// Appends a non-negative <c>mpint</c>: the magnitude without leading zero bytes, with
    /// one zero byte in front when its top bit is set, as a <c>string</c>.
    /// </summary>
    /// <param name="magnitude">The value, unsigned big-endian, leading zeros allowed.</param>
    internal void WriteMpint(ReadOnlySpan<byte> magnitude)
    {
        ReadOnlySpan<byte> trimmed = magnitude.TrimStart((byte)0);
        bool needsSignByte = !trimmed.IsEmpty && trimmed[0] >= 0x80;
        WriteUInt32((uint)(trimmed.Length + (needsSignByte ? 1 : 0)));
        if (needsSignByte)
        {
            WriteByte(0);
        }

        WriteBytes(trimmed);
    }

    /// <summary>
    /// Appends a <c>name-list</c>:the names joined by commas, as a <c>string</c> of
    /// US-ASCII bytes.
    /// </summary>
    /// <param name="names">The names, in preference order.</param>
    internal void WriteNameList(IEnumerable<string> names) =>
        WriteString(Encoding.ASCII.GetBytes(string.Join(',', names)));

    /// <summary>
    /// Gets everything appended so far.
    /// </summary>
    /// <returns>A copy of the bytes.</returns>
    internal byte[] ToArray() => buffer.WrittenSpan.ToArray();
}
