namespace Curl.Protocol.Http;

/// <summary>
/// Finds a Digest challenge carrying <c>stale=true</c> among a <c>401</c>'s
/// <c>WWW-Authenticate</c> or a <c>407</c>'s <c>Proxy-Authenticate</c> values: the server's word
/// that the nonce the last answer used has expired and the credential was right, which curl
/// 8.21.0 answers again with the new nonce instead of taking the response as the result
/// (measured, BL-1148 Notes). The CONNECT tunnel has its own copy in
/// <c>Curl.Networking.UnitLibrary</c> (BL-864), as protocol libraries reference no other.
/// </summary>
internal static class HttpDigestStaleChallenge
{
    /// <summary>
    /// Decides whether any of <paramref name="challenges" /> is a Digest challenge whose
    /// <c>stale</c> parameter is <c>true</c>, in any case and quoted or not, as curl's
    /// <c>Curl_auth_decode_digest_http_message</c> reads it.
    /// </summary>
    /// <param name="challenges">The challenge header values, in the order received.</param>
    /// <returns><see langword="true" /> when the server marked the last Digest nonce stale.</returns>
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
