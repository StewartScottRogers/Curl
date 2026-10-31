using System.Buffers.Binary;

namespace Curl.Extension.CertificateTransparency;

/// <summary>Reads the TLS presentation language encoding (RFC 8446 §3) that RFC 6962 §3.2 and §3.3 use for SCTs.</summary>
/// <param name="bytes">The encoded structure.</param>
internal sealed class TlsReader(ReadOnlyMemory<byte> bytes)
{
    private int position;

    /// <summary>Gets a value indicating whether every byte has been read.</summary>
    internal bool IsAtEnd => position == bytes.Length;

    /// <summary>Reads the next <paramref name="count" /> bytes.</summary>
    /// <param name="count">How many bytes to read.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="FormatException">Fewer bytes are left.</exception>
    internal ReadOnlyMemory<byte> ReadBytes(int count)
    {
        if (count > bytes.Length - position)
        {
            throw new FormatException("A TLS structure ends before its declared length.");
        }

        var read = bytes.Slice(position, count);
        position += count;
        return read;
    }

    /// <summary>Reads one byte.</summary>
    /// <returns>The byte.</returns>
    internal byte ReadByte() => ReadBytes(1).Span[0];

    /// <summary>Reads a big-endian <c>uint64</c>.</summary>
    /// <returns>The value.</returns>
    internal ulong ReadUInt64() => BinaryPrimitives.ReadUInt64BigEndian(ReadBytes(sizeof(ulong)).Span);

    /// <summary>Reads a vector with a two-byte length, such as <c>opaque x&lt;0..2^16-1&gt;</c>.</summary>
    /// <returns>The vector's contents.</returns>
    internal ReadOnlyMemory<byte> ReadVector16() => ReadBytes(BinaryPrimitives.ReadUInt16BigEndian(ReadBytes(sizeof(ushort)).Span));

    /// <summary>Checks that every byte has been read.</summary>
    /// <exception cref="FormatException">Bytes are left over.</exception>
    internal void ExpectEnd()
    {
        if (!IsAtEnd)
        {
            throw new FormatException("A TLS structure has bytes past its end.");
        }
    }
}
