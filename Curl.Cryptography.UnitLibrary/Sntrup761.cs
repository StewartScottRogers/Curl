using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// Streamlined NTRU Prime's sntrup761 key encapsulation ("NTRU Prime: round 3",
/// 2020-10-07, p = 761, q = 4591, w = 286), byte for byte the round-3 reference and
/// OpenSSH's <c>sntrup761.c</c>, which SSH's <c>sntrup761x25519-sha512</c> key exchange
/// uses: a 1158-byte public key, a 1763-byte secret key, a 1039-byte ciphertext and a
/// 32-byte shared secret, hashed with SHA-512 truncated to 32 bytes.
/// </summary>
/// <remarks>
/// Constant-time in every secret: polynomial arithmetic, sorting and encoding run the
/// same steps whatever the coefficients, and decapsulation compares the re-encrypted
/// ciphertext by mask and always hashes, so a tampered ciphertext yields the
/// implicit-rejection secret rather than an error. Key generation retries a random g
/// that is not invertible modulo 3, which reveals only that a retry happened, as the
/// reference does. Every secret temporary is zeroed before returning.
/// </remarks>
public static class Sntrup761
{
    /// <summary>The length in bytes of a public key.</summary>
    public const int PublicKeySize = Sntrup761Encoding.RqSize;

    /// <summary>
    /// The length in bytes of a secret key: f, 1/g, the public key, the rejection value
    /// rho and the public key's hash.
    /// </summary>
    public const int SecretKeySize = (2 * Sntrup761Encoding.SmallSize) + PublicKeySize + Sntrup761Encoding.SmallSize + HashSize;

    /// <summary>The length in bytes of a ciphertext: the rounded polynomial and its 32-byte confirmation hash.</summary>
    public const int CiphertextSize = Sntrup761Encoding.RoundedSize + HashSize;

    /// <summary>The length in bytes of a shared secret.</summary>
    public const int SharedSecretSize = HashSize;

    /// <summary>
    /// The random bytes one encapsulation consumes: one little-endian 32-bit word per
    /// coefficient of the short polynomial r, 4p.
    /// </summary>
    public const int EncapsulationRandomSize = 4 * Sntrup761Ring.P;

    /// <summary>
    /// The random bytes one key generation consumes, in the order the reference draws
    /// them: 4p for g, 4p for f, then the 191 bytes of rho.
    /// </summary>
    public const int KeyGenerationRandomSize = (2 * EncapsulationRandomSize) + Sntrup761Encoding.SmallSize;

    private const int HashSize = 32;

    private const int PublicKeyOffset = 2 * Sntrup761Encoding.SmallSize;

    private const int RhoOffset = PublicKeyOffset + PublicKeySize;

    private const int CacheOffset = RhoOffset + Sntrup761Encoding.SmallSize;

    /// <summary>Fills a buffer with random bytes; <see cref="RandomNumberGenerator.Fill" /> outside tests.</summary>
    internal delegate void RandomSource(Span<byte> destination);

    /// <summary>
    /// Generates a key pair from <see cref="RandomNumberGenerator" /> into
    /// <paramref name="publicKey" /> and <paramref name="secretKey" />.
    /// </summary>
    /// <exception cref="ArgumentException">A span has the wrong length.</exception>
    public static void GenerateKeyPair(Span<byte> publicKey, Span<byte> secretKey) =>
        GenerateKeyPair(publicKey, secretKey, RandomNumberGenerator.Fill);

    /// <summary>
    /// Generates a key pair from the <see cref="KeyGenerationRandomSize" /> bytes of
    /// <paramref name="random" />, exactly as the reference does when its first g is
    /// invertible, so published known answers reproduce.
    /// </summary>
    /// <returns>
    /// <c>false</c>, with both keys zeroed, when the g those bytes give has no inverse
    /// modulo 3 and the reference would draw another; otherwise <c>true</c>.
    /// </returns>
    /// <exception cref="ArgumentException">A span has the wrong length.</exception>
    public static bool TryGenerateKeyPair(ReadOnlySpan<byte> random, Span<byte> publicKey, Span<byte> secretKey)
    {
        RequireSize(random.Length, KeyGenerationRandomSize, nameof(random));
        RequireSize(publicKey.Length, PublicKeySize, nameof(publicKey));
        RequireSize(secretKey.Length, SecretKeySize, nameof(secretKey));
        Span<short> polynomials = stackalloc short[4 * Sntrup761Ring.P];
        Span<short> g = polynomials[..Sntrup761Ring.P];
        Span<short> gReciprocal = polynomials.Slice(Sntrup761Ring.P, Sntrup761Ring.P);
        Span<short> f = polynomials.Slice(2 * Sntrup761Ring.P, Sntrup761Ring.P);
        Span<short> h = polynomials.Slice(3 * Sntrup761Ring.P, Sntrup761Ring.P);
        try
        {
            Sntrup761Ring.SmallFromRandom(g, random[..EncapsulationRandomSize]);
            if (Sntrup761Ring.ReciprocalModThree(gReciprocal, g) != 0)
            {
                publicKey.Clear();
                secretKey.Clear();
                return false;
            }

            Sntrup761Ring.ShortFromRandom(f, random.Slice(EncapsulationRandomSize, EncapsulationRandomSize));
            Sntrup761Ring.ReciprocalOfThreeTimesModQ(h, f);
            Sntrup761Ring.MultiplyModQ(h, h, g);
            Sntrup761Encoding.EncodeRq(publicKey, h);
            Sntrup761Encoding.EncodeSmall(secretKey[..Sntrup761Encoding.SmallSize], f);
            Sntrup761Encoding.EncodeSmall(secretKey.Slice(Sntrup761Encoding.SmallSize, Sntrup761Encoding.SmallSize), gReciprocal);
            publicKey.CopyTo(secretKey.Slice(PublicKeyOffset, PublicKeySize));
            random[(2 * EncapsulationRandomSize)..].CopyTo(secretKey.Slice(RhoOffset, Sntrup761Encoding.SmallSize));
            HashWithPrefix(secretKey[CacheOffset..], 4, publicKey);
            return true;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(polynomials));
        }
    }

    /// <summary>
    /// Encapsulates a fresh shared secret to <paramref name="publicKey" /> with randomness
    /// from <see cref="RandomNumberGenerator" />.
    /// </summary>
    /// <exception cref="ArgumentException">A span has the wrong length.</exception>
    public static void Encapsulate(ReadOnlySpan<byte> publicKey, Span<byte> ciphertext, Span<byte> sharedSecret)
    {
        Span<byte> random = stackalloc byte[EncapsulationRandomSize];
        try
        {
            RandomNumberGenerator.Fill(random);
            Encapsulate(publicKey, random, ciphertext, sharedSecret);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(random);
        }
    }

    /// <summary>
    /// Encapsulates to <paramref name="publicKey" /> with the short polynomial r drawn from
    /// the <see cref="EncapsulationRandomSize" /> bytes of <paramref name="random" />,
    /// writing the ciphertext and the shared secret.
    /// </summary>
    /// <exception cref="ArgumentException">A span has the wrong length.</exception>
    public static void Encapsulate(
        ReadOnlySpan<byte> publicKey,
        ReadOnlySpan<byte> random,
        Span<byte> ciphertext,
        Span<byte> sharedSecret)
    {
        RequireSize(publicKey.Length, PublicKeySize, nameof(publicKey));
        RequireSize(random.Length, EncapsulationRandomSize, nameof(random));
        RequireSize(ciphertext.Length, CiphertextSize, nameof(ciphertext));
        RequireSize(sharedSecret.Length, SharedSecretSize, nameof(sharedSecret));
        Span<byte> cache = stackalloc byte[HashSize];
        Span<byte> encodedR = stackalloc byte[Sntrup761Encoding.SmallSize];
        Span<short> r = stackalloc short[Sntrup761Ring.P];
        try
        {
            HashWithPrefix(cache, 4, publicKey);
            Sntrup761Ring.ShortFromRandom(r, random);
            Hide(ciphertext, encodedR, r, publicKey, cache);
            HashSession(sharedSecret, 1, encodedR, ciphertext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encodedR);
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(r));
        }
    }

    /// <summary>
    /// Decapsulates <paramref name="ciphertext" /> with <paramref name="secretKey" /> into
    /// <paramref name="sharedSecret" />. A ciphertext that does not re-encrypt to itself
    /// gives the implicit-rejection secret, SHA-512(0, SHA-512(3, rho), ciphertext)
    /// truncated, never an error, so the peer learns nothing it can tell apart.
    /// </summary>
    /// <exception cref="ArgumentException">A span has the wrong length.</exception>
    public static void Decapsulate(ReadOnlySpan<byte> secretKey, ReadOnlySpan<byte> ciphertext, Span<byte> sharedSecret)
    {
        RequireSize(secretKey.Length, SecretKeySize, nameof(secretKey));
        RequireSize(ciphertext.Length, CiphertextSize, nameof(ciphertext));
        RequireSize(sharedSecret.Length, SharedSecretSize, nameof(sharedSecret));
        ReadOnlySpan<byte> publicKey = secretKey.Slice(PublicKeyOffset, PublicKeySize);
        ReadOnlySpan<byte> rho = secretKey.Slice(RhoOffset, Sntrup761Encoding.SmallSize);
        Span<short> r = stackalloc short[Sntrup761Ring.P];
        Span<byte> encodedR = stackalloc byte[Sntrup761Encoding.SmallSize];
        Span<byte> reencrypted = stackalloc byte[CiphertextSize];
        try
        {
            DecryptCore(r, ciphertext, secretKey);
            Hide(reencrypted, encodedR, r, publicKey, secretKey[CacheOffset..]);
            int mask = DifferenceMask(ciphertext, reencrypted);
            for (int index = 0; index < Sntrup761Encoding.SmallSize; index++)
            {
                encodedR[index] ^= (byte)(mask & (encodedR[index] ^ rho[index]));
            }

            HashSession(sharedSecret, 1 + mask, encodedR, ciphertext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(r));
            CryptographicOperations.ZeroMemory(encodedR);
            CryptographicOperations.ZeroMemory(reencrypted);
        }
    }

    /// <summary>The convenience key generation with its random source injected, so a retry can be tested.</summary>
    internal static void GenerateKeyPair(Span<byte> publicKey, Span<byte> secretKey, RandomSource fillRandom)
    {
        RequireSize(publicKey.Length, PublicKeySize, nameof(publicKey));
        RequireSize(secretKey.Length, SecretKeySize, nameof(secretKey));
        Span<byte> random = stackalloc byte[KeyGenerationRandomSize];
        try
        {
            do
            {
                fillRandom(random);
            }
            while (!TryGenerateKeyPair(random, publicKey, secretKey));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(random);
        }
    }

    private static void RequireSize(int length, int expected, string parameterName)
    {
        if (length != expected)
        {
            throw new ArgumentException($"sntrup761 needs {expected} bytes here; this span is {length}.", parameterName);
        }
    }

    /// <summary>
    /// Encrypts r to the public key and appends the confirmation hash
    /// SHA-512(2, SHA-512(3, r), SHA-512(4, pk)) (<c>Hide</c>); <paramref name="encodedR" />
    /// receives r's encoding.
    /// </summary>
    private static void Hide(
        Span<byte> ciphertext,
        Span<byte> encodedR,
        ReadOnlySpan<short> r,
        ReadOnlySpan<byte> publicKey,
        ReadOnlySpan<byte> cache)
    {
        Span<short> polynomials = stackalloc short[2 * Sntrup761Ring.P];
        Span<short> h = polynomials[..Sntrup761Ring.P];
        Span<short> product = polynomials[Sntrup761Ring.P..];
        Span<byte> confirmInput = stackalloc byte[2 * HashSize];
        try
        {
            Sntrup761Encoding.EncodeSmall(encodedR, r);
            Sntrup761Encoding.DecodeRq(h, publicKey);
            Sntrup761Ring.MultiplyModQ(product, h, r);
            Sntrup761Ring.Round(product, product);
            Sntrup761Encoding.EncodeRounded(ciphertext[..Sntrup761Encoding.RoundedSize], product);
            HashWithPrefix(confirmInput[..HashSize], 3, encodedR);
            cache.CopyTo(confirmInput[HashSize..]);
            HashWithPrefix(ciphertext[Sntrup761Encoding.RoundedSize..], 2, confirmInput);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(polynomials));
            CryptographicOperations.ZeroMemory(confirmInput);
        }
    }

    /// <summary>r from the ciphertext's rounded polynomial with the secret key's f and 1/g (<c>ZDecrypt</c>).</summary>
    private static void DecryptCore(Span<short> r, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> secretKey)
    {
        Span<short> polynomials = stackalloc short[3 * Sntrup761Ring.P];
        Span<short> f = polynomials[..Sntrup761Ring.P];
        Span<short> gReciprocal = polynomials.Slice(Sntrup761Ring.P, Sntrup761Ring.P);
        Span<short> c = polynomials.Slice(2 * Sntrup761Ring.P, Sntrup761Ring.P);
        try
        {
            Sntrup761Encoding.DecodeSmall(f, secretKey[..Sntrup761Encoding.SmallSize]);
            Sntrup761Encoding.DecodeSmall(gReciprocal, secretKey.Slice(Sntrup761Encoding.SmallSize, Sntrup761Encoding.SmallSize));
            Sntrup761Encoding.DecodeRounded(c, ciphertext[..Sntrup761Encoding.RoundedSize]);
            Sntrup761Ring.Decrypt(r, c, f, gReciprocal);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(polynomials));
        }
    }

    /// <summary>0 when the two ciphertexts are equal, -1 otherwise, reading every byte (<c>Ciphertexts_diff_mask</c>).</summary>
    private static int DifferenceMask(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        int differentBits = 0;
        for (int index = 0; index < CiphertextSize; index++)
        {
            differentBits |= left[index] ^ right[index];
        }

        return (1 & ((differentBits - 1) >> 8)) - 1;
    }

    /// <summary>The session key SHA-512(b, SHA-512(3, r), ciphertext) truncated to 32 bytes (<c>HashSession</c>).</summary>
    private static void HashSession(Span<byte> sharedSecret, int prefix, ReadOnlySpan<byte> encodedR, ReadOnlySpan<byte> ciphertext)
    {
        Span<byte> input = stackalloc byte[HashSize + CiphertextSize];
        try
        {
            HashWithPrefix(input[..HashSize], 3, encodedR);
            ciphertext.CopyTo(input[HashSize..]);
            HashWithPrefix(sharedSecret, prefix, input);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
        }
    }

    /// <summary>The first 32 bytes of SHA-512 over one prefix byte and <paramref name="input" /> (<c>Hash_prefix</c>).</summary>
    private static void HashWithPrefix(Span<byte> output, int prefix, ReadOnlySpan<byte> input)
    {
        Span<byte> message = stackalloc byte[input.Length + 1];
        Span<byte> digest = stackalloc byte[SHA512.HashSizeInBytes];
        try
        {
            message[0] = (byte)prefix;
            input.CopyTo(message[1..]);
            SHA512.HashData(message, digest);
            digest[..HashSize].CopyTo(output);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(message);
            CryptographicOperations.ZeroMemory(digest);
        }
    }
}
