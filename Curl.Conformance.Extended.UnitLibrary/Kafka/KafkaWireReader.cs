using System.Buffers.Binary;
using System.Text;

namespace Curl.Conformance.Extended.Kafka;

/// <summary>Reads the Kafka protocol guide's primitive types big-endian from bytes the client sent.</summary>
/// <param name="bytes">The bytes to read.</param>
public sealed class KafkaWireReader(ReadOnlyMemory<byte> bytes)
{
    private int position;

    /// <summary>The number of bytes read so far.</summary>
    public int Position => position;

    /// <summary>Reads an INT16.</summary>
    /// <returns>The value.</returns>
    public short Int16() => BinaryPrimitives.ReadInt16BigEndian(Take(sizeof(short)).Span);

    /// <summary>Reads an INT32.</summary>
    /// <returns>The value.</returns>
    public int Int32() => BinaryPrimitives.ReadInt32BigEndian(Take(sizeof(int)).Span);

    /// <summary>Reads a STRING: an INT16 length and that many UTF-8 bytes.</summary>
    /// <returns>The value.</returns>
    public string String() => Encoding.UTF8.GetString(Take(Int16()).Span);

    /// <summary>Reads BYTES: an INT32 length and that many bytes.</summary>
    /// <returns>The bytes.</returns>
    public byte[] Bytes() => Take(Int32()).ToArray();

    private ReadOnlyMemory<byte> Take(int count)
    {
        ReadOnlyMemory<byte> taken = bytes.Slice(position, count);
        position += count;
        return taken;
    }
}
