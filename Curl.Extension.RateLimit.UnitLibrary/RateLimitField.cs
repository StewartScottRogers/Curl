using System.Globalization;

namespace Curl.Extension.RateLimit;

/// <summary>
/// Reads the <c>RateLimit</c> and <c>RateLimit-Policy</c> fields, each a Structured Fields List (RFC 9651 §3.1) of
/// Items naming a quota policy, with parameters (draft-ietf-httpapi-ratelimit-headers-11 §3, §4). A member whose name
/// is not a String (§3, §4) is skipped, and a parameter whose value is not a non-negative Integer (a <c>pk</c>
/// Byte Sequence, a negative number) is left out, so one bad member never hides the others.
/// </summary>
internal static class RateLimitField
{
    /// <summary>Reads a field's members.</summary>
    /// <param name="value">The field's value, every field line of the name joined with commas; <see langword="null" /> when absent.</param>
    /// <returns>Its members in order; none when the field is absent.</returns>
    public static IReadOnlyList<RateLimitItem> Parse(string? value)
    {
        if (value is null)
        {
            return [];
        }

        List<RateLimitItem> items = [];
        foreach (var member in SplitOutsideQuotes(value, ','))
        {
            if (ItemOf(member) is { } item)
            {
                items.Add(item);
            }
        }

        return items;
    }

    private static RateLimitItem? ItemOf(string member)
    {
        var parts = SplitOutsideQuotes(member, ';');
        if (NameOf(parts[0].Trim()) is not { } name)
        {
            return null;
        }

        Dictionary<string, long> parameters = new(StringComparer.Ordinal);
        foreach (var pair in parts.Skip(1).Select(parameter => parameter.Split('=', 2)))
        {
            if (pair.Length == 2 && long.TryParse(pair[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                parameters[pair[0].Trim()] = number;
            }
        }

        return new(name, parameters);
    }

    private static string? NameOf(string bareItem) =>
        bareItem.Length >= 2 && bareItem[0] == '"' && bareItem[^1] == '"' ? bareItem[1..^1] : null;

    private static List<string> SplitOutsideQuotes(string text, char separator)
    {
        List<string> parts = [];
        var start = 0;
        var quoted = false;
        for (var index = 0; index < text.Length; index++)
        {
            quoted ^= text[index] == '"';
            if (!quoted && text[index] == separator)
            {
                parts.Add(text[start..index]);
                start = index + 1;
            }
        }

        parts.Add(text[start..]);
        return parts;
    }
}
