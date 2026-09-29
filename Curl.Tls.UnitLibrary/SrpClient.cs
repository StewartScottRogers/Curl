using System.Numerics;
using System.Security.Cryptography;

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
        return ToBytes(BigInteger.ModPow(group.GeneratorValue, ToInteger(privateKey), group.PrimeValue));
    }

    /// <summary>Returns the client's public value A = g^a % N, which its ClientKeyExchange carries.</summary>
    /// <param name="group">The group.</param>
    /// <param name="privateValue">The private value a.</param>
    /// <returns>A, big-endian, without leading zero bytes.</returns>
    public static byte[] ComputePublicValue(SrpGroup group, ReadOnlySpan<byte> privateValue)
    {
        ArgumentNullException.ThrowIfNull(group);
        return ToBytes(BigInteger.ModPow(group.GeneratorValue, ToInteger(privateValue), group.PrimeValue));
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
        BigInteger kv = k * BigInteger.ModPow(group.GeneratorValue, x, n) % n;
        BigInteger basis = ((b - kv) % n + n) % n;
        return ToBytes(BigInteger.ModPow(basis, ToInteger(privateValue) + (u * x), n));
    }

    internal static BigInteger ToInteger(ReadOnlySpan<byte> value) => new(value, isUnsigned: true, isBigEndian: true);

    private static byte[] ToBytes(BigInteger value) => value.ToByteArray(isUnsigned: true, isBigEndian: true);

    private static byte[] Pad(ReadOnlySpan<byte> value, int length)
    {
        ReadOnlySpan<byte> significant = value.TrimStart((byte)0);
        byte[] padded = new byte[Math.Max(length, significant.Length)];
        significant.CopyTo(padded.AsSpan(padded.Length - significant.Length));
        return padded;
    }
}
