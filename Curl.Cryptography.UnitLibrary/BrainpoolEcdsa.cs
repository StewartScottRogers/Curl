using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// ECDSA (SEC 1 section 4.1; FIPS 186-4 section 6) over the brainpool curves, on a hash
/// the caller computed: an instance holds a private key and signs with RFC 6979's
/// deterministic nonce, and <see cref="VerifyHash" /> checks a signature against an
/// uncompressed public key. A signature is r || s, each <see cref="GetSignatureLength" />
/// / 2 bytes, big-endian (IEEE P1363); TLS's <c>ecdsa_brainpoolP*r1tls13_sha*</c> schemes
/// (RFC 8734) and X.509 carry the DER <c>ECDSA-Sig-Value</c>, which the caller encodes.
/// </summary>
/// <remarks>
/// <para>
/// Signing is constant-time in the private key d and the nonce k: k * G is
/// <see cref="BrainpoolPoint.MultiplyScalar" />'s fixed-window ladder with no
/// secret-dependent branch or table index, k^-1 is k^(q-2) mod q by
/// <see cref="MontgomeryModulus" />'s fixed-window exponentiation, the affine conversion
/// likewise, and the rest is masked fixed-width limb arithmetic. The one branch is RFC
/// 6979's rejection of a candidate k outside [1, q - 1] or giving r or s of 0, which
/// reveals only that a discarded candidate was discarded. The private key is zeroed on
/// <see cref="Dispose" />, and every intermediate before each call returns.
/// </para>
/// <para>
/// Verification works on public values only. Anything a peer can send wrong - a public key
/// that is not an uncompressed point of the curve, a signature of the wrong length, r or s
/// of 0 or not below q, or a signature that does not match - is a <see langword="false" />
/// return, the typed failure ADR-0118 names.
/// </para>
/// </remarks>
public sealed class BrainpoolEcdsa : IDisposable
{
    private readonly BrainpoolDomainParameters domain;
    private readonly uint[] privateKey;
    private readonly byte[] privateKeyOctets;
    private bool disposed;

    /// <summary>Copies the private key d for <paramref name="curve" />.</summary>
    /// <param name="curve">The curve the key belongs to.</param>
    /// <param name="privateKey">d, big-endian, exactly <see cref="BrainpoolEcdh.GetPrivateKeyLength" /> bytes, in [1, q - 1].</param>
    /// <exception cref="ArgumentException">The private key has the wrong length or is not in [1, q - 1].</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="curve" /> is not a <see cref="BrainpoolCurve" /> value.</exception>
    public BrainpoolEcdsa(BrainpoolCurve curve, ReadOnlySpan<byte> privateKey)
    {
        domain = BrainpoolDomainParameters.For(curve);
        BrainpoolEcdh.RequirePrivateKey(domain, privateKey);
        this.privateKey = new uint[domain.LimbCount];
        MontgomeryModulus.ToLimbs(privateKey, this.privateKey);
        privateKeyOctets = privateKey.ToArray();
    }

    /// <summary>The length in bytes of a signature on this key's curve, r || s.</summary>
    public int SignatureLength => 2 * domain.Length;

    /// <summary>Returns the length in bytes of a signature, r || s, on <paramref name="curve" />: 64, 96 or 128.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="curve" /> is not a <see cref="BrainpoolCurve" /> value.</exception>
    public static int GetSignatureLength(BrainpoolCurve curve) => 2 * BrainpoolDomainParameters.For(curve).Length;

    /// <summary>
    /// Returns whether <paramref name="signature" /> is a valid ECDSA signature over
    /// <paramref name="hash" /> by <paramref name="publicKey" /> on <paramref name="curve" />
    /// (SEC 1 section 4.1.4). Anything wrong is <see langword="false" />, as the remarks list.
    /// </summary>
    /// <param name="curve">The curve.</param>
    /// <param name="publicKey">The uncompressed point 0x04 || x || y.</param>
    /// <param name="hash">The message's hash, any length; its leftmost bits, up to q's length, are used.</param>
    /// <param name="signature">r || s, each exactly q's length.</param>
    /// <returns><see langword="true" /> when the signature is valid.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="curve" /> is not a <see cref="BrainpoolCurve" /> value.</exception>
    public static bool VerifyHash(BrainpoolCurve curve, ReadOnlySpan<byte> publicKey, ReadOnlySpan<byte> hash, ReadOnlySpan<byte> signature)
    {
        BrainpoolDomainParameters domain = BrainpoolDomainParameters.For(curve);
        int n = domain.LimbCount;
        int length = domain.Length;
        uint[] memory = new uint[5 * n];
        Span<uint> r = memory.AsSpan(0, n);
        Span<uint> s = memory.AsSpan(n, n);
        Span<uint> point = memory.AsSpan(2 * n, 3 * n);
        return signature.Length == 2 * length
            && domain.TryReadScalar(signature[..length], r)
            && domain.TryReadScalar(signature[length..], s)
            && BrainpoolPoint.TryDecode(domain, publicKey, point)
            && Verify(domain, point, hash, r, s);
    }

    /// <summary>Writes this key's public key d * G, uncompressed, to <paramref name="destination" />.</summary>
    /// <param name="destination">Receives 0x04 || x || y, exactly <see cref="BrainpoolEcdh.GetPublicKeyLength" /> bytes.</param>
    /// <exception cref="ArgumentException"><paramref name="destination" /> has the wrong length.</exception>
    /// <exception cref="ObjectDisposedException">The key was disposed.</exception>
    public void ExportPublicKey(Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        RequireLength(destination, 1 + (2 * domain.Length), nameof(destination));
        BrainpoolEcdh.ComputePublicKey(domain, privateKeyOctets, destination);
    }

    /// <summary>
    /// Writes the signature r || s over <paramref name="hash" />, with the nonce k derived
    /// from d and the hash by RFC 6979 section 3.2, so the same key and hash always give the
    /// same signature.
    /// </summary>
    /// <param name="hash">The message's hash, exactly <paramref name="hashAlgorithm" />'s length.</param>
    /// <param name="hashAlgorithm">The hash the message was hashed with, and RFC 6979's HMAC runs on: SHA1, SHA224 (by its name), SHA256, SHA384 or SHA512.</param>
    /// <param name="destination">Receives r || s, exactly <see cref="SignatureLength" /> bytes.</param>
    /// <exception cref="ArgumentException">The hash algorithm is not one of those, or a length is wrong.</exception>
    /// <exception cref="ObjectDisposedException">The key was disposed.</exception>
    public void SignHash(ReadOnlySpan<byte> hash, HashAlgorithmName hashAlgorithm, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        RequireLength(hash, DeterministicDsaNonce.DigestLength(hashAlgorithm), nameof(hash));
        RequireLength(destination, SignatureLength, nameof(destination));
        int n = domain.LimbCount;
        int length = domain.Length;
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

    /// <summary>Zeroes the private key; any later use throws <see cref="ObjectDisposedException" />.</summary>
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
    /// SEC 1 section 4.1.4 steps 4 to 8: w = s^-1, u1 = z * w, u2 = r * w mod q,
    /// R = u1 * G + u2 * Q, which must not be the point at infinity, and x_R mod q must equal r.
    /// </summary>
    private static bool Verify(BrainpoolDomainParameters domain, ReadOnlySpan<uint> publicPoint, ReadOnlySpan<byte> hash, ReadOnlySpan<uint> r, ReadOnlySpan<uint> s)
    {
        int n = domain.LimbCount;
        int length = domain.Length;
        uint[] memory = new uint[(11 * n) + BrainpoolPoint.AddWorkLength(n)];
        Span<uint> w = memory.AsSpan(0, n);
        Span<uint> u1 = memory.AsSpan(n, n);
        Span<uint> u2 = memory.AsSpan(2 * n, n);
        Span<uint> first = memory.AsSpan(3 * n, 3 * n);
        Span<uint> second = memory.AsSpan(6 * n, 3 * n);
        Span<uint> x = memory.AsSpan(9 * n, n);
        Span<uint> y = memory.AsSpan(10 * n, n);
        Span<uint> work = memory.AsSpan(11 * n);
        domain.InvertOrder(s, w);
        domain.ReduceHash(hash, u1);
        domain.Order.MultiplyModulo(u1, u1, w);
        domain.Order.MultiplyModulo(u2, r, w);
        byte[] scalars = new byte[2 * length];
        MontgomeryModulus.FromLimbs(u1, scalars.AsSpan(0, length));
        MontgomeryModulus.FromLimbs(u2, scalars.AsSpan(length));
        BrainpoolPoint.MultiplyScalar(domain, scalars.AsSpan(0, length), domain.Generator, first);
        BrainpoolPoint.MultiplyScalar(domain, scalars.AsSpan(length), publicPoint, second);
        BrainpoolPoint.Add(domain, first, first, second, work);
        bool finite = BrainpoolPoint.ToAffine(domain, first, x, y);
        domain.Order.Reduce(x, w);
        return finite && w.SequenceEqual(r);
    }

    /// <summary>
    /// Makes the signature for the candidate nonce k: r = x(k * G) mod q and
    /// s = k^-1 * (z + r * d) mod q, and returns whether k is in [1, q - 1] and r and s are
    /// nonzero. Every step runs whatever the answer, which is combined without branching.
    /// </summary>
    private bool TrySign(ReadOnlySpan<byte> nonce, ReadOnlySpan<uint> z, Span<uint> r, Span<uint> s)
    {
        int n = domain.LimbCount;
        uint[] work = new uint[(9 * n) + 2];
        Span<uint> k = work.AsSpan(0, n);
        Span<uint> kInverse = work.AsSpan(n, n);
        Span<uint> sum = work.AsSpan(2 * n, n);
        Span<uint> point = work.AsSpan(3 * n, 3 * n);
        Span<uint> x = work.AsSpan(6 * n, n);
        Span<uint> y = work.AsSpan(7 * n, n);
        Span<uint> scratch = work.AsSpan(8 * n, n + 2);
        try
        {
            bool nonceInRange = domain.TryReadScalar(nonce, k);
            BrainpoolPoint.MultiplyScalar(domain, nonce, domain.Generator, point);
            BrainpoolPoint.ToAffine(domain, point, x, y);
            domain.Order.Reduce(x, r);
            domain.InvertOrder(k, kInverse);
            domain.Order.MultiplyModulo(sum, privateKey, r);
            domain.Order.Add(sum, sum, z, scratch);
            domain.Order.MultiplyModulo(s, kInverse, sum);
            return nonceInRange & !BrainpoolDomainParameters.IsZero(r) & !BrainpoolDomainParameters.IsZero(s);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(work.AsSpan()));
        }
    }
}
