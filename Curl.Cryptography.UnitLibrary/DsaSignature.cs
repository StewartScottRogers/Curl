using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// DSA (FIPS 186-4 section 4) over a hash the caller computed: an instance holds a private
/// key and signs with RFC 6979's deterministic nonce, and <see cref="VerifyHash" /> checks a
/// signature against a public key. A signature is r || s, each the length of q, big-endian:
/// SSH's <c>ssh-dss</c> blob as it stands, and the two INTEGERs of TLS's DER
/// <c>Dss-Sig-Value</c> once the caller encodes them. Named so it never collides with
/// <see cref="DSA" />, which cannot create keys on macOS and does not follow FIPS 186-3
/// above 1024 bits there (ADR-0118).
/// </summary>
/// <remarks>
/// <para>
/// Domain parameters are accepted as OpenSSL's verification accepts them: q of 160, 224 or
/// 256 bits, an odd p longer than q and at most 10,000 bits, 1 &lt; g &lt; p. Every value
/// is big-endian and may carry leading zero bytes, as an SSH <c>mpint</c> does.
/// </para>
/// <para>
/// Signing is constant-time in x and k: the exponentiations are
/// <see cref="MontgomeryModulus" />'s fixed-window ladder, whose table look-up reads every
/// entry, k^-1 is k^(q-2) mod q by the same ladder, and the rest is masked fixed-width limb
/// arithmetic, so no branch, loop bound or table index depends on a secret. The one branch
/// in signing is RFC 6979's rejection of a candidate k outside [1, q - 1] or giving r or s
/// of 0, and it reveals only that a discarded candidate was discarded. The private key is
/// zeroed on <see cref="Dispose" />, and every intermediate before each call returns.
/// Verification works on public values only.
/// </para>
/// </remarks>
public sealed class DsaSignature : IDisposable
{
    private readonly DsaDomainParameters domain;
    private readonly uint[] privateKey;
    private readonly byte[] privateKeyOctets;
    private bool disposed;

    /// <summary>Copies the domain parameters and the private key x.</summary>
    /// <param name="prime">p, big-endian.</param>
    /// <param name="subprime">q, big-endian.</param>
    /// <param name="generator">g, big-endian.</param>
    /// <param name="privateKey">x, big-endian, 0 &lt; x &lt; q.</param>
    /// <exception cref="ArgumentException">The domain parameters are outside the accepted sizes and ranges, or x is not in [1, q - 1].</exception>
    public DsaSignature(ReadOnlySpan<byte> prime, ReadOnlySpan<byte> subprime, ReadOnlySpan<byte> generator, ReadOnlySpan<byte> privateKey)
    {
        domain = DsaDomainParameters.TryCreate(prime, subprime, generator)
            ?? throw new ArgumentException("DSA domain parameters need q of 160, 224 or 256 bits, an odd p longer than q of at most 10,000 bits, and 1 < g < p.", nameof(prime));
        this.privateKey = new uint[domain.Subprime.LimbCount];
        privateKeyOctets = new byte[domain.SubprimeLength];
        if (!TryReadBelowSubprime(domain, privateKey, this.privateKey))
        {
            throw new ArgumentException("The DSA private key x must lie in [1, q - 1].", nameof(privateKey));
        }

        MontgomeryModulus.FromLimbs(this.privateKey, privateKeyOctets);
    }

    /// <summary>The length in bytes of a signature, r || s: twice the length of q.</summary>
    public int SignatureLength => 2 * domain.SubprimeLength;

    /// <summary>
    /// Returns whether <paramref name="signature" /> is a valid DSA signature over
    /// <paramref name="hash" /> by the public key y under the domain parameters. Anything
    /// wrong - parameters outside the accepted sizes, y not below p, a signature of the wrong
    /// length, r or s of 0 or not below q, or a signature that does not match - is
    /// <see langword="false" />.
    /// </summary>
    /// <param name="prime">p, big-endian.</param>
    /// <param name="subprime">q, big-endian.</param>
    /// <param name="generator">g, big-endian.</param>
    /// <param name="publicKey">y, big-endian.</param>
    /// <param name="hash">The message's hash, any length; its leftmost N bits are used.</param>
    /// <param name="signature">r || s, each exactly the length of q.</param>
    /// <returns><see langword="true" /> when the signature is valid.</returns>
    public static bool VerifyHash(ReadOnlySpan<byte> prime, ReadOnlySpan<byte> subprime, ReadOnlySpan<byte> generator, ReadOnlySpan<byte> publicKey, ReadOnlySpan<byte> hash, ReadOnlySpan<byte> signature)
    {
        DsaDomainParameters? domain = DsaDomainParameters.TryCreate(prime, subprime, generator);
        return domain is not null && signature.Length == 2 * domain.SubprimeLength && Verify(domain, publicKey, hash, signature);
    }

    /// <summary>
    /// Writes the signature r || s over <paramref name="hash" />, with the nonce k derived
    /// from x and the hash by RFC 6979 section 3.2, so the same key and hash always give the
    /// same signature.
    /// </summary>
    /// <param name="hash">The message's hash, exactly <paramref name="hashAlgorithm" />'s length.</param>
    /// <param name="hashAlgorithm">The hash the message was hashed with, and RFC 6979's HMAC runs on: SHA1, SHA224 (by its name, which <see cref="HashAlgorithmName" /> has no property for), SHA256, SHA384 or SHA512.</param>
    /// <param name="destination">Receives r || s, exactly <see cref="SignatureLength" /> bytes.</param>
    /// <exception cref="ArgumentException">The hash algorithm is not one of those, or a length is wrong.</exception>
    /// <exception cref="ObjectDisposedException">The key was disposed.</exception>
    public void SignHash(ReadOnlySpan<byte> hash, HashAlgorithmName hashAlgorithm, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        RequireLength(hash, DeterministicDsaNonce.DigestLength(hashAlgorithm), nameof(hash));
        RequireLength(destination, SignatureLength, nameof(destination));
        int n = domain.Subprime.LimbCount;
        int length = domain.SubprimeLength;
        uint[] work = new uint[3 * n];
        Span<uint> z = work.AsSpan(0, n);
        Span<uint> r = work.AsSpan(n, n);
        Span<uint> s = work.AsSpan(2 * n, n);
        byte[] octets = new byte[2 * length];
        Span<byte> reducedHash = octets.AsSpan(0, length);
        Span<byte> nonce = octets.AsSpan(length, length);
        try
        {
            domain.ReduceHash(hash, z);
            MontgomeryModulus.FromLimbs(z, reducedHash);
            using DeterministicDsaNonce nonces = new(hashAlgorithm, privateKeyOctets, reducedHash);
            do
            {
                nonces.NextCandidate(nonce);
            }
            while (!TrySign(nonce, z, r, s));

            MontgomeryModulus.FromLimbs(r, destination[..length]);
            MontgomeryModulus.FromLimbs(s, destination[length..]);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(work.AsSpan()));
            CryptographicOperations.ZeroMemory(octets);
        }
    }

    /// <summary>Zeroes the private key; any later <see cref="SignHash" /> throws <see cref="ObjectDisposedException" />.</summary>
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(privateKey.AsSpan()));
        CryptographicOperations.ZeroMemory(privateKeyOctets);
        disposed = true;
    }

    private static void RequireLength(ReadOnlySpan<byte> value, int length, string name)
    {
        if (value.Length != length)
        {
            throw new ArgumentException($"{name} must be {length} bytes; it is {value.Length}.", name);
        }
    }

    /// <summary>
    /// Reads the big-endian <paramref name="value" /> into <paramref name="limbs" /> (q's
    /// limbs) and returns whether it lies in [1, q - 1]. Bytes beyond q's length must be
    /// zero. Every check runs whatever the value, so a secret x costs the same time whatever
    /// it is.
    /// </summary>
    private static bool TryReadBelowSubprime(DsaDomainParameters domain, ReadOnlySpan<byte> value, Span<uint> limbs)
    {
        int excess = Math.Max(0, value.Length - domain.SubprimeLength);
        bool leadingZeros = ConstantTime.IsAllZero(value[..excess]);
        MontgomeryModulus.ToLimbs(value[excess..], limbs);
        return leadingZeros & domain.Subprime.IsBelowModulus(limbs) & !IsZero(limbs);
    }

    private static bool IsZero(ReadOnlySpan<uint> limbs) => ConstantTime.IsAllZero(MemoryMarshal.AsBytes(limbs));

    /// <summary>
    /// FIPS 186-4 section 4.7: w = s^-1, u1 = z * w, u2 = r * w mod q, then
    /// v = (g^u1 * y^u2 mod p) mod q must equal r.
    /// </summary>
    private static bool Verify(DsaDomainParameters domain, ReadOnlySpan<byte> publicKey, ReadOnlySpan<byte> hash, ReadOnlySpan<byte> signature)
    {
        int n = domain.Subprime.LimbCount;
        int m = domain.Prime.LimbCount;
        int length = domain.SubprimeLength;
        uint[] work = new uint[(5 * n) + (3 * m)];
        Span<uint> r = work.AsSpan(0, n);
        Span<uint> s = work.AsSpan(n, n);
        Span<uint> w = work.AsSpan(2 * n, n);
        Span<uint> u1 = work.AsSpan(3 * n, n);
        Span<uint> u2 = work.AsSpan(4 * n, n);
        Span<uint> y = work.AsSpan(5 * n, m);
        Span<uint> power = work.AsSpan((5 * n) + m, m);
        Span<uint> v = work.AsSpan((5 * n) + (2 * m), m);
        if (!TryReadPublicKey(domain, publicKey, y)
            || !TryReadBelowSubprime(domain, signature[..length], r)
            || !TryReadBelowSubprime(domain, signature[length..], s))
        {
            return false;
        }

        domain.InvertModuloSubprime(s, w);
        domain.ReduceHash(hash, u1);
        domain.Subprime.MultiplyModulo(u1, u1, w);
        domain.Subprime.MultiplyModulo(u2, r, w);
        byte[] exponents = new byte[2 * length];
        MontgomeryModulus.FromLimbs(u1, exponents.AsSpan(0, length));
        MontgomeryModulus.FromLimbs(u2, exponents.AsSpan(length));
        domain.RaiseGenerator(exponents.AsSpan(0, length), power);
        domain.Prime.Exponentiate(y, exponents.AsSpan(length), v);
        domain.Prime.MultiplyModulo(v, power, v);
        domain.Subprime.Reduce(v, w);
        return CryptographicOperations.FixedTimeEquals(MemoryMarshal.AsBytes(w), MemoryMarshal.AsBytes(r));
    }

    /// <summary>Reads y into <paramref name="limbs" /> (p's limbs) and returns whether it is below p.</summary>
    private static bool TryReadPublicKey(DsaDomainParameters domain, ReadOnlySpan<byte> publicKey, Span<uint> limbs)
    {
        publicKey = DsaDomainParameters.TrimLeadingZeros(publicKey);
        if (publicKey.Length > domain.PrimeLength)
        {
            return false;
        }

        MontgomeryModulus.ToLimbs(publicKey, limbs);
        return domain.Prime.IsBelowModulus(limbs);
    }

    /// <summary>
    /// Makes the signature for the candidate nonce k: r = (g^k mod p) mod q and
    /// s = k^-1 * (z + x * r) mod q, and returns whether k is in [1, q - 1] and r and s are
    /// nonzero. Every step runs whatever the answer, which is combined without branching.
    /// </summary>
    private bool TrySign(ReadOnlySpan<byte> nonce, ReadOnlySpan<uint> z, Span<uint> r, Span<uint> s)
    {
        int n = domain.Subprime.LimbCount;
        int m = domain.Prime.LimbCount;
        uint[] work = new uint[(4 * n) + 2 + m];
        Span<uint> k = work.AsSpan(0, n);
        Span<uint> kInverse = work.AsSpan(n, n);
        Span<uint> sum = work.AsSpan(2 * n, n);
        Span<uint> scratch = work.AsSpan(3 * n, n + 2);
        Span<uint> power = work.AsSpan((4 * n) + 2, m);
        try
        {
            MontgomeryModulus.ToLimbs(nonce, k);
            bool nonceInRange = domain.Subprime.IsBelowModulus(k) & !IsZero(k);
            domain.RaiseGenerator(nonce, power);
            domain.Subprime.Reduce(power, r);
            domain.InvertModuloSubprime(k, kInverse);
            domain.Subprime.MultiplyModulo(sum, privateKey, r);
            domain.Subprime.Add(sum, sum, z, scratch);
            domain.Subprime.MultiplyModulo(s, kInverse, sum);
            return nonceInRange & !IsZero(r) & !IsZero(s);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(work.AsSpan()));
        }
    }
}
