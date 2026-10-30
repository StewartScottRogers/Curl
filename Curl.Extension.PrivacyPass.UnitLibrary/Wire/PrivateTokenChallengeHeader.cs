namespace Curl.Extension.PrivacyPass.Wire;

/// <summary>
/// One <c>PrivateToken</c> challenge of a <c>WWW-Authenticate</c> field (RFC 9577 §2.1.1): its <c>challenge</c> and
/// <c>token-key</c> parameters, base64url-decoded.
/// </summary>
/// <param name="Challenge">The decoded <c>TokenChallenge</c>.</param>
/// <param name="TokenKey">The <c>token-key</c> parameter's bytes: for token type <c>0x0002</c>, the issuer's public key as a DER <c>SubjectPublicKeyInfo</c>.</param>
public sealed record PrivateTokenChallengeHeader(PrivateTokenChallenge Challenge, byte[] TokenKey)
{
    /// <summary>The authentication scheme's name, compared case-insensitively (RFC 9110 §11.1).</summary>
    public const string Scheme = "PrivateToken";

    /// <summary>
    /// Finds every <c>PrivateToken</c> challenge in <paramref name="fieldValue"/> whose <c>challenge</c> and <c>token-key</c>
    /// parameters are present and decode, in the order they appear (RFC 9577 §2.1.1 lets a server send several).
    /// </summary>
    /// <param name="fieldValue">One <c>WWW-Authenticate</c> field value.</param>
    /// <returns>The challenges; empty when there is none.</returns>
    public static IEnumerable<PrivateTokenChallengeHeader> FindAll(string fieldValue)
    {
        ArgumentNullException.ThrowIfNull(fieldValue);
        Dictionary<string, string>? parameters = null;
        foreach (var element in SplitOutsideQuotes(fieldValue))
        {
            var (scheme, parameter) = SplitScheme(element);
            if (scheme is not null)
            {
                if (From(parameters) is { } found)
                {
                    yield return found;
                }

                parameters = string.Equals(scheme, Scheme, StringComparison.OrdinalIgnoreCase) ? [] : null;
            }

            AddParameter(parameters, parameter);
        }

        if (From(parameters) is { } last)
        {
            yield return last;
        }
    }

    private static PrivateTokenChallengeHeader? From(Dictionary<string, string>? parameters) =>
        DecodedParameter(parameters, "challenge") is { } challengeBytes
        && DecodedParameter(parameters, "token-key") is { } tokenKeyBytes
        && PrivateTokenChallenge.Decode(challengeBytes) is { } decoded
            ? new(decoded, tokenKeyBytes)
            : null;

    private static byte[]? DecodedParameter(Dictionary<string, string>? parameters, string name) =>
        parameters is not null && parameters.TryGetValue(name, out var value) ? Base64Url.Decode(value) : null;

    // An element that begins with a token followed by a space starts a new challenge (RFC 9110 §11.6.1).
    private static (string? Scheme, string Parameter) SplitScheme(string element)
    {
        var space = element.IndexOf(' ', StringComparison.Ordinal);
        var equals = element.IndexOf('=', StringComparison.Ordinal);
        if (equals < 0)
        {
            return (element, string.Empty);
        }

        return space > 0 && space < equals ? (element[..space], element[(space + 1)..].Trim()) : (null, element);
    }

    private static void AddParameter(Dictionary<string, string>? parameters, string parameter)
    {
        var equals = parameter.IndexOf('=', StringComparison.Ordinal);
        if (parameters is null || equals <= 0)
        {
            return;
        }

        var value = parameter[(equals + 1)..].Trim();
        parameters[parameter[..equals].Trim().ToLowerInvariant()] = value.Length > 1 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
    }

    private static IEnumerable<string> SplitOutsideQuotes(string value)
    {
        var start = 0;
        var quoted = false;
        for (var index = 0; index < value.Length; index++)
        {
            quoted ^= value[index] == '"';
            if (value[index] == ',' && !quoted)
            {
                yield return value[start..index].Trim();
                start = index + 1;
            }
        }

        yield return value[start..].Trim();
    }
}
