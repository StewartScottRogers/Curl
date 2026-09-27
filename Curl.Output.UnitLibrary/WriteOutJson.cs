using System.Globalization;
using System.Text;

namespace Curl.Output;

/// <summary>
/// The hand-written JSON <c>%{json}</c> and <c>%{header_json}</c> print, as curl 8.21.0's
/// <c>tool_writeout_json.c</c> writes it. Measured on 2026-09-27 against curl 8.21.0 (mingw,
/// Schannel); the commands are in BL-227's Notes. See ADR-0063.
/// </summary>
internal static class WriteOutJson
{
    /// <summary>The characters curl writes as a backslash and one more character.</summary>
    private static readonly Dictionary<char, string> ShortEscapes = new()
    {
        ['"'] = "\\\"",
        ['\\'] = "\\\\",
        ['\b'] = "\\b",
        ['\f'] = "\\f",
        ['\n'] = "\\n",
        ['\r'] = "\\r",
        ['\t'] = "\\t",
    };

    /// <summary>
    /// Quotes <paramref name="text"/> as curl's <c>jsonquoted</c> does: <c>"</c> and <c>\</c>
    /// escaped with a backslash, backspace, form feed, line feed, carriage return and tab as
    /// <c>\b</c>, <c>\f</c>, <c>\n</c>, <c>\r</c> and <c>\t</c>, any other character below
    /// U+0020 as <c>\u</c> and four lower-case hex digits, and everything else as it is.
    /// </summary>
    /// <param name="text">The text to quote.</param>
    /// <returns>The text between double quotes.</returns>
    public static string Quote(string text)
    {
        StringBuilder quoted = new(text.Length + 2);
        quoted.Append('"');
        foreach (char character in text)
        {
            AppendEscaped(quoted, character);
        }

        return quoted.Append('"').ToString();
    }

    /// <summary>
    /// The object <c>%{header_json}</c> prints: one member per header name, lower-cased, in
    /// the order the name first appeared, holding every value of that name in order, trimmed
    /// of spaces and tabs; members separated by a comma and a line feed, and a line feed
    /// before the closing brace, so no headers print <c>{</c>, a line feed and <c>}</c>.
    /// </summary>
    /// <param name="headers">The response headers in the order received.</param>
    /// <param name="valueWhitespace">The characters trimmed from both ends of each value.</param>
    /// <returns>The JSON object.</returns>
    public static string FormatHeaders(IReadOnlyList<KeyValuePair<string, string>> headers, char[] valueWhitespace)
    {
        List<(string Name, List<string> Values)> members = [];
        Dictionary<string, List<string>> valuesByName = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> header in headers)
        {
            string name = header.Key.ToLowerInvariant();
            if (!valuesByName.TryGetValue(name, out List<string>? values))
            {
                values = [];
                valuesByName.Add(name, values);
                members.Add((name, values));
            }

            values.Add(Quote(header.Value.Trim(valueWhitespace)));
        }

        IEnumerable<string> formatted = members.Select(member => $"{Quote(member.Name)}:[{string.Join(',', member.Values)}]");
        return "{" + string.Join(",\n", formatted) + "\n}";
    }

    private static void AppendEscaped(StringBuilder quoted, char character)
    {
        if (ShortEscapes.TryGetValue(character, out string? shortEscape))
        {
            quoted.Append(shortEscape);
        }
        else if (character < ' ')
        {
            quoted.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
        }
        else
        {
            quoted.Append(character);
        }
    }
}
