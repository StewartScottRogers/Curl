using System.Security.Cryptography;
using System.Text;
using Curl.Cryptography;

namespace Curl.Ntlm;

/// <summary>
/// The password hashes of MS-NLMP section 3.3: <c>NTOWFv1</c>, <c>LMOWFv1</c> and
/// <c>NTOWFv2</c>, computed from strings as curl 8.21.0 computes them in
/// <c>lib/curl_ntlm_core.c</c>, which for ASCII is exactly MS-NLMP's definition.
/// </summary>
public static class NtlmOneWayFunctions
{
    /// <summary>The length of every hash here.</summary>
    public const int HashLength = 16;

    /// <summary>The longest password prefix <c>LMOWFv1</c> reads.</summary>
    public const int LmPasswordLength = 14;

    // "KGS!@#$%", the constant LMOWFv1 encrypts.
    private static ReadOnlySpan<byte> LmMagic => "KGS!@#$%"u8;

    /// <summary>
    /// <c>NTOWFv1</c>: MD4 of the password, each UTF-8 byte widened to 16 bits
    /// (<c>Curl_ntlm_core_mk_nt_hash</c>).
    /// </summary>
    public static byte[] ComputeNtOwfV1(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        byte[] widened = NtlmCurlString.Widen(password);
        byte[] hash = new byte[HashLength];
        Md4.HashData(widened, hash);
        CryptographicOperations.ZeroMemory(widened);
        return hash;
    }

    /// <summary>
    /// <c>LMOWFv1</c>: the password's first <see cref="LmPasswordLength" /> UTF-8 bytes,
    /// ASCII letters uppercased and zero-padded to 14, each 7-byte half the DES key that
    /// encrypts <c>KGS!@#$%</c> (<c>Curl_ntlm_core_mk_lm_hash</c>).
    /// </summary>
    public static byte[] ComputeLmOwfV1(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        byte[] bytes = NtlmCurlString.UppercaseAscii(Encoding.UTF8.GetBytes(password));
        byte[] padded = new byte[LmPasswordLength];
        bytes.AsSpan(0, Math.Min(bytes.Length, LmPasswordLength)).CopyTo(padded);
        byte[] hash = new byte[HashLength];
        NtlmDes.Encrypt(padded.AsSpan(0, 7), LmMagic, hash.AsSpan(0, 8));
        NtlmDes.Encrypt(padded.AsSpan(7, 7), LmMagic, hash.AsSpan(8, 8));
        CryptographicOperations.ZeroMemory(bytes);
        CryptographicOperations.ZeroMemory(padded);
        return hash;
    }

    /// <summary>
    /// <c>NTOWFv2</c> (and <c>LMOWFv2</c>, the same): HMAC-MD5 keyed by
    /// <paramref name="ntOwfV1" /> over the user uppercased and the domain as it is, both
    /// widened (<c>Curl_ntlm_core_mk_ntlmv2_hash</c>). curl uppercases ASCII letters only,
    /// where MS-NLMP uppercases every character.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="ntOwfV1" /> is not <see cref="HashLength" /> bytes.</exception>
    public static byte[] ComputeNtOwfV2(string user, string domain, ReadOnlySpan<byte> ntOwfV1)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(domain);
        if (ntOwfV1.Length != HashLength)
        {
            throw new ArgumentException($"NTOWFv2 needs the {HashLength}-byte NTOWFv1; this is {ntOwfV1.Length} bytes.", nameof(ntOwfV1));
        }

        byte[] identity = [.. NtlmCurlString.WidenUppercase(user), .. NtlmCurlString.Widen(domain)];
        return HMACMD5.HashData(ntOwfV1, identity);
    }
}
