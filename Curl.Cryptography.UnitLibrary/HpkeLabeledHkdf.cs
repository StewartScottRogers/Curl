using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// RFC 9180 section 4's <c>LabeledExtract</c> and <c>LabeledExpand</c> over HKDF-SHA256:
/// every input is prefixed with <c>"HPKE-v1"</c>, the caller's suite identifier and a
/// label, and <c>LabeledExpand</c> also with the two-byte output length.
/// </summary>
/// <remarks>Each labeled input is built in a temporary array that is zeroed before it is released.</remarks>
internal static class HpkeLabeledHkdf
{
    /// <summary>Nh, the length in bytes of an HKDF-SHA256 pseudorandom key.</summary>
    internal const int HashSize = 32;

    private static ReadOnlySpan<byte> VersionLabel => "HPKE-v1"u8;

    /// <summary>
    /// Writes <c>LabeledExtract(salt, label, ikm)</c> for <paramref name="suiteId" /> into
    /// <paramref name="pseudorandomKey" />, which is <see cref="HashSize" /> bytes.
    /// </summary>
    internal static void Extract(
        ReadOnlySpan<byte> suiteId,
        ReadOnlySpan<byte> salt,
        ReadOnlySpan<byte> label,
        ReadOnlySpan<byte> inputKeyMaterial,
        Span<byte> pseudorandomKey)
    {
        byte[] labeledInput = new byte[VersionLabel.Length + suiteId.Length + label.Length + inputKeyMaterial.Length];
        try
        {
            Concatenate(labeledInput, suiteId, label, inputKeyMaterial);
            HKDF.Extract(HashAlgorithmName.SHA256, labeledInput, salt, pseudorandomKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(labeledInput);
        }
    }

    /// <summary>
    /// Fills <paramref name="output" /> with <c>LabeledExpand(prk, label, info, L)</c> for
    /// <paramref name="suiteId" />, L being the length of <paramref name="output" />.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="output" /> is empty or longer than 255 * <see cref="HashSize" /> bytes.</exception>
    internal static void Expand(
        ReadOnlySpan<byte> suiteId,
        ReadOnlySpan<byte> pseudorandomKey,
        ReadOnlySpan<byte> label,
        ReadOnlySpan<byte> info,
        Span<byte> output)
    {
        byte[] labeledInfo = new byte[sizeof(ushort) + VersionLabel.Length + suiteId.Length + label.Length + info.Length];
        try
        {
            BinaryPrimitives.WriteUInt16BigEndian(labeledInfo, (ushort)output.Length);
            Concatenate(labeledInfo.AsSpan(sizeof(ushort)), suiteId, label, info);
            HKDF.Expand(HashAlgorithmName.SHA256, pseudorandomKey, output, labeledInfo);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(labeledInfo);
        }
    }

    private static void Concatenate(Span<byte> destination, ReadOnlySpan<byte> suiteId, ReadOnlySpan<byte> label, ReadOnlySpan<byte> value)
    {
        VersionLabel.CopyTo(destination);
        suiteId.CopyTo(destination[VersionLabel.Length..]);
        label.CopyTo(destination[(VersionLabel.Length + suiteId.Length)..]);
        value.CopyTo(destination[(VersionLabel.Length + suiteId.Length + label.Length)..]);
    }
}
