using System.Security.Cryptography;
using System.Text;

namespace Curl.HttpMessageSignatures;

/// <summary>
/// Signs an RFC 9421 signature base with <c>ed25519</c> (the hand-built
/// <see cref="Cryptography.Ed25519" />) or <c>hmac-sha256</c> (the BCL's <see cref="HMACSHA256" />),
/// over the base's UTF-8 bytes.
/// </summary>
public static class HttpMessageSigner
{
    /// <summary>Returns the signature of <paramref name="signatureBase" /> under <paramref name="key" />.</summary>
    /// <param name="algorithm">The algorithm.</param>
    /// <param name="key">The 32-byte Ed25519 private seed, or the HMAC shared secret.</param>
    /// <param name="signatureBase">The text <see cref="HttpSignatureBase.Build" /> wrote.</param>
    /// <returns>64 bytes for Ed25519, 32 for HMAC-SHA256.</returns>
    /// <exception cref="ArgumentException">An Ed25519 key is not 32 bytes.</exception>
    public static byte[] Sign(HttpSignatureAlgorithm algorithm, ReadOnlySpan<byte> key, string signatureBase)
    {
        var message = Encoding.UTF8.GetBytes(signatureBase);
        if (algorithm == HttpSignatureAlgorithm.HmacSha256)
        {
            return HMACSHA256.HashData(key, message);
        }

        var signature = new byte[Cryptography.Ed25519.SignatureSize];
        Cryptography.Ed25519.Sign(key, message, signature);
        return signature;
    }
}
