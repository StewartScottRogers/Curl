using Curl.Cryptography;

namespace Curl.Kerberos;

/// <summary>
/// AES in CBC mode with ciphertext stealing from the initial, all-zero cipher state: the
/// <c>E</c> and <c>D</c> of RFC 3962 and RFC 8009, for inputs of at least one block (the
/// confounder guarantees it).
/// </summary>
internal static class KerberosAesCts
{
    private static readonly byte[] InitialCipherState = new byte[AesCbcCts.BlockSize];

    /// <summary>Encrypts <paramref name="source" /> under <paramref name="key" /> into <paramref name="destination" />, as long as it.</summary>
    public static void Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> source, Span<byte> destination)
    {
        using AesCbcCts cipher = new(key);
        cipher.Encrypt(InitialCipherState, source, destination);
    }

    /// <summary>Decrypts <paramref name="source" /> under <paramref name="key" /> into <paramref name="destination" />, as long as it.</summary>
    public static void Decrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> source, Span<byte> destination)
    {
        using AesCbcCts cipher = new(key);
        cipher.Decrypt(InitialCipherState, source, destination);
    }
}
