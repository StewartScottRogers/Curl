using System.Security.Cryptography;

namespace Curl.Authentication;

/// <summary>
/// Creates the client nonce (<c>cnonce</c>) of a Digest answer as curl 8.21.0 does.
/// </summary>
public static class DigestClientNonce
{
    /// <summary>
    /// Creates a client nonce: 12 cryptographically random bytes in base64, 16 characters.
    /// </summary>
    /// <returns>The client nonce.</returns>
    public static string CreateRandom() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(12));

    /// <summary>
    /// Creates a SASL DIGEST-MD5 client nonce: 32 cryptographically random lower-case
    /// hexadecimal digits, as curl sends in both its own code and through SSPI (ADR-0139).
    /// </summary>
    /// <returns>The client nonce.</returns>
    public static string CreateRandomHex() => RandomNumberGenerator.GetHexString(32, lowercase: true);
}
