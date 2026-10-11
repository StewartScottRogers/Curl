using System.Security.Cryptography;

namespace Curl.Authentication;

/// <summary>
/// Creates the client nonce (<c>cnonce</c>) of a Digest answer as the platform's curl 8.21.0 build does:
/// 32 lower-case hexadecimal digits from the Schannel build's SSPI, 12 random bytes in base64 from curl's
/// own Digest code (ADR-0025, ADR-0139).
/// </summary>
public static class DigestClientNonce
{
    /// <summary>
    /// Picks the client nonce source of the build the Digest answer matches.
    /// </summary>
    /// <param name="matchesSspiBuild">
    /// <see langword="true" /> for the Schannel build's SSPI (<see cref="CreateRandomHex" />);
    /// <see langword="false" /> for curl's own Digest code (<see cref="CreateRandom" />).
    /// </param>
    /// <returns>The client nonce source.</returns>
    public static Func<string> ForBuild(bool matchesSspiBuild) => matchesSspiBuild ? CreateRandomHex : CreateRandom;

    /// <summary>
    /// Creates the client nonce curl's own Digest code sends: 12 cryptographically random bytes in
    /// base64, 16 characters.
    /// </summary>
    /// <returns>The client nonce.</returns>
    public static string CreateRandom() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(12));

    /// <summary>
    /// Creates the client nonce SSPI sends, for HTTP Digest in the Schannel build and SASL DIGEST-MD5:
    /// 32 cryptographically random lower-case hexadecimal digits, as curl sends in both its own code
    /// and through SSPI (ADR-0139).
    /// </summary>
    /// <returns>The client nonce.</returns>
    public static string CreateRandomHex() => RandomNumberGenerator.GetHexString(32, lowercase: true);
}
