using System.Buffers.Binary;
using System.Text;

namespace Curl.Protocol.Ssh.Transport;

/// <summary>
/// Reads the data types of RFC 4251 section 5 from a message payload, front to back.
/// </summary>
/// <param name="payload">The payload to read.</param>
internal sealed class SshWireReader(ReadOnlyMemory<byte> payload)
{
    private int position;

    /// <summary>
    /// Reads one <c>byte</c>.
    /// </summary>
    /// <returns>The byte.</returns>
    /// <exception cref="InvalidDataException">The payload has ended.</exception>
    internal byte ReadByte() => ReadBytes(1).Span[0];

    /// <summary>
    /// Reads a <c>boolean</c>: any non-zero byte is true.
    /// </summary>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidDataException">The payload has ended.</exception>
    internal bool ReadBoolean() => ReadByte() != 0;

    /// <summary>
    /// Reads a <c>uint32</c>, most significant byte first.
    /// </summary>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidDataException">Fewer than four bytes remain.</exception>
    internal uint ReadUInt32() => BinaryPrimitives.ReadUInt32BigEndian(ReadBytes(4).Span);

    /// <summary>
    /// Reads <paramref name="count" /> raw bytes.
    /// </summary>
    /// <param name="count">How many bytes to read.</param>
    /// <returns>The bytes, a slice of the payload.</returns>
    /// <exception cref="InvalidDataException">Fewer than <paramref name="count" /> bytes remain.</exception>
    internal ReadOnlyMemory<byte> ReadBytes(long count)
    {
        if (count > payload.Length - position)
        {
            throw new InvalidDataException("The SSH message ended inside a field.");
        }

        ReadOnlyMemory<byte> bytes = payload.Slice(position, (int)count);
        position += (int)count;
        return bytes;
    }

    /// <summary>
    /// Reads a <c>string</c>: a <c>uint32</c> length, then that many bytes.
    /// </summary>
    /// <returns>The string's bytes, a slice of the payload.</returns>
    /// <exception cref="InvalidDataException">The length runs past the payload's end.</exception>
    internal ReadOnlyMemory<byte> ReadString() => ReadBytes(ReadUInt32());

    /// <summary>
    /// Reads a <c>name-list</c>: a <c>string</c> of comma-separated US-ASCII names. An
    /// empty string is an empty list.
    /// </summary>
    /// <returns>The names, in the order given.</returns>
    /// <exception cref="InvalidDataException">The length runs past the payload's end.</exception>
    internal IReadOnlyList<string> ReadNameList()
    {
        ReadOnlyMemory<byte> bytes = ReadString();
        return bytes.IsEmpty ? [] : Encoding.ASCII.GetString(bytes.Span).Split(',');
    }
}
