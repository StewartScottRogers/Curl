using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// The RC4 stream cipher (the "Arcfour" of RFC 4345 and RFC 4757), keys of 1 to 256
/// bytes. <see cref="ApplyKeyStream" /> encrypts and decrypts alike and keeps its place in
/// the keystream between calls; <see cref="DiscardKeyStream" /> skips keystream bytes, as
/// SSH's <c>arcfour128</c> and <c>arcfour256</c> do with the first
/// <see cref="Rfc4345DiscardLength" /> (RFC 4345 section 4).
/// </summary>
/// <remarks>
/// Not constant-time: RC4 indexes its key-dependent permutation with key-dependent
/// positions by design (ADR-0118); it exists because curl's SSH backends and Kerberos
/// <c>rc4-hmac</c> use it. The permutation and its two indices live in the instance and
/// are zeroed by <see cref="Dispose" />.
/// </remarks>
public sealed class Rc4 : IDisposable
{
    /// <summary>The shortest key in bytes.</summary>
    public const int MinimumKeySize = 1;

    /// <summary>The longest key in bytes: the permutation's 256 entries.</summary>
    public const int MaximumKeySize = 256;

    /// <summary>
    /// The keystream bytes SSH's <c>arcfour128</c> and <c>arcfour256</c> discard before the
    /// first byte they use (RFC 4345 section 4).
    /// </summary>
    public const int Rfc4345DiscardLength = 1536;

    private readonly byte[] permutation = new byte[256];

    private byte first;

    private byte second;

    private bool disposed;

    /// <summary>Runs RC4's key-scheduling algorithm on <paramref name="key" />.</summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="key" /> is shorter than <see cref="MinimumKeySize" /> or longer than
    /// <see cref="MaximumKeySize" /> bytes.
    /// </exception>
    public Rc4(ReadOnlySpan<byte> key)
    {
        if (key.Length < MinimumKeySize || key.Length > MaximumKeySize)
        {
            throw new ArgumentException($"RC4 needs a key of {MinimumKeySize} to {MaximumKeySize} bytes; this is {key.Length}.", nameof(key));
        }

        for (int index = 0; index < permutation.Length; index++)
        {
            permutation[index] = (byte)index;
        }

        byte position = 0;
        for (int index = 0; index < permutation.Length; index++)
        {
            position = (byte)(position + permutation[index] + key[index % key.Length]);
            (permutation[index], permutation[position]) = (permutation[position], permutation[index]);
        }
    }

    /// <summary>
    /// Exclusive-ors <paramref name="source" /> with the next keystream bytes and writes the
    /// result to <paramref name="destination" />, which may be <paramref name="source" />
    /// itself: encryption and decryption alike.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="destination" /> is not as long as <paramref name="source" />.</exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void ApplyKeyStream(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (destination.Length != source.Length)
        {
            throw new ArgumentException($"RC4 needs a destination of {source.Length} bytes; this is {destination.Length}.", nameof(destination));
        }

        for (int index = 0; index < source.Length; index++)
        {
            destination[index] = (byte)(source[index] ^ NextKeyStreamByte());
        }
    }

    /// <summary>Generates and throws away the next <paramref name="length" /> keystream bytes.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length" /> is negative.</exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void DiscardKeyStream(int length)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        for (int index = 0; index < length; index++)
        {
            NextKeyStreamByte();
        }
    }

    /// <summary>Zeroes the permutation and indices; any later call throws <see cref="ObjectDisposedException" />.</summary>
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(permutation);
        first = 0;
        second = 0;
        disposed = true;
    }

    private byte NextKeyStreamByte()
    {
        first++;
        second += permutation[first];
        (permutation[first], permutation[second]) = (permutation[second], permutation[first]);
        return permutation[(byte)(permutation[first] + permutation[second])];
    }
}
