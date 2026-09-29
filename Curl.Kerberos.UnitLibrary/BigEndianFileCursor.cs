using System.Buffers.Binary;
using System.Text;

namespace Curl.Kerberos;

/// <summary>
/// Reads the big-endian fields of a credential cache or keytab front to back, throwing
/// <see cref="KerberosFileException" /> with <see cref="KerberosFileError.Truncated" />
/// when a field runs past the end.
/// </summary>
/// <param name="bytes">The bytes to read.</param>
internal ref struct BigEndianFileCursor(ReadOnlySpan<byte> bytes)
{
    private ReadOnlySpan<byte> remaining = bytes;

    /// <summary>Gets the number of bytes not yet read.</summary>
    public readonly int RemainingLength => remaining.Length;

    /// <summary>Reads one byte.</summary>
    public byte ReadByte() => Take(1)[0];

    /// <summary>Reads a big-endian unsigned 16-bit integer.</summary>
    public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16BigEndian(Take(2));

    /// <summary>Reads a big-endian unsigned 32-bit integer.</summary>
    public uint ReadUInt32() => BinaryPrimitives.ReadUInt32BigEndian(Take(4));

    /// <summary>Reads a big-endian signed 32-bit integer.</summary>
    public int ReadInt32() => BinaryPrimitives.ReadInt32BigEndian(Take(4));

    /// <summary>Reads <paramref name="length" /> bytes; a length past the end is truncation.</summary>
    public ReadOnlySpan<byte> Take(long length)
    {
        if (length > remaining.Length)
        {
            throw new KerberosFileException(KerberosFileError.Truncated);
        }

        ReadOnlySpan<byte> taken = remaining[..(int)length];
        remaining = remaining[(int)length..];
        return taken;
    }

    /// <summary>Reads a UTF-8 string whose byte length is a 32-bit prefix (the credential cache's form).</summary>
    public string ReadString32() => Encoding.UTF8.GetString(Take(ReadUInt32()));

    /// <summary>Reads a UTF-8 string whose byte length is a 16-bit prefix (the keytab's form).</summary>
    public string ReadString16() => Encoding.UTF8.GetString(Take(ReadUInt16()));

    /// <summary>Reads bytes whose length is a 32-bit prefix.</summary>
    public byte[] ReadBytes32() => Take(ReadUInt32()).ToArray();

    /// <summary>Reads bytes whose length is a 16-bit prefix.</summary>
    public byte[] ReadBytes16() => Take(ReadUInt16()).ToArray();
}
