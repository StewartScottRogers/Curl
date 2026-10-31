using System.Text;

namespace Curl.Extension.Protocol.OciRegistry.Auth;

/// <summary>
/// Parses a <c>WWW-Authenticate</c> value into an <see cref="OciBearerChallenge"/>: the <c>Bearer</c> scheme and its
/// auth-params (RFC 9110 §11.2, §11.6.1), each a token or a quoted string that may hold commas and backslash escapes.
/// </summary>
public static class OciBearerChallengeParser
{
    private const string BearerScheme = "Bearer";

    /// <summary>Parses <paramref name="value"/>.</summary>
    /// <param name="value">The <c>WWW-Authenticate</c> field value, or <see langword="null"/> when the response has none.</param>
    /// <returns>
    /// The challenge, or <see langword="null"/> when the value is missing, is not the <c>Bearer</c> scheme, breaks the
    /// auth-param syntax, or has no <c>realm</c>.
    /// </returns>
    public static OciBearerChallenge? Parse(string? value)
    {
        var text = value?.Trim(' ', '\t') ?? string.Empty;
        if (!IsBearerScheme(text))
        {
            return null;
        }

        var parameters = ParseParameters(text, BearerScheme.Length);
        return parameters is not null && parameters.TryGetValue("realm", out var realm)
            ? new OciBearerChallenge(realm, parameters.GetValueOrDefault("service"), parameters.GetValueOrDefault("scope"))
            : null;
    }

    // The scheme is case-insensitive (RFC 9110 §11.1) and ends at whitespace or the end of the value.
    private static bool IsBearerScheme(string text) =>
        text.StartsWith(BearerScheme, StringComparison.OrdinalIgnoreCase)
        && (text.Length == BearerScheme.Length || text[BearerScheme.Length] is ' ' or '\t');

    // auth-param = token BWS "=" BWS ( token / quoted-string ), separated by commas; names are case-insensitive and the first wins.
    private static Dictionary<string, string>? ParseParameters(string text, int position)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (position = SkipAny(text, position, " \t,"); position < text.Length; position = SkipAny(text, position, " \t,"))
        {
            var equals = text.IndexOf('=', position);
            var name = equals < 0 ? string.Empty : text[position..equals].Trim(' ', '\t');
            if (name.Length == 0)
            {
                return null;
            }

            (var parameterValue, position) = ReadValue(text, SkipAny(text, equals + 1, " \t"));
            if (parameterValue is null)
            {
                return null;
            }

            parameters.TryAdd(name, parameterValue);
        }

        return parameters;
    }

    private static int SkipAny(string text, int position, string skipped)
    {
        while (position < text.Length && skipped.Contains(text[position], StringComparison.Ordinal))
        {
            position++;
        }

        return position;
    }

    private static (string? Value, int End) ReadValue(string text, int position) =>
        position < text.Length && text[position] == '"' ? ReadQuotedString(text, position + 1) : ReadToken(text, position);

    private static (string? Value, int End) ReadToken(string text, int position)
    {
        var end = text.IndexOf(',', position);
        end = end < 0 ? text.Length : end;
        return (text[position..end].Trim(' ', '\t'), end);
    }

    // RFC 9110 §5.6.4: a backslash quotes the character after it; an unclosed quoted string breaks the syntax.
    private static (string? Value, int End) ReadQuotedString(string text, int position)
    {
        var value = new StringBuilder();
        for (; position < text.Length; position++)
        {
            var character = text[position];
            if (character == '"')
            {
                return (value.ToString(), position + 1);
            }

            if (character == '\\' && position + 1 < text.Length)
            {
                character = text[++position];
            }

            value.Append(character);
        }

        return (null, position);
    }
}
