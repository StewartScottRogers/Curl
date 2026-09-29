using System.Numerics;
using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Ntlm;

/// <summary>
/// DES as NTLM uses it (MS-NLMP section 6): keyed by 7 bytes spread over DES's 8 with the
/// parity bits set odd, as curl's <c>extend_key_56_to_64</c> and
/// <c>curl_des_set_odd_parity</c> do (<c>lib/curl_ntlm_core.c</c>), and <c>DESL</c>, which
/// encrypts one block under the three 7-byte thirds of a 16-byte key padded to 21
/// (<c>Curl_ntlm_core_lm_resp</c>). Runs on the hand-built <see cref="Des" />, which takes
/// the weak keys a password hash can produce (ADR-0156).
/// </summary>
internal static class NtlmDes
{
    /// <summary>The length of a <see cref="Desl" /> result.</summary>
    public const int DeslLength = 24;

    private const int SevenByteKeyLength = 7;

    /// <summary>Encrypts the 8-byte <paramref name="block" /> under the 7-byte <paramref name="sevenByteKey" /> into <paramref name="destination" />.</summary>
    public static void Encrypt(ReadOnlySpan<byte> sevenByteKey, ReadOnlySpan<byte> block, Span<byte> destination)
    {
        Span<byte> key = stackalloc byte[Des.KeySize];
        try
        {
            ExpandKey(sevenByteKey, key);
            using Des des = new(key);
            des.EncryptBlock(block, destination);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>
    /// MS-NLMP's <c>DESL(K, D)</c>: the 8-byte <paramref name="block" /> encrypted under bytes
    /// 0 to 6, 7 to 13 and 14 to 20 of the 16-byte <paramref name="key" /> followed by five
    /// zero bytes.
    /// </summary>
    public static byte[] Desl(ReadOnlySpan<byte> key, ReadOnlySpan<byte> block)
    {
        Span<byte> padded = stackalloc byte[3 * SevenByteKeyLength];
        try
        {
            padded.Clear();
            key.CopyTo(padded);
            byte[] result = new byte[DeslLength];
            for (int third = 0; third < 3; third++)
            {
                Encrypt(padded.Slice(third * SevenByteKeyLength, SevenByteKeyLength), block, result.AsSpan(third * Des.BlockSize, Des.BlockSize));
            }

            return result;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(padded);
        }
    }

    /// <summary>
    /// Spreads the 56 bits of <paramref name="sevenByteKey" /> over the high seven bits of
    /// each of the 8 bytes of <paramref name="key" /> and sets each low bit for odd parity.
    /// </summary>
    internal static void ExpandKey(ReadOnlySpan<byte> sevenByteKey, Span<byte> key)
    {
        key[0] = sevenByteKey[0];
        for (int index = 1; index < SevenByteKeyLength; index++)
        {
            key[index] = (byte)((sevenByteKey[index - 1] << (8 - index)) | (sevenByteKey[index] >> index));
        }

        key[7] = (byte)(sevenByteKey[6] << 1);
        for (int index = 0; index < Des.KeySize; index++)
        {
            int highBitsSet = BitOperations.PopCount((uint)(key[index] >> 1));
            key[index] = (byte)((key[index] & 0xFE) | ((highBitsSet & 1) ^ 1));
        }
    }
}
