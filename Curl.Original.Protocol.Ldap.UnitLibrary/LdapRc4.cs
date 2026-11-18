namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// The RC4 stream cipher (the BCL has none) that the RFC 2831 section 2.3 <c>rc4</c>, <c>rc4-56</c> and
/// <c>rc4-40</c> ciphers of a DIGEST-MD5 confidentiality layer use: one instance per direction, its
/// keystream carrying on from message to message (BL-5452).
/// </summary>
internal sealed class LdapRc4
{
    private readonly byte[] state = new byte[256];

    private int first;

    private int second;

    /// <summary>Creates the cipher with its key schedule run for <paramref name="key" />.</summary>
    /// <param name="key">The key, 1 to 256 bytes.</param>
    internal LdapRc4(ReadOnlySpan<byte> key)
    {
        for (int i = 0; i < 256; i++)
        {
            state[i] = (byte)i;
        }

        int j = 0;
        for (int i = 0; i < 256; i++)
        {
            j = (j + state[i] + key[i % key.Length]) & 0xFF;
            (state[i], state[j]) = (state[j], state[i]);
        }
    }

    /// <summary>XORs <paramref name="data" /> with the next keystream bytes; encrypting and decrypting are the same.</summary>
    /// <param name="data">The bytes to encrypt or decrypt.</param>
    /// <returns>The transformed bytes.</returns>
    internal byte[] Transform(ReadOnlySpan<byte> data)
    {
        byte[] result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            first = (first + 1) & 0xFF;
            second = (second + state[first]) & 0xFF;
            (state[first], state[second]) = (state[second], state[first]);
            result[i] = (byte)(data[i] ^ state[(state[first] + state[second]) & 0xFF]);
        }

        return result;
    }
}
