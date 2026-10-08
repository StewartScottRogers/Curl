using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// One party's finite-field Diffie-Hellman key pair in a <see cref="FiniteFieldDiffieHellmanGroup" />:
/// the public value g^x mod p, and the shared secret y^x mod p from a peer's public value
/// y (RFC 2631 section 2.1.1, as SSH's RFC 4253 section 8 and TLS's RFC 7919 use it).
/// Public values and shared secrets are big-endian and exactly
/// <see cref="FiniteFieldDiffieHellmanGroup.PrimeLength" /> bytes, leading zeros kept, as
/// RFC 7919 section 5.1 sends them; SSH's <c>mpint</c> and TLS 1.2's stripped premaster
/// secret are the caller's encoding to apply.
/// </summary>
/// <remarks>
/// Constant-time in the private exponent: the exponentiation is a fixed 4-bit window
/// over Montgomery multiplication and squaring on fixed-width 64-bit limbs, every window
/// squares four times, reads all 16 table entries and keeps one by mask, and multiplies
/// once (by base^0 when the window is zero), so the sequence of operations is the same
/// whatever the exponent's bits; carries are flag values, never branches, and the final
/// reduction subtracts by mask - no branch, loop bound, array index or address depends on
/// the exponent. Only its length,
/// which is public, shapes the running time. <see cref="System.Numerics.BigInteger" />
/// touches public values only. The private exponent is zeroed on <see cref="Dispose" />,
/// and every intermediate before each method returns.
/// </remarks>
public sealed class FiniteFieldDiffieHellman : IDisposable
{
    /// <summary>
    /// The length in bytes of the private exponent <see cref="Generate" /> draws: 512 bits,
    /// at least twice the symmetric strength of every named group (RFC 7919 appendix A asks
    /// for 400 bits in <c>ffdhe8192</c>) and OpenSSH's twice-the-hash choice for SHA-512.
    /// </summary>
    public const int GeneratedExponentLength = 64;

    private readonly byte[] privateExponent;
    private bool disposed;

    /// <summary>
    /// Creates the key pair with the given private exponent x, big-endian, which is copied.
    /// This is the overload that reproduces a known answer; <see cref="Generate" /> draws x.
    /// </summary>
    /// <param name="group">The group to compute in.</param>
    /// <param name="privateExponent">
    /// x, from 1 to <see cref="FiniteFieldDiffieHellmanGroup.PrimeLength" /> bytes. Its value
    /// is not checked, because checking it would branch on a secret: the caller keeps it in
    /// 1 &lt; x &lt; p - 1.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="privateExponent" /> is empty or longer than p.</exception>
    public FiniteFieldDiffieHellman(FiniteFieldDiffieHellmanGroup group, ReadOnlySpan<byte> privateExponent)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (privateExponent.IsEmpty || privateExponent.Length > group.PrimeLength)
        {
            throw new ArgumentException(
                $"A private exponent is 1 to {group.PrimeLength} bytes in this group; this one is {privateExponent.Length}.",
                nameof(privateExponent));
        }

        Group = group;
        this.privateExponent = privateExponent.ToArray();
    }

    /// <summary>The group this key pair computes in.</summary>
    public FiniteFieldDiffieHellmanGroup Group { get; }

    /// <summary>The private exponent x as held, so tests can see <see cref="Dispose" /> zero it.</summary>
    internal ReadOnlySpan<byte> PrivateExponent => privateExponent;

    /// <summary>
    /// Creates a key pair with a private exponent of <see cref="GeneratedExponentLength" />
    /// bytes, or one byte less than p when p is shorter, from
    /// <see cref="RandomNumberGenerator" />, top bit set so 1 &lt; x &lt; p - 1.
    /// </summary>
    public static FiniteFieldDiffieHellman Generate(FiniteFieldDiffieHellmanGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        Span<byte> exponent = stackalloc byte[Math.Min(GeneratedExponentLength, group.PrimeLength - 1)];
        try
        {
            RandomNumberGenerator.Fill(exponent);
            exponent[0] |= 0x80;
            return new FiniteFieldDiffieHellman(group, exponent);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(exponent);
        }
    }

    /// <summary>Writes the public value g^x mod p to <paramref name="publicValue" />.</summary>
    /// <exception cref="ArgumentException"><paramref name="publicValue" /> is not <see cref="FiniteFieldDiffieHellmanGroup.PrimeLength" /> bytes.</exception>
    /// <exception cref="ObjectDisposedException">The key pair was disposed.</exception>
    public void ComputePublicValue(Span<byte> publicValue)
    {
        RequirePrimeLength(publicValue.Length, nameof(publicValue));
        ObjectDisposedException.ThrowIf(disposed, this);
        RaiseToPrivateExponent(Group.Generator, publicValue);
    }

    /// <summary>
    /// Computes the shared secret y^x mod p from the peer's public value y into
    /// <paramref name="sharedSecret" />.
    /// </summary>
    /// <param name="peerPublicValue">y, big-endian, of any length (leading zeros are ignored).</param>
    /// <param name="sharedSecret">Receives the secret, <see cref="FiniteFieldDiffieHellmanGroup.PrimeLength" /> bytes.</param>
    /// <returns>
    /// <c>false</c>, with <paramref name="sharedSecret" /> zeroed, when y is not in
    /// 1 &lt; y &lt; p - 1 (0, 1, p - 1, p or larger), which RFC 7919 section 5.1 and
    /// RFC 4253 section 8 require the caller to reject; otherwise <c>true</c>.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="sharedSecret" /> is not <see cref="FiniteFieldDiffieHellmanGroup.PrimeLength" /> bytes.</exception>
    /// <exception cref="ObjectDisposedException">The key pair was disposed.</exception>
    public bool TryComputeSharedSecret(ReadOnlySpan<byte> peerPublicValue, Span<byte> sharedSecret)
    {
        RequirePrimeLength(sharedSecret.Length, nameof(sharedSecret));
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!Group.IsValidPublicValue(peerPublicValue))
        {
            CryptographicOperations.ZeroMemory(sharedSecret);
            return false;
        }

        RaiseToPrivateExponent(peerPublicValue.TrimStart((byte)0), sharedSecret);
        return true;
    }

    /// <summary>Zeroes the private exponent; any later call throws <see cref="ObjectDisposedException" />.</summary>
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(privateExponent);
        disposed = true;
    }

    private void RequirePrimeLength(int length, string parameterName)
    {
        if (length != Group.PrimeLength)
        {
            throw new ArgumentException(
                $"Values in this group are {Group.PrimeLength} bytes; this one is {length}.",
                parameterName);
        }
    }

    /// <summary>Writes <paramref name="baseValue" />^x mod p, big-endian, to <paramref name="destination" />; the base is below p.</summary>
    private void RaiseToPrivateExponent(ReadOnlySpan<byte> baseValue, Span<byte> destination)
    {
        MontgomeryModulus modulus = Group.Modulus;
        uint[] limbs = new uint[modulus.LimbCount];
        try
        {
            MontgomeryModulus.ToLimbs(baseValue, limbs);
            modulus.Exponentiate(limbs, privateExponent, limbs);
            MontgomeryModulus.FromLimbs(limbs, destination);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(limbs.AsSpan()));
        }
    }
}
