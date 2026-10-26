using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Curl.Authentication;

/// <summary>
/// Reads a Digest challenge the way the SSPI behind curl 8.22.0's Schannel build reads it,
/// measured on Windows (BL-3606): what SSPI refuses, which curl reports as exit 94, and the
/// values it answers with.
/// </summary>
/// <remarks>
/// The parameters are comma-separated <c>key=value</c> elements, blanks allowed around the
/// key, the <c>=</c>, the value and each comma. A value in double quotes runs to the next
/// double quote not escaped by a backslash, and each backslash escapes the character after
/// it; any other value runs to the next comma, blanks trimmed. SSPI refuses an element
/// without <c>=</c>, an empty element or a trailing comma, a quoted value never closed or
/// followed by more than blanks before its comma, no <c>nonce</c> or more than one, an
/// <c>algorithm</c> other than MD5 and MD5-sess in any case, a <c>qop</c> list without
/// <c>auth</c>, and MD5-sess without one (whose answer SSPI hashes in a way not reproduced).
/// A repeated parameter of another kind takes its last value; <c>userhash</c> and unknown
/// keys are ignored. The realm is not SSPI's: it is the one curl reads itself, the last
/// <c>realm</c> among the pairs curl's own parser reads, which curl gives SSPI as the
/// identity's domain.
/// </remarks>
internal static class DigestSspiChallenge
{
    private const string Utf8Charset = "UTF-8";

    /// <summary>
    /// Reads the parameters of a Digest challenge as SSPI does.
    /// </summary>
    /// <param name="challenge">The header value.</param>
    /// <param name="index">The index just after the scheme name <c>Digest</c>.</param>
    /// <returns>
    /// The challenge, its <c>algorithm</c> named as SSPI writes it (<c>MD5</c> or
    /// <c>MD5-sess</c>); <see langword="null" /> when SSPI refuses it.
    /// </returns>
    internal static DigestChallenge? Read(string challenge, int index) =>
        ReadElements(challenge, index) is { } pairs ? Build(pairs, ReadCurlRealm(challenge, index)) : null;

    private static DigestChallenge? Build(List<KeyValuePair<string, string>> pairs, string? realm)
    {
        string[] nonces = ValuesOf(pairs, "nonce");
        string? algorithmName = LastValueOf(pairs, "algorithm");
        DigestAlgorithm? algorithm = algorithmName is null ? DigestAlgorithm.Md5 : DigestAlgorithm.Find(algorithmName);
        string? qopList = LastValueOf(pairs, "qop");
        string? qop = qopList is null ? null : DigestChallengeParameters.ReadQop(qopList);
        return IsRefused(nonces.Length, algorithm, qopList, qop)
            ? null
            : new DigestChallenge(
                realm,
                nonces[0],
                LastValueOf(pairs, "opaque"),
                qop,
                NameAsSspiWrites(algorithmName, algorithm),
                algorithm,
                userHash: false,
                sendsUtf8Charset: Utf8Charset.Equals(LastValueOf(pairs, "charset"), StringComparison.OrdinalIgnoreCase));
    }

    private static string? LastValueOf(List<KeyValuePair<string, string>> pairs, string key) =>
        ValuesOf(pairs, key).LastOrDefault();

    // Not exactly one nonce, an algorithm SSPI does not answer, or a qop it refuses.
    private static bool IsRefused(int nonceCount, [NotNullWhen(false)] DigestAlgorithm? algorithm, string? qopList, string? qop) =>
        nonceCount != 1 || algorithm is not { IsAnsweredBySspi: true } || IsQopRefused(qopList, qop, algorithm);

    // A qop list without auth, or MD5-sess without a qop.
    private static bool IsQopRefused(string? qopList, string? qop, DigestAlgorithm algorithm) =>
        qopList is null ? algorithm.IsSession : qop != "auth";

    private static string? NameAsSspiWrites(string? algorithmName, DigestAlgorithm algorithm) =>
        algorithmName is null ? null : (algorithm.IsSession ? "MD5-sess" : "MD5");

    private static string[] ValuesOf(List<KeyValuePair<string, string>> pairs, string key) =>
        [.. pairs.Where(pair => pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Value)];

    // Curl_override_sspi_http_realm: the last realm among the pairs curl's own parser reads.
    private static string? ReadCurlRealm(string challenge, int index) =>
        (DigestChallengeParameters.ReadPairs(challenge, index) ?? [])
            .Where(pair => pair.Key.Equals("realm", StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Value)
            .LastOrDefault();

    // Every element up to the end; null when one is malformed.
    private static List<KeyValuePair<string, string>>? ReadElements(string challenge, int index)
    {
        List<KeyValuePair<string, string>> pairs = [];
        do
        {
            if (ReadElement(challenge, ref index) is not { } pair)
            {
                return null;
            }

            pairs.Add(pair);
        }
        while (index++ < challenge.Length);

        return pairs;
    }

    // One key=value element, leaving the index at the comma after it or at the end.
    private static KeyValuePair<string, string>? ReadElement(string challenge, ref int index)
    {
        int end = challenge.IndexOf(',', index) is int comma and >= 0 ? comma : challenge.Length;
        int equals = challenge.IndexOf('=', index, end - index);
        string key = equals < 0 ? string.Empty : challenge[index..equals].Trim(' ', '\t');
        if (key.Length == 0)
        {
            return null;
        }

        index = DigestChallengeParameters.SkipBlanks(challenge, equals + 1);
        string? value = challenge.AsSpan(index).StartsWith("\"")
            ? ReadQuotedValue(challenge, ref index)
            : ReadUnquotedValue(challenge, ref index);
        return value is null ? null : new(key, value);
    }

    private static string ReadUnquotedValue(string challenge, ref int index)
    {
        int end = challenge.IndexOf(',', index) is int comma and >= 0 ? comma : challenge.Length;
        string value = challenge[index..end].TrimEnd(' ', '\t');
        index = end;
        return value;
    }

    // From the opening quote; null when it never closes or more than blanks follow it.
    private static string? ReadQuotedValue(string challenge, ref int index)
    {
        string value = ReadQuotedContent(challenge, ref index);
        if (index == challenge.Length)
        {
            return null;
        }

        index = DigestChallengeParameters.SkipBlanks(challenge, index + 1);
        return index == challenge.Length || challenge[index] == ',' ? value : null;
    }

    // From the opening quote to the closing one, or the end; each backslash escapes the next character.
    private static string ReadQuotedContent(string challenge, ref int index)
    {
        StringBuilder value = new();
        index++;
        while (index < challenge.Length && challenge[index] != '"')
        {
            index += challenge[index] == '\\' && index + 1 < challenge.Length ? 1 : 0;
            value.Append(challenge[index++]);
        }

        return value.ToString();
    }
}
