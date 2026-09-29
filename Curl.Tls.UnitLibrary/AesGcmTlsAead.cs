using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>The BCL's <see cref="AesGcm" /> with 16-byte tags, for the RFC 5288 suites and the TLS 1.3 AES-GCM suites.</summary>
internal sealed class AesGcmTlsAead(byte[] key) : ITlsAead
{
    private readonly AesGcm aesGcm = new(key, 16);

    public void Encrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> plaintext, Span<byte> ciphertext, Span<byte> tag, ReadOnlySpan<byte> associatedData) =>
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

    public bool TryDecrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag, Span<byte> plaintext, ReadOnlySpan<byte> associatedData)
    {
        try
        {
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
            return true;
        }
        catch (AuthenticationTagMismatchException)
        {
            return false;
        }
    }

    public void Dispose() => aesGcm.Dispose();
}
