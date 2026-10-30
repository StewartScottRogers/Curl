using System.Buffers.Binary;

namespace Curl.Extension.ObliviousDoh.Wire;

/// <summary>Writes an <c>opaque &lt;0..2^16-1&gt;</c> vector of the TLS presentation language: a two-octet big-endian length, then the bytes.</summary>
internal static class LengthPrefix
{
    /// <summary>Gives <paramref name="bytes" /> with its two-octet length in front.</summary>
    /// <param name="bytes">The vector's bytes, at most 65535 of them.</param>
    /// <returns>The vector.</returns>
    internal static byte[] Prepend(ReadOnlySpan<byte> bytes)
    {
        byte[] vector = new byte[2 + bytes.Length];
        BinaryPrimitives.WriteUInt16BigEndian(vector, checked((ushort)bytes.Length));
        bytes.CopyTo(vector.AsSpan(2));
        return vector;
    }
}
