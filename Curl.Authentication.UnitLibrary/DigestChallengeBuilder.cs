namespace Curl.Authentication;

/// <summary>
/// Collects a Digest challenge's parameters one by one, as curl 8.21.0 applies them, and
/// checks the result the way curl does.
/// </summary>
/// <remarks>
/// Keys are compared in any case. A repeated key replaces the earlier value, except that
/// a <c>qop</c> list offering neither <c>auth</c> nor <c>auth-int</c> leaves the earlier
/// choice alone, and <c>userhash</c> once <c>true</c> stays true. <c>stale</c> and unknown keys are ignored: <c>stale</c> only resets
/// curl's nonce count, which is always 1 here.
/// </remarks>
internal sealed class DigestChallengeBuilder
{
    private string? realm;
    private string? nonce;
    private string? opaque;
    private string? qop;
    private string? algorithmName;
    private DigestAlgorithm algorithm = DigestAlgorithm.Md5;
    private bool userHash;

    /// <summary>
    /// Applies one parameter.
    /// </summary>
    /// <param name="key">The key, as received.</param>
    /// <param name="value">The value, unescaped.</param>
    /// <returns><see langword="false" /> when curl rejects it: an unknown algorithm.</returns>
    internal bool TryApply(string key, string value)
    {
        switch (key.ToLowerInvariant())
        {
            case "nonce":
                nonce = value;
                break;
            case "realm":
                realm = value;
                break;
            case "opaque":
                opaque = value;
                break;
            case "qop":
                qop = DigestChallengeParameters.ReadQop(value) ?? qop;
                break;
            case "algorithm":
                algorithmName = value;
                DigestAlgorithm? found = DigestAlgorithm.Find(value);
                algorithm = found ?? algorithm;
                return found is not null;
            case "userhash":
                userHash |= value.Equals("true", StringComparison.OrdinalIgnoreCase);
                break;
        }

        return true;
    }

    /// <summary>
    /// Builds the challenge.
    /// </summary>
    /// <returns>
    /// The challenge; <see langword="null" /> when curl rejects it: no nonce, or a
    /// <c>-sess</c> algorithm without a qop.
    /// </returns>
    internal DigestChallenge? Build() =>
        nonce is null || (algorithm.IsSession && qop is null)
            ? null
            : new DigestChallenge(realm, nonce, opaque, qop, algorithmName, algorithm, userHash);
}
