using System.Security.Cryptography;
using Curl.Cryptography;
using Curl.Tls;

namespace Curl.Extended.Protocol.Coap.Dtls.Record;

/// <summary>
/// The AEAD that protects DTLS 1.2 records under one of the suites the client offers: AES-128-GCM
/// with a 16-byte tag (RFC 5288) or AES-128-CCM with an 8-byte tag (RFC 6655, RFC 7251,
/// <c>_CCM_8</c>), both with a 12-byte nonce.
/// </summary>
internal sealed class DtlsRecordAead : IDisposable
{
    private readonly AesGcm? aesGcm;

    private readonly AeadAesCcm? aesCcm;

    /// <summary>Creates the AEAD of <paramref name="bulkCipher" /> keyed with <paramref name="key" />.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bulkCipher" /> is neither AES-128-GCM nor AES-128-CCM with an 8-byte tag.</exception>
    public DtlsRecordAead(Tls12BulkCipher bulkCipher, ReadOnlySpan<byte> key)
    {
        TagLength = bulkCipher switch
        {
            Tls12BulkCipher.Aes128Gcm => GcmTagLength,
            Tls12BulkCipher.Aes128Ccm8 => Ccm8TagLength,
            _ => throw new ArgumentOutOfRangeException(nameof(bulkCipher), bulkCipher, "DTLS record protection supports AES-128-GCM and AES-128-CCM_8."),
        };
        if (bulkCipher == Tls12BulkCipher.Aes128Gcm)
        {
            aesGcm = new AesGcm(key, GcmTagLength);
        }
        else
        {
            aesCcm = new AeadAesCcm(key);
        }
    }

    /// <summary>Gets the length in bytes of the authentication tag that follows the ciphertext.</summary>
    public int TagLength { get; }

    private const int GcmTagLength = 16;

    private const int Ccm8TagLength = 8;

    /// <summary>Encrypts <paramref name="plaintext" /> and writes the tag over <paramref name="associatedData" /> and the ciphertext.</summary>
    public void Encrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> plaintext, Span<byte> ciphertext, Span<byte> tag, ReadOnlySpan<byte> associatedData)
    {
        if (aesGcm is not null)
        {
            aesGcm.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
        }
        else
        {
            aesCcm!.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
        }
    }

    /// <summary>Decrypts <paramref name="ciphertext" /> when <paramref name="tag" /> matches; returns <c>false</c> otherwise.</summary>
    public bool TryDecrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag, Span<byte> plaintext, ReadOnlySpan<byte> associatedData)
    {
        if (aesCcm is not null)
        {
            return aesCcm.TryDecrypt(nonce, ciphertext, tag, plaintext, associatedData);
        }

        try
        {
            aesGcm!.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
            return true;
        }
        catch (AuthenticationTagMismatchException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        aesGcm?.Dispose();
        aesCcm?.Dispose();
    }
}
