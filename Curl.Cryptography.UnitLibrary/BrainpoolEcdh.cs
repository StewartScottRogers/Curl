using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// Elliptic-curve Diffie-Hellman over the brainpool curves (RFC 5639; SEC 1 section 3.3.1,
/// ECSVDP-DH with cofactor 1): a private key is a big-endian scalar d in [1, q - 1] of
/// <see cref="GetPrivateKeyLength" /> bytes, a public key the uncompressed point
/// 0x04 || x || y, and the shared secret the x-coordinate of d times the peer's point,
/// <see cref="GetSharedSecretLength" /> bytes with leading zeros kept - TLS 1.3's
/// <c>brainpoolP*r1tls13</c> key shares (RFC 8734, RFC 8446 section 7.4.2) and TLS 1.2's
/// premaster secret (RFC 7027, RFC 8422 section 5.10).
/// </summary>
/// <remarks>
/// Constant-time in the private key: <see cref="BrainpoolPoint.MultiplyScalar" /> is a
/// fixed-window ladder with no secret-dependent branch or table index, the key's range
/// check runs whatever the key, and the affine conversion is a fixed exponentiation.
/// Every secret intermediate is zeroed before returning. The peer's point is public and is
/// checked before it is used: one off the curve, at infinity, or of the wrong form is a
/// <see langword="false" /> return, the typed failure ADR-0118 names.
/// </remarks>
public static class BrainpoolEcdh
{
    /// <summary>Extra random bytes reduced with a generated key, so its bias modulo q is below 2^-64 (FIPS 186-4 B.4.1).</summary>
    private const int ExtraRandomLength = 8;

    /// <summary>Returns the length in bytes of a private key on <paramref name="curve" />: 32, 48 or 64.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="curve" /> is not a <see cref="BrainpoolCurve" /> value.</exception>
    public static int GetPrivateKeyLength(BrainpoolCurve curve) => BrainpoolDomainParameters.For(curve).Length;

    /// <summary>Returns the length in bytes of an uncompressed public key on <paramref name="curve" />: 65, 97 or 129.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="curve" /> is not a <see cref="BrainpoolCurve" /> value.</exception>
    public static int GetPublicKeyLength(BrainpoolCurve curve) => 1 + (2 * BrainpoolDomainParameters.For(curve).Length);

    /// <summary>Returns the length in bytes of a shared secret on <paramref name="curve" />: 32, 48 or 64.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="curve" /> is not a <see cref="BrainpoolCurve" /> value.</exception>
    public static int GetSharedSecretLength(BrainpoolCurve curve) => BrainpoolDomainParameters.For(curve).Length;

    /// <summary>
    /// Writes a private key in [1, q - 1] drawn from <see cref="RandomNumberGenerator" /> to
    /// <paramref name="privateKey" />: 8 more random bytes than the key reduced modulo q, as
    /// FIPS 186-4 appendix B.4.1 extra random bits do, with the one value 0 made 1.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="privateKey" /> is not <see cref="GetPrivateKeyLength" /> bytes.</exception>
    public static void GeneratePrivateKey(BrainpoolCurve curve, Span<byte> privateKey)
    {
        BrainpoolDomainParameters domain = BrainpoolDomainParameters.For(curve);
        RequireLength(privateKey, domain.Length, nameof(privateKey));
        byte[] random = new byte[domain.Length + ExtraRandomLength];
        try
        {
            RandomNumberGenerator.Fill(random);
            DerivePrivateKey(domain, random, privateKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(random);
        }
    }

    /// <summary>
    /// Writes the public key d * G, uncompressed, to <paramref name="publicKey" />.
    /// </summary>
    /// <exception cref="ArgumentException">A span has the wrong length, or the private key is not in [1, q - 1].</exception>
    public static void ComputePublicKey(BrainpoolCurve curve, ReadOnlySpan<byte> privateKey, Span<byte> publicKey)
    {
        BrainpoolDomainParameters domain = BrainpoolDomainParameters.For(curve);
        RequirePrivateKey(domain, privateKey);
        RequireLength(publicKey, 1 + (2 * domain.Length), nameof(publicKey));
        ComputePublicKey(domain, privateKey, publicKey);
    }

    /// <summary>
    /// Computes the shared secret, the x-coordinate of <paramref name="privateKey" /> times
    /// the peer's point, into <paramref name="sharedSecret" />.
    /// </summary>
    /// <returns>
    /// <see langword="false" />, with <paramref name="sharedSecret" /> all zero, when the
    /// peer's key is not an uncompressed point of the curve (wrong length or form, a
    /// coordinate not below p, a point off the curve, or the point at infinity); the caller
    /// must reject it. Otherwise <see langword="true" />.
    /// </returns>
    /// <exception cref="ArgumentException">The private key or the destination has the wrong length, or the private key is not in [1, q - 1].</exception>
    public static bool TryComputeSharedSecret(BrainpoolCurve curve, ReadOnlySpan<byte> privateKey, ReadOnlySpan<byte> peerPublicKey, Span<byte> sharedSecret)
    {
        BrainpoolDomainParameters domain = BrainpoolDomainParameters.For(curve);
        RequirePrivateKey(domain, privateKey);
        RequireLength(sharedSecret, domain.Length, nameof(sharedSecret));
        int n = domain.LimbCount;
        uint[] memory = new uint[5 * n];
        Span<uint> point = memory.AsSpan(0, 3 * n);
        Span<uint> x = memory.AsSpan(3 * n, n);
        Span<uint> y = memory.AsSpan(4 * n, n);
        try
        {
            bool valid = BrainpoolPoint.TryDecode(domain, peerPublicKey, point);
            BrainpoolPoint.MultiplyScalar(domain, privateKey, point, point);
            valid &= BrainpoolPoint.ToAffine(domain, point, x, y);
            MontgomeryModulus.FromLimbs(x, sharedSecret);
            if (!valid)
            {
                CryptographicOperations.ZeroMemory(sharedSecret);
            }

            return valid;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(memory.AsSpan()));
        }
    }

    /// <summary>
    /// Writes (<paramref name="random" /> mod q), or 1 where that is 0, to
    /// <paramref name="privateKey" />, <see cref="BrainpoolDomainParameters.Length" /> bytes.
    /// The 0 case is a mask, not a branch.
    /// </summary>
    internal static void DerivePrivateKey(BrainpoolDomainParameters domain, ReadOnlySpan<byte> random, Span<byte> privateKey)
    {
        uint[] memory = new uint[((random.Length + 3) / 4) + domain.LimbCount];
        Span<uint> wide = memory.AsSpan(0, (random.Length + 3) / 4);
        Span<uint> scalar = memory.AsSpan(wide.Length);
        try
        {
            MontgomeryModulus.ToLimbs(random, wide);
            domain.Order.Reduce(wide, scalar);
            uint accumulator = 0;
            foreach (uint limb in scalar)
            {
                accumulator |= limb;
            }

            scalar[0] |= ((accumulator | (0u - accumulator)) >> 31) ^ 1u;
            MontgomeryModulus.FromLimbs(scalar, privateKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(memory.AsSpan()));
        }
    }

    /// <summary>Writes d * G, uncompressed, to <paramref name="publicKey" />, for a checked private key d.</summary>
    internal static void ComputePublicKey(BrainpoolDomainParameters domain, ReadOnlySpan<byte> privateKey, Span<byte> publicKey)
    {
        int n = domain.LimbCount;
        uint[] memory = new uint[5 * n];
        Span<uint> point = memory.AsSpan(0, 3 * n);
        Span<uint> x = memory.AsSpan(3 * n, n);
        Span<uint> y = memory.AsSpan(4 * n, n);
        try
        {
            BrainpoolPoint.MultiplyScalar(domain, privateKey, domain.Generator, point);
            BrainpoolPoint.ToAffine(domain, point, x, y);
            BrainpoolPoint.EncodeUncompressed(domain, x, y, publicKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(memory.AsSpan()));
        }
    }

    /// <summary>Throws unless <paramref name="privateKey" /> is <see cref="BrainpoolDomainParameters.Length" /> bytes and in [1, q - 1].</summary>
    internal static void RequirePrivateKey(BrainpoolDomainParameters domain, ReadOnlySpan<byte> privateKey)
    {
        RequireLength(privateKey, domain.Length, nameof(privateKey));
        uint[] limbs = new uint[domain.LimbCount];
        bool inRange = domain.TryReadScalar(privateKey, limbs);
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(limbs.AsSpan()));
        if (!inRange)
        {
            throw new ArgumentException("A brainpool private key must lie in [1, q - 1].", nameof(privateKey));
        }
    }

    private static void RequireLength(ReadOnlySpan<byte> value, int length, string name)
    {
        if (value.Length != length)
        {
            throw new ArgumentException($"{name} must be {length} bytes; it is {value.Length}.", name);
        }
    }
}
