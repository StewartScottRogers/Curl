using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// The AES-256 CTR_DRBG behind NIST's post-quantum known-answer files: <c>randombytes_init</c>
/// and <c>randombytes</c> from the submission package's <c>nist/rng.c</c> (Bassham, 2017),
/// on the BCL's AES-ECB. Every call to <see cref="NextBytes" /> ends with a state update, so
/// callers must ask for bytes in the same pieces the reference implementation does.
/// </summary>
internal sealed class NistKnownAnswerRandom : IDisposable
{
    private readonly byte[] key = new byte[32];
    private readonly byte[] counter = new byte[16];
    private readonly Aes aes = Aes.Create();

    /// <summary><c>randombytes_init(seed, NULL, 256)</c> for a 48-byte seed.</summary>
    public NistKnownAnswerRandom(ReadOnlySpan<byte> seed)
    {
        Update(seed);
    }

    /// <summary><c>randombytes(destination, destination.Length)</c>.</summary>
    public void NextBytes(Span<byte> destination)
    {
        Span<byte> block = stackalloc byte[16];
        for (int offset = 0; offset < destination.Length; offset += 16)
        {
            EncryptNextCounter(block);
            block[..Math.Min(16, destination.Length - offset)].CopyTo(destination[offset..]);
        }

        Update(default);
    }

    /// <summary>Concatenates <paramref name="calls" /> separate calls of <paramref name="size" /> bytes each.</summary>
    public byte[] NextBytesInCalls(int calls, int size)
    {
        byte[] result = new byte[calls * size];
        for (int call = 0; call < calls; call++)
        {
            NextBytes(result.AsSpan(call * size, size));
        }

        return result;
    }

    public void Dispose() => aes.Dispose();

    /// <summary><c>AES256_CTR_DRBG_Update</c>: three counter blocks, XORed with the provided data if any.</summary>
    private void Update(ReadOnlySpan<byte> providedData)
    {
        Span<byte> temporary = stackalloc byte[48];
        for (int block = 0; block < 3; block++)
        {
            EncryptNextCounter(temporary.Slice(16 * block, 16));
        }

        for (int index = 0; index < providedData.Length; index++)
        {
            temporary[index] ^= providedData[index];
        }

        temporary[..32].CopyTo(key);
        temporary[32..].CopyTo(counter);
    }

    private void EncryptNextCounter(Span<byte> block)
    {
        for (int index = 15; index >= 0; index--)
        {
            counter[index]++;
            if (counter[index] != 0)
            {
                break;
            }
        }

        aes.Key = key;
        aes.EncryptEcb(counter, block, PaddingMode.None);
    }
}
