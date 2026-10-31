using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Curl.Extension.OAuth2.Wire;

/// <summary>
/// Proof Key for Code Exchange (RFC 7636): the <c>code_verifier</c> made from random octets (§4.1) and
/// its <c>S256</c> <c>code_challenge</c> (§4.2).
/// </summary>
public static class OAuth2Pkce
{
    /// <summary>The <c>code_challenge_method</c> of a challenge made by <see cref="CreateS256CodeChallenge"/>.</summary>
    public const string S256Method = "S256";

    /// <summary>The fewest random octets, which make a verifier of 43 characters, the least §4.1 allows.</summary>
    public const int MinimumRandomOctets = 32;

    /// <summary>The most random octets, which make a verifier of 128 characters, the most §4.1 allows.</summary>
    public const int MaximumRandomOctets = 96;

    /// <summary>
    /// Makes a <c>code_verifier</c> by base64url-encoding the random octets without padding, as §4.1
    /// recommends.
    /// </summary>
    /// <param name="randomOctets">From 32 to 96 octets from a cryptographic random number generator.</param>
    /// <returns>The code verifier.</returns>
    /// <exception cref="ArgumentOutOfRangeException">There are fewer than 32 or more than 96 octets.</exception>
    public static string CreateCodeVerifier(ReadOnlySpan<byte> randomOctets)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(randomOctets.Length, MinimumRandomOctets, nameof(randomOctets));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(randomOctets.Length, MaximumRandomOctets, nameof(randomOctets));
        return Base64Url.EncodeToString(randomOctets);
    }

    /// <summary>
    /// Makes the <c>S256</c> <c>code_challenge</c>: <c>BASE64URL-ENCODE(SHA256(ASCII(code_verifier)))</c> (§4.2).
    /// </summary>
    /// <param name="codeVerifier">The code verifier.</param>
    /// <returns>The code challenge.</returns>
    public static string CreateS256CodeChallenge(string codeVerifier) =>
        Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
}
