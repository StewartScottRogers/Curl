namespace Curl.Authentication;

/// <summary>
/// The parameters of a Digest challenge, read the way curl 8.21.0 reads them.
/// </summary>
/// <param name="realm">The <c>realm</c>, unescaped; <see langword="null" /> when absent.</param>
/// <param name="nonce">The <c>nonce</c>, unescaped.</param>
/// <param name="opaque">The <c>opaque</c>, unescaped; <see langword="null" /> when absent.</param>
/// <param name="qop">
/// <c>auth</c> when the <c>qop</c> list offers it, else <c>auth-int</c> when it offers that,
/// else <see langword="null" />.
/// </param>
/// <param name="algorithmName">
/// The <c>algorithm</c> value as received, which the answer echoes;
/// <see langword="null" /> when absent.
/// </param>
/// <param name="algorithm">The algorithm it names; MD5 when absent.</param>
/// <param name="userHash"><see langword="true" /> when <c>userhash</c> is <c>true</c>, in any case.</param>
internal sealed class DigestChallenge(
    string? realm,
    string nonce,
    string? opaque,
    string? qop,
    string? algorithmName,
    DigestAlgorithm algorithm,
    bool userHash)
{
    private const string Scheme = "Digest";

    /// <summary>Gets the <c>realm</c>, unescaped; <see langword="null" /> when absent.</summary>
    internal string? Realm { get; } = realm;

    /// <summary>Gets the <c>nonce</c>, unescaped.</summary>
    internal string Nonce { get; } = nonce;

    /// <summary>Gets the <c>opaque</c>, unescaped; <see langword="null" /> when absent.</summary>
    internal string? Opaque { get; } = opaque;

    /// <summary>Gets <c>auth</c>, <c>auth-int</c>, or <see langword="null" /> for no qop.</summary>
    internal string? Qop { get; } = qop;

    /// <summary>Gets the <c>algorithm</c> value as received; <see langword="null" /> when absent.</summary>
    internal string? AlgorithmName { get; } = algorithmName;

    /// <summary>Gets the algorithm the challenge names; MD5 when absent.</summary>
    internal DigestAlgorithm Algorithm { get; } = algorithm;

    /// <summary>Gets whether the user name is sent hashed.</summary>
    internal bool UserHash { get; } = userHash;

    /// <summary>
    /// Reads the first Digest challenge among the header values, as curl does: each value
    /// is split at every comma, blanks after a comma are skipped, and an element that
    /// starts with <c>Digest</c> in any case, not followed by a letter or digit, is a Digest
    /// challenge whose parameters run to the end of the value. A later Digest challenge is
    /// ignored even when the first cannot be answered.
    /// </summary>
    /// <param name="challenges">The header values, verbatim and in the order received.</param>
    /// <returns>
    /// The challenge; <see langword="null" /> when there is none, or when the first is one
    /// curl rejects: no blank after <c>Digest</c>, no nonce, an unknown algorithm, a
    /// <c>-sess</c> algorithm without a qop curl answers, or a malformed parameter.
    /// </returns>
    internal static DigestChallenge? ReadFirst(IReadOnlyList<string> challenges)
    {
        foreach (string challenge in challenges)
        {
            int start = DigestChallengeParameters.SkipBlanks(challenge, 0);
            while (start >= 0)
            {
                if (IsDigestAt(challenge, start))
                {
                    return DigestChallengeParameters.Read(challenge, start + Scheme.Length);
                }

                start = NextElement(challenge, start);
            }
        }

        return null;
    }

    // The start of the element after the next comma, blanks skipped; -1 when there is none.
    private static int NextElement(string challenge, int start)
    {
        int comma = challenge.IndexOf(',', start);
        return comma < 0 ? -1 : DigestChallengeParameters.SkipBlanks(challenge, comma + 1);
    }

    // A shorter remainder compares unequal, so a match leaves the index after the name in
    // range or at the end.
    private static bool IsDigestAt(string challenge, int start) =>
        string.Compare(challenge, start, Scheme, 0, Scheme.Length, StringComparison.OrdinalIgnoreCase) == 0
        && (start + Scheme.Length == challenge.Length || !char.IsAsciiLetterOrDigit(challenge[start + Scheme.Length]));
}
