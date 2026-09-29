namespace Curl.Tls;

/// <summary>
/// Writes TLS presentation-language fields (RFC 8446 section 3). A variable-length vector
/// is written by <see cref="WriteVector" />, which fills in its length prefix once the
/// body is written.
/// </summary>
internal sealed class TlsWriter
{
    private readonly List<byte> bytes = [];

    public void WriteUInt8(byte value) => bytes.Add(value);

    public void WriteUInt16(ushort value) => WriteUnsigned(value, 2);

    public void WriteUInt24(int value) => WriteUnsigned((uint)value, 3);

    public void WriteUInt32(uint value) => WriteUnsigned(value, 4);

    public void WriteBytes(ReadOnlySpan<byte> value) => bytes.AddRange(value);

    /// <summary>Writes <paramref name="value" /> preceded by its length in <paramref name="lengthBytes" /> bytes.</summary>
    public void WriteOpaque(int lengthBytes, byte[] value) => WriteVector(lengthBytes, body => body.WriteBytes(value));

    /// <summary>Writes a vector of 16-bit values preceded by its length in <paramref name="lengthBytes" /> bytes.</summary>
    public void WriteUInt16List(int lengthBytes, IReadOnlyList<ushort> values) => WriteVector(lengthBytes, body =>
    {
        foreach (ushort value in values)
        {
            body.WriteUInt16(value);
        }
    });

    /// <summary>
    /// Writes a length of <paramref name="lengthBytes" /> bytes, then the body
    /// <paramref name="writeBody" /> writes, and sets the length to the body's.
    /// </summary>
    /// <exception cref="ArgumentException">The body is too long for the length field.</exception>
    public void WriteVector(int lengthBytes, Action<TlsWriter> writeBody)
    {
        int lengthAt = bytes.Count;
        WriteUnsigned(0, lengthBytes);
        writeBody(this);
        long length = bytes.Count - lengthAt - lengthBytes;
        if (length >= 1L << (8 * lengthBytes))
        {
            throw new ArgumentException($"A {length}-byte vector does not fit a {lengthBytes}-byte length.", nameof(writeBody));
        }

        for (int index = lengthBytes - 1; index >= 0; index--)
        {
            bytes[lengthAt + index] = (byte)length;
            length >>= 8;
        }
    }

    public byte[] ToArray() => [.. bytes];

    private void WriteUnsigned(uint value, int byteCount)
    {
        for (int shift = 8 * (byteCount - 1); shift >= 0; shift -= 8)
        {
            bytes.Add((byte)(value >> shift));
        }
    }
}
