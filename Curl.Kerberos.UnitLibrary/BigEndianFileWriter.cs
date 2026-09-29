using System.Buffers.Binary;
using System.Text;

namespace Curl.Kerberos;

/// <summary>
/// Writes the big-endian fields of a credential cache front to back, the mirror of
/// <see cref="BigEndianFileCursor" />. A writer made by <see cref="Measuring" /> writes
/// nothing and only counts, so the exact length is known before the real bytes are written
/// and no buffer holding a session key is ever grown and left behind.
/// </summary>
internal ref struct BigEndianFileWriter
{
    private readonly Span<byte> destination;

    private readonly bool measuring;

    /// <summary>Initializes a writer that writes into <paramref name="destination" />.</summary>
    /// <param name="destination">Where the fields go; it must be at least as long as they are.</param>
    public BigEndianFileWriter(Span<byte> destination)
        : this(destination, false)
    {
    }

    private BigEndianFileWriter(Span<byte> destination, bool measuring)
    {
        this.destination = destination;
        this.measuring = measuring;
    }

    /// <summary>Gets the number of bytes written, or counted when measuring.</summary>
    public int Position { get; private set; }

    /// <summary>Makes a writer that only counts the bytes it would write.</summary>
    /// <returns>The writer.</returns>
    public static BigEndianFileWriter Measuring() => new(default, true);

    /// <summary>Writes one byte.</summary>
    public void WriteByte(byte value) => Write([value]);

    /// <summary>Writes a big-endian unsigned 16-bit integer.</summary>
    public void WriteUInt16(ushort value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        Write(bytes);
    }

    /// <summary>Writes a big-endian unsigned 32-bit integer.</summary>
    public void WriteUInt32(uint value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        Write(bytes);
    }

    /// <summary>Writes a big-endian signed 32-bit integer.</summary>
    public void WriteInt32(int value) => WriteUInt32(unchecked((uint)value));

    /// <summary>Writes a UTF-8 string with a 32-bit length prefix (the credential cache's form).</summary>
    public void WriteString32(string value) => WriteBytes32(Encoding.UTF8.GetBytes(value));

    /// <summary>Writes bytes with a 32-bit length prefix.</summary>
    public void WriteBytes32(scoped ReadOnlySpan<byte> value)
    {
        WriteUInt32((uint)value.Length);
        Write(value);
    }

    private void Write(scoped ReadOnlySpan<byte> bytes)
    {
        if (!measuring)
        {
            bytes.CopyTo(destination[Position..]);
        }

        Position += bytes.Length;
    }
}
