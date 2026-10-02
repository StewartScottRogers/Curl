namespace Curl.Networking;

/// <summary>
/// Finds a Digest challenge carrying <c>stale=true</c> among a <c>407</c>'s
/// <c>Proxy-Authenticate</c> values: the proxy's word that the nonce the last answer used has
/// expired and the credential was right, which curl 8.21.0 answers again with the new nonce
/// instead of giving up (measured, BL-864 Notes).
/// </summary>
internal static class DigestStaleChallenge
{
    /// <summary>
    /// Decides whether any of <paramref name="challenges" /> is a Digest challenge whose
    /// <c>stale</c> parameter is <c>true</c>, in any case and quoted or not, as curl's
    /// <c>Curl_auth_decode_digest_http_message</c> reads it.
    /// </summary>
    /// <param name="challenges">The <c>Proxy-Authenticate</c> values, in the order received.</param>
    /// <returns><see langword="true" /> when the proxy marked the last Digest nonce stale.</returns>
    internal static bool IsOfferedIn(IReadOnlyList<string> challenges) =>
        challenges.Any(challenge => IsStaleDigest(challenge.Trim()));

    private static bool IsStaleDigest(string challenge) =>
        challenge.StartsWith("Digest", StringComparison.OrdinalIgnoreCase)
            && challenge.Length > "Digest".Length
            && char.IsWhiteSpace(challenge["Digest".Length])
            && challenge["Digest".Length..].Split(',').Any(IsStaleTrue);

    private static bool IsStaleTrue(string parameter)
    {
        var equals = parameter.IndexOf('=', StringComparison.Ordinal);
        return equals > 0
            && string.Equals(parameter[..equals].Trim(), "stale", StringComparison.OrdinalIgnoreCase)
            && string.Equals(parameter[(equals + 1)..].Trim().Trim('"'), "true", StringComparison.OrdinalIgnoreCase);
    }
}
