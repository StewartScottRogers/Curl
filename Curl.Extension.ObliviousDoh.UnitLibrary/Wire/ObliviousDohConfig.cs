using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Extension.ObliviousDoh.Wire;

/// <summary>
/// One target key: the <c>ObliviousDoHConfigContents</c> of an <c>ObliviousDoHConfig</c> (RFC 9230 section 5),
/// its HPKE suite and public key.
/// </summary>
/// <param name="Kem">The HPKE KEM.</param>
/// <param name="Kdf">The HPKE KDF.</param>
/// <param name="Aead">The HPKE AEAD.</param>
/// <param name="PublicKey">The target's public key, in the KEM's serialized form.</param>
public sealed record ObliviousDohConfig(HpkeKem Kem, HpkeKdf Kdf, HpkeAead Aead, byte[] PublicKey)
{
    /// <summary>The only <c>ObliviousDoHConfig</c> version RFC 9230 defines, <c>0x0001</c>.</summary>
    public const ushort Version = 0x0001;

    private const int KeyIdLength = 32;

    /// <summary>
    /// Picks the first config of an <c>ObliviousDoHConfigs</c> list (section 5) with version <see cref="Version" />
    /// and a suite <see cref="Hpke" /> supports with HKDF-SHA256, skipping the others as section 5 says a client does.
    /// </summary>
    /// <param name="configs">The <c>ObliviousDoHConfigs</c> bytes.</param>
    /// <returns>The config, or <see langword="null" /> when the list is malformed or no entry can be used.</returns>
    public static ObliviousDohConfig? SelectFrom(ReadOnlySpan<byte> configs)
    {
        var list = new LengthPrefixedReader(configs);
        return list.TryReadVector(out ReadOnlySpan<byte> entries) && list.IsEmpty ? SelectEntry(entries) : null;
    }

    /// <summary>Writes the <c>ObliviousDoHConfigContents</c>: the three suite identifiers, then the public key as a vector.</summary>
    /// <returns>The contents' bytes.</returns>
    public byte[] EncodeContents() =>
        [(byte)((ushort)Kem >> 8), (byte)Kem, (byte)((ushort)Kdf >> 8), (byte)Kdf, (byte)((ushort)Aead >> 8), (byte)Aead, .. LengthPrefix.Prepend(PublicKey)];

    /// <summary>
    /// Computes the key identifier a query names the key by: <c>Expand(Extract("", config), "odoh key id", Nh)</c>
    /// over the contents (section 6.1), with HKDF-SHA256, so <c>Nh</c> is 32.
    /// </summary>
    /// <returns>The key identifier.</returns>
    public byte[] KeyId()
    {
        byte[] pseudorandomKey = HKDF.Extract(HashAlgorithmName.SHA256, EncodeContents(), []);
        return HKDF.Expand(HashAlgorithmName.SHA256, pseudorandomKey, KeyIdLength, "odoh key id"u8.ToArray());
    }

    // The first usable entry of the list's ObliviousDoHConfig entries, or null.
    private static ObliviousDohConfig? SelectEntry(ReadOnlySpan<byte> entries)
    {
        var reader = new LengthPrefixedReader(entries);
        while (!reader.IsEmpty)
        {
            if (!reader.TryReadUInt16(out ushort version) || !reader.TryReadVector(out ReadOnlySpan<byte> contents))
            {
                return null;
            }

            if (DecodeContents(version, contents) is { } config)
            {
                return config;
            }
        }

        return null;
    }

    // The contents, or null when the version is not 0x0001 or they are malformed or name a suite this client cannot use.
    private static ObliviousDohConfig? DecodeContents(ushort version, ReadOnlySpan<byte> contents)
    {
        var reader = new LengthPrefixedReader(contents);
        bool read = reader.TryReadUInt16(out ushort kem) & reader.TryReadUInt16(out ushort kdf) & reader.TryReadUInt16(out ushort aead)
            & reader.TryReadVector(out ReadOnlySpan<byte> publicKey);
        return version == Version && read && reader.IsEmpty && IsUsable((HpkeKem)kem, (HpkeKdf)kdf, (HpkeAead)aead, publicKey.Length)
            ? new ObliviousDohConfig((HpkeKem)kem, (HpkeKdf)kdf, (HpkeAead)aead, publicKey.ToArray())
            : null;
    }

    private static bool IsUsable(HpkeKem kem, HpkeKdf kdf, HpkeAead aead, int publicKeyLength) =>
        Enum.IsDefined(kem) && kdf == HpkeKdf.HkdfSha256 && Enum.IsDefined(aead) && publicKeyLength == Hpke.GetEncapsulatedKeySize(kem);
}
