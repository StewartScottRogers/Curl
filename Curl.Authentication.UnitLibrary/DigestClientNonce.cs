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
}
