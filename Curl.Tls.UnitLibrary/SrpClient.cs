using System.Numerics;
using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Tls;

/// <summary>
/// The client's side of SRP-6a as RFC 5054 section 2.6 runs it for TLS: SHA-1 for every
/// hash, <c>PAD()</c> to the length of N where the RFC writes it, and each value as the
/// big-endian integer the RFC names. Pure functions: the private value a is passed in.
/// </summary>
public static class SrpClient
{
    /// <summary>
    /// The length in bytes of the private value a the handshake draws: 384 bits, what
    /// OpenSSL draws (<c>SSL_MAX_MASTER_KEY_LENGTH</c>), above RFC 5054's 256-bit minimum.
    /// </summary>
    public const int PrivateValueLength = 48;

    /// <summary>Returns the multiplier k = SHA1(N | PAD(g)).</summary>
    /// <param name="group">The group.</param>
    /// <returns>The 20-byte k.</returns>
    public static byte[] ComputeMultiplier(SrpGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return SHA1.HashData([.. group.Prime, .. Pad(group.Generator, group.PrimeLength)]);
    }

    /// <summary>Returns the private key x = SHA1(s | SHA1(I | ":" | P)).</summary>
    /// <param name="salt">The salt s the server sent.</param>
    /// <param name="identity">The user name I.</param>
    /// <param name="password">The password P.</param>
    /// <returns>The 20-byte x.</returns>
    public static byte[] ComputePrivateKey(ReadOnlySpan<byte> salt, ReadOnlySpan<byte> identity, ReadOnlySpan<byte> password) =>
        SHA1.HashData([.. salt, .. SHA1.HashData([.. identity, (byte)':', .. password])]);

    /// <summary>Returns the password verifier v = g^x % N, as a server stores it.</summary>
    /// <param name="group">The group.</param>
    /// <param name="privateKey">The private key x.</param>
    /// <returns>v, big-endian, without leading zero bytes.</returns>
    public static byte[] ComputeVerifier(SrpGroup group, ReadOnlySpan<byte> privateKey)
    {
        ArgumentNullException.ThrowIfNull(group);
        return RaiseGenerator(group, privateKey);
    }

    /// <summary>Returns the client's public value A = g^a % N, which its ClientKeyExchange carries.</summary>
    /// <param name="group">The group.</param>
    /// <param name="privateValue">The private value a.</param>
    /// <returns>A, big-endian, without leading zero bytes.</returns>
    public static byte[] ComputePublicValue(SrpGroup group, ReadOnlySpan<byte> privateValue)
    {
        ArgumentNullException.ThrowIfNull(group);
        return RaiseGenerator(group, privateValue);
    }

    /// <summary>Returns the scrambling parameter u = SHA1(PAD(A) | PAD(B)).</summary>
    /// <param name="group">The group.</param>
    /// <param name="clientPublicValue">The client's public value A.</param>
    /// <param name="serverPublicValue">The server's public value B.</param>
    /// <returns>The 20-byte u.</returns>
    public static byte[] ComputeScrambler(SrpGroup group, ReadOnlySpan<byte> clientPublicValue, ReadOnlySpan<byte> serverPublicValue)
    {
        ArgumentNullException.ThrowIfNull(group);
        return SHA1.HashData([.. Pad(clientPublicValue, group.PrimeLength), .. Pad(serverPublicValue, group.PrimeLength)]);
    }

    /// <summary>
    /// Returns the premaster secret S = (B - (k * g^x)) ^ (a + (u * x)) % N, big-endian
    /// without leading zero bytes, as OpenSSL writes it (<c>BN_bn2bin</c>). The caller has
    /// already refused a B that is 0 modulo N.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="privateKey">The private key x.</param>
    /// <param name="privateValue">The private value a.</param>
    /// <param name="serverPublicValue">The server's public value B.</param>
    /// <returns>The premaster secret.</returns>
    public static byte[] ComputePremasterSecret(SrpGroup group, ReadOnlySpan<byte> privateKey, ReadOnlySpan<byte> privateValue, ReadOnlySpan<byte> serverPublicValue)
    {
        ArgumentNullException.ThrowIfNull(group);
        BigInteger n = group.PrimeValue;
        BigInteger x = ToInteger(privateKey);
        BigInteger k = ToInteger(ComputeMultiplier(group));
        BigInteger u = ToInteger(ComputeScrambler(group, ComputePublicValue(group, privateValue), serverPublicValue));
        BigInteger b = ToInteger(serverPublicValue);
        BigInteger kv = k * ToInteger(RaiseGenerator(group, privateKey)) % n;
        BigInteger basis = ((b - kv) % n + n) % n;
        BigInteger exponent = ToInteger(privateValue) + (u * x);
        return Raise(group, basis, ToFixedLength(exponent, Math.Max(privateValue.Length, privateKey.Length + u.GetByteCount(isUnsigned: true)) + 1));
    }

    internal static BigInteger ToInteger(ReadOnlySpan<byte> value) => new(value, isUnsigned: true, isBigEndian: true);

    private static byte[] ToBytes(BigInteger value) => value.ToByteArray(isUnsigned: true, isBigEndian: true);

    /// <summary>
    /// Returns g^<paramref name="exponent" /> % N through Curl.Cryptography's fixed-window
    /// Montgomery exponentiation, whose running time depends only on the exponent's length.
    /// </summary>
    private static byte[] RaiseGenerator(SrpGroup group, ReadOnlySpan<byte> exponent)
    {
        using FiniteFieldDiffieHellman power = new(group.ExponentiationGroup, exponent);
        byte[] result = new byte[group.PrimeLength];
        power.ComputePublicValue(result);
        return ToBytes(ToInteger(result));
    }

    /// <summary>
    /// Returns <paramref name="basis" />^<paramref name="exponent" /> % N for a basis below N,
    /// through the same constant-time exponentiation. That exponentiation refuses the three
    /// bases a peer's public value may not be - 0, 1 and N - 1 - whose powers need no
    /// exponentiation: 0 and 1 are their own powers, and N - 1 raised to e is 1 when e is even
    /// and N - 1 when it is odd. Only a server that already holds the verifier can steer B to
    /// one of them.
    /// </summary>
    private static byte[] Raise(SrpGroup group, BigInteger basis, byte[] exponent)
    {
        using FiniteFieldDiffieHellman power = new(group.ExponentiationGroup, exponent);
        byte[] result = new byte[group.PrimeLength];
        if (power.TryComputeSharedSecret(ToBytes(basis), result))
        {
            return ToBytes(ToInteger(result));
        }

        bool isNegativeOne = basis == group.PrimeValue - 1;
        return ToBytes(isNegativeOne && (exponent[^1] & 1) == 0 ? BigInteger.One : basis);
    }

    /// <summary>Writes <paramref name="value" /> big-endian into exactly <paramref name="length" /> bytes.</summary>
    private static byte[] ToFixedLength(BigInteger value, int length)
    {
        byte[] bytes = new byte[length];
        value.TryWriteBytes(bytes.AsSpan(length - value.GetByteCount(isUnsigned: true)), out _, isUnsigned: true, isBigEndian: true);
        return bytes;
    }

    private static byte[] Pad(ReadOnlySpan<byte> value, int length)
    {
        ReadOnlySpan<byte> significant = value.TrimStart((byte)0);
        byte[] padded = new byte[Math.Max(length, significant.Length)];
        significant.CopyTo(padded.AsSpan(padded.Length - significant.Length));
        return padded;
    }
}
