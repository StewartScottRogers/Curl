using Curl.Cryptography;

namespace Curl.Tls;

/// <summary>
/// The hand-built <see cref="AeadAesCcm" /> for the RFC 6655 and RFC 7251 suites: 16-byte
/// tags for <c>_CCM</c>, 8-byte tags for <c>_CCM_8</c>. The tag length is the length of the
/// <c>tag</c> span the record cipher passes.
/// </summary>
internal sealed class AesCcmTlsAead(byte[] key) : ITlsAead
{
    private readonly AeadAesCcm aesCcm = new(key);

    public void Encrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> plaintext, Span<byte> ciphertext, Span<byte> tag, ReadOnlySpan<byte> associatedData) =>
        aesCcm.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

    public bool TryDecrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag, Span<byte> plaintext, ReadOnlySpan<byte> associatedData) =>
        aesCcm.TryDecrypt(nonce, ciphertext, tag, plaintext, associatedData);

    public void Dispose() => aesCcm.Dispose();
}
