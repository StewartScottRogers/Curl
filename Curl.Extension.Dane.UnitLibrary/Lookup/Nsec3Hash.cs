using System.Security.Cryptography;
using Curl.Extension.Dane.Dnssec;
using Curl.Extension.Dane.Wire;

namespace Curl.Extension.Dane.Lookup;

/// <summary>
/// RFC 5155's hashed owner names: section 5's iterated SHA-1 of a canonical name with a salt, and the Base32hex
/// (RFC 4648 section 7) first label an NSEC3 record's owner holds it in.
/// </summary>
internal static class Nsec3Hash
{
    /// <summary>The only hash algorithm RFC 5155 section 11 defines.</summary>
    public const byte Sha1Algorithm = 1;

    /// <summary>The most extra iterations hashed; RFC 9276 section 3.2 lets a validator refuse more.</summary>
    public const int MaximumIterations = 150;

    // At most 7 bits wait for the next character's 5, so 12 bits hold every pending bit.
    private const int PendingBitsMask = 0xFFF;

    private const string Base32HexAlphabet = "0123456789abcdefghijklmnopqrstuv";

    /// <summary>Hashes <paramref name="name" /> with an NSEC3 record's salt and iterations (RFC 5155 section 5).</summary>
    /// <param name="name">The name.</param>
    /// <param name="parameters">The NSEC3 record whose parameters to use.</param>
    /// <returns>The 20-octet hash.</returns>
    public static byte[] Compute(DnsName name, Nsec3RecordData parameters)
    {
        byte[] hash = SHA1.HashData([.. DnssecCanonicalForm.LowercaseName(name.Wire.Span), .. parameters.Salt]);
        for (int iteration = 0; iteration < parameters.Iterations; iteration++)
        {
            hash = SHA1.HashData([.. hash, .. parameters.Salt]);
        }

        return hash;
    }

    /// <summary>Decodes an NSEC3 owner's first label from Base32hex without padding, either case.</summary>
    /// <param name="label">The label's octets.</param>
    /// <returns>The hash, or null when a character is not Base32hex.</returns>
    public static byte[]? DecodeBase32Hex(byte[] label)
    {
        List<byte> decoded = [];
        int buffer = 0;
        int bits = 0;
        foreach (byte character in label)
        {
            int value = Base32HexAlphabet.IndexOf(char.ToLowerInvariant((char)character), StringComparison.Ordinal);
            if (value < 0)
            {
                return null;
            }

            buffer = ((buffer << 5) | value) & PendingBitsMask;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                decoded.Add((byte)(buffer >> bits));
            }
        }

        return [.. decoded];
    }
}
