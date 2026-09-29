using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// HMAC (RFC 2104) with <see cref="Ripemd160" /> as the hash, HMAC-RIPEMD-160 as RFC 2286
/// specifies it: a key of any length and a message give a 20-byte MAC. SSH's
/// <c>hmac-ripemd160</c> and <c>hmac-ripemd160@openssh.com</c> use it untruncated.
/// </summary>
/// <remarks>
/// MAC a whole message with <see cref="HashData" />, or key an instance once and feed it
/// message after message with <see cref="AppendData" /> and <see cref="GetHashAndReset" />.
/// Constant-time in the key: a key longer than 64 bytes is hashed first, a branch on its
/// length only, and the padded key is combined with the pads by exclusive-or. Compare MACs
/// with <see cref="Verify" />, which uses
/// <see cref="CryptographicOperations.FixedTimeEquals" />. The padded keys, both hash
/// states and the inner hash are zeroed after use and by <see cref="Dispose" />.
/// </remarks>
public sealed class HmacRipemd160 : IDisposable
{
    /// <summary>The length in bytes of a MAC.</summary>
    public const int HashSize = Ripemd160.HashSize;

    private const int BlockSize = LittleEndianMerkleDamgard<Ripemd160>.BlockSize;

    private readonly byte[] innerPad = new byte[BlockSize];

    private readonly byte[] outerPad = new byte[BlockSize];

    private readonly Ripemd160 inner = new();

    private readonly Ripemd160 outer = new();

    private bool disposed;

    /// <summary>
    /// Copies <paramref name="key" /> into the inner and outer pads, hashing it first when
    /// it is longer than a 64-byte block, and starts an empty message.
    /// </summary>
    public HmacRipemd160(ReadOnlySpan<byte> key)
    {
        if (key.Length > BlockSize)
        {
            Ripemd160.HashData(key, innerPad.AsSpan(0, HashSize));
        }
        else
        {
            key.CopyTo(innerPad);
        }

        for (int index = 0; index < BlockSize; index++)
        {
            outerPad[index] = (byte)(innerPad[index] ^ 0x5c);
            innerPad[index] ^= 0x36;
        }

        inner.AppendData(innerPad);
    }

    /// <summary>
    /// Writes the HMAC-RIPEMD-160 of <paramref name="source" /> under
    /// <paramref name="key" /> to <paramref name="destination" />.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="destination" /> is not <see cref="HashSize" /> bytes.</exception>
    public static void HashData(ReadOnlySpan<byte> key, ReadOnlySpan<byte> source, Span<byte> destination)
    {
        using HmacRipemd160 hmac = new(key);
        hmac.AppendData(source);
        hmac.GetHashAndReset(destination);
    }

    /// <summary>
    /// Returns whether <paramref name="mac" /> is the HMAC-RIPEMD-160 of
    /// <paramref name="source" /> under <paramref name="key" />, comparing in fixed time.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="mac" /> is not <see cref="HashSize" /> bytes.</exception>
    public static bool Verify(ReadOnlySpan<byte> key, ReadOnlySpan<byte> source, ReadOnlySpan<byte> mac)
    {
        if (mac.Length != HashSize)
        {
            throw new ArgumentException($"HMAC-RIPEMD-160 needs a {HashSize}-byte MAC; this is {mac.Length}.", nameof(mac));
        }

        Span<byte> expected = stackalloc byte[HashSize];
        try
        {
            HashData(key, source, expected);
            return CryptographicOperations.FixedTimeEquals(expected, mac);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expected);
        }
    }

    /// <summary>Adds <paramref name="data" /> to the message being authenticated.</summary>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void AppendData(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        inner.AppendData(data);
    }

    /// <summary>
    /// Writes the MAC of the data appended so far to <paramref name="destination" /> and
    /// starts a new, empty message under the same key.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="destination" /> is not <see cref="HashSize" /> bytes.</exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void GetHashAndReset(Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (destination.Length != HashSize)
        {
            throw new ArgumentException($"HMAC-RIPEMD-160 needs a {HashSize}-byte destination; this is {destination.Length}.", nameof(destination));
        }

        Span<byte> innerHash = stackalloc byte[HashSize];
        try
        {
            inner.GetHashAndReset(innerHash);
            outer.AppendData(outerPad);
            outer.AppendData(innerHash);
            outer.GetHashAndReset(destination);
            inner.AppendData(innerPad);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(innerHash);
        }
    }

    /// <summary>Zeroes the padded keys and both hash states.</summary>
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(innerPad);
        CryptographicOperations.ZeroMemory(outerPad);
        inner.Dispose();
        outer.Dispose();
        disposed = true;
    }
}
