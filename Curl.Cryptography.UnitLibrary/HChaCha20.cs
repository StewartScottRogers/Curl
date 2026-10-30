using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// HChaCha20 (draft-irtf-cfrg-xchacha-03 section 2.2): ChaCha20's 20 rounds on the
/// constants, a key and a 16-byte nonce, returning the first and last state rows - without
/// the final addition - as a 32-byte subkey. XChaCha20 derives its key with it.
/// </summary>
/// <remarks>
/// Constant-time: the same additions, rotations and exclusive-ors as <see cref="ChaCha20" />
/// on fixed state words. The state is zeroed before returning.
/// </remarks>
public static class HChaCha20
{
    /// <summary>The length in bytes of a key.</summary>
    public const int KeySize = ChaCha20.KeySize;

    /// <summary>The length in bytes of a nonce.</summary>
    public const int NonceSize = 16;

    /// <summary>The length in bytes of the subkey.</summary>
    public const int SubkeySize = 32;

    /// <summary>
    /// Derives the subkey for <paramref name="key" /> and <paramref name="nonce" /> into
    /// <paramref name="subkey" />.
    /// </summary>
    /// <exception cref="ArgumentException">A span has the wrong length.</exception>
    public static void DeriveSubkey(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, Span<byte> subkey)
    {
        RequireLength(key.Length, KeySize, nameof(key));
        RequireLength(nonce.Length, NonceSize, nameof(nonce));
        RequireLength(subkey.Length, SubkeySize, nameof(subkey));
        Span<uint> state = stackalloc uint[16];
        try
        {
            state[0] = 0x61707865;
            state[1] = 0x3320646e;
            state[2] = 0x79622d32;
            state[3] = 0x6b206574;
            for (int word = 0; word < 8; word++)
            {
                state[4 + word] = BinaryPrimitives.ReadUInt32LittleEndian(key[(word * sizeof(uint))..]);
            }

            for (int word = 0; word < 4; word++)
            {
                state[12 + word] = BinaryPrimitives.ReadUInt32LittleEndian(nonce[(word * sizeof(uint))..]);
            }

            RunRounds(state);
            for (int word = 0; word < 4; word++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(subkey[(word * sizeof(uint))..], state[word]);
                BinaryPrimitives.WriteUInt32LittleEndian(subkey[((word + 4) * sizeof(uint))..], state[12 + word]);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(state));
        }
    }

    private static void RequireLength(int length, int expected, string parameterName)
    {
        if (length != expected)
        {
            throw new ArgumentException($"HChaCha20 needs {expected} bytes here; this is {length}.", parameterName);
        }
    }

    /// <summary>Ten column rounds interleaved with ten diagonal rounds (RFC 8439 section 2.3).</summary>
    private static void RunRounds(Span<uint> state)
    {
        for (int doubleRound = 0; doubleRound < 10; doubleRound++)
        {
            ChaCha20.QuarterRound(state, 0, 4, 8, 12);
            ChaCha20.QuarterRound(state, 1, 5, 9, 13);
            ChaCha20.QuarterRound(state, 2, 6, 10, 14);
            ChaCha20.QuarterRound(state, 3, 7, 11, 15);
            ChaCha20.QuarterRound(state, 0, 5, 10, 15);
            ChaCha20.QuarterRound(state, 1, 6, 11, 12);
            ChaCha20.QuarterRound(state, 2, 7, 8, 13);
            ChaCha20.QuarterRound(state, 3, 4, 9, 14);
        }
    }
}
