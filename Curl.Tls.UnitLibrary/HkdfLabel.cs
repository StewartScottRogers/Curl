using System.Security.Cryptography;
using System.Text;

namespace Curl.Tls;

/// <summary>
/// HKDF-Expand-Label (RFC 8446 section 7.1): HKDF-Expand with an info block that names
/// the output's length, a label prefixed with <c>tls13 </c>, and a context. Public so QUIC
/// derives its <c>client in</c>, <c>quic key</c>, <c>quic iv</c> and <c>quic hp</c> keys
/// (RFC 9001 section 5) with the same function.
/// </summary>
public static class HkdfLabel
{
    /// <summary>The prefix RFC 8446 puts before every label.</summary>
    public const string LabelPrefix = "tls13 ";

    /// <summary>
    /// Returns the <c>HkdfLabel</c> structure: a two-byte length, the prefixed label with a
    /// one-byte length, and the context with a one-byte length.
    /// </summary>
    /// <param name="label">The label without its <c>tls13 </c> prefix, e.g. <c>key</c> or <c>quic hp</c>.</param>
    /// <param name="context">The context: a transcript hash, a ticket nonce, or empty.</param>
    /// <param name="length">The length in bytes of the output it describes.</param>
    /// <returns>The encoded structure, the info input to HKDF-Expand.</returns>
    /// <exception cref="ArgumentException">The prefixed label is longer than 255 bytes, or the context is.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length" /> does not fit in two bytes.</exception>
    public static byte[] Encode(string label, ReadOnlySpan<byte> context, int length)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, ushort.MaxValue);
        byte[] fullLabel = Encoding.ASCII.GetBytes(LabelPrefix + label);
        TlsWriter writer = new();
        writer.WriteUInt16((ushort)length);
        writer.WriteOpaque(1, fullLabel);
        writer.WriteOpaque(1, context.ToArray());
        return writer.ToArray();
    }

    /// <summary>Returns HKDF-Expand-Label(<paramref name="secret" />, <paramref name="label" />, <paramref name="context" />, <paramref name="length" />).</summary>
    /// <param name="hashAlgorithm">The hash of the HKDF, the cipher suite's hash.</param>
    /// <param name="secret">The pseudorandom key to expand.</param>
    /// <param name="label">The label without its <c>tls13 </c> prefix.</param>
    /// <param name="context">The context: a transcript hash, a ticket nonce, or empty.</param>
    /// <param name="length">The length in bytes of the output.</param>
    /// <returns>The expanded key material.</returns>
    public static byte[] Expand(HashAlgorithmName hashAlgorithm, byte[] secret, string label, ReadOnlySpan<byte> context, int length)
    {
        ArgumentNullException.ThrowIfNull(secret);
        byte[] info = Encode(label, context, length);
        return HKDF.Expand(hashAlgorithm, secret, length, info);
    }
}
