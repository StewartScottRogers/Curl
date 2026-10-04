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
/// Constant-time in the key: every read and swap at the key-dependent index reads and
/// rewrites all 256 entries of the permutation in order and picks the one it needs by
/// mask, so no memory address depends on the key (ADR-0399). It exists because curl's SSH
/// backends and Kerberos <c>rc4-hmac</c> use it. The permutation and its two indices live
/// in the instance and are zeroed by <see cref="Dispose" />.
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
            SwapWithSecretIndex(permutation, index, position);
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
        byte atFirst = permutation[first];
        second += atFirst;
        byte atSecond = SwapWithSecretIndex(permutation, first, second);
        return ReadAtSecretIndex(permutation, (byte)(atFirst + atSecond));
    }

    /// <summary>
    /// Swaps the entries of <paramref name="table" /> at the public
    /// <paramref name="publicIndex" /> and the secret <paramref name="secretIndex" /> without
    /// a secret-dependent address (ADR-0399): every entry is read and rewritten, in order,
    /// and only the one at <paramref name="secretIndex" /> changes, chosen by a mask
    /// computed without a branch. Returns the entry that was at
    /// <paramref name="secretIndex" />.
    /// </summary>
    internal static byte SwapWithSecretIndex(Span<byte> table, int publicIndex, byte secretIndex)
    {
        uint atPublic = table[publicIndex];
        uint atSecret = 0;
        for (int position = 0; position < table.Length; position++)
        {
            uint mask = ConstantTime.EqualMask((uint)position, secretIndex);
            atSecret |= mask & table[position];
            table[position] = (byte)ConstantTime.Select(mask, atPublic, table[position]);
        }

        table[publicIndex] = (byte)atSecret;
        return (byte)atSecret;
    }

    /// <summary>
    /// Entry <paramref name="secretIndex" /> of <paramref name="table" />, without a
    /// secret-dependent address (ADR-0399): every entry is read, in order, once, and kept
    /// only where its position equals <paramref name="secretIndex" />.
    /// </summary>
    internal static byte ReadAtSecretIndex(ReadOnlySpan<byte> table, byte secretIndex)
    {
        uint result = 0;
        for (int position = 0; position < table.Length; position++)
        {
            result |= ConstantTime.EqualMask((uint)position, secretIndex) & table[position];
        }

        return (byte)result;
    }
}
