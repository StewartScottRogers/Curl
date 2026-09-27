namespace Curl.Conformance;

/// <summary>
/// Recognises tag lines of an upstream test file the way <c>getpart.pm</c> at <c>curl-8_21_0</c>
/// does: a line that starts, after any spaces, with <c>&lt;name</c> or <c>&lt;/name</c> followed by
/// a space or <c>&gt;</c>. Whatever follows the tag on the line is ignored.
/// </summary>
internal static class UpstreamTestFileTag
{
    /// <summary>Reads an opening tag and its attributes (<c>^ *&lt;name[ &gt;]</c>).</summary>
    /// <param name="line">One line of the file.</param>
    /// <param name="name">The opened element's name; empty when the line is not an opening tag.</param>
    /// <param name="attributes">The attributes; empty when the line is not an opening tag.</param>
    /// <returns><see langword="true"/> when the line is an opening tag.</returns>
    public static bool TryReadOpening(string line, out string name, out IReadOnlyDictionary<string, string> attributes)
    {
        string trimmed = line.TrimStart(' ');
        name = trimmed.StartsWith('<') ? NameAt(trimmed, 1) : string.Empty;
        string afterName = trimmed[Math.Min(trimmed.Length, name.Length + 1)..];
        bool isTag = name.Length > 0 && EndsName(afterName);
        int attributesEnd = afterName.IndexOf('>');
        attributes = isTag ? ReadAttributes(attributesEnd < 0 ? afterName : afterName[..attributesEnd]) : new Dictionary<string, string>();
        return isTag;
    }

    /// <summary>Reads a closing tag (<c>^ *&lt;/name[ &gt;]</c>).</summary>
    /// <param name="line">One line of the file.</param>
    /// <param name="name">The closed element's name; empty when the line is not a closing tag.</param>
    /// <returns><see langword="true"/> when the line is a closing tag.</returns>
    public static bool TryReadClosing(string line, out string name)
    {
        string trimmed = line.TrimStart(' ');
        name = trimmed.StartsWith("</", StringComparison.Ordinal) ? NameAt(trimmed, 2) : string.Empty;
        return name.Length > 0 && EndsName(trimmed[(name.Length + 2)..]);
    }

    /// <summary>Whether the line opens an element with the given name.</summary>
    /// <param name="line">One line of the file.</param>
    /// <param name="name">The element name.</param>
    /// <returns><see langword="true"/> when the line is an opening tag for <paramref name="name"/>.</returns>
    public static bool Opens(string line, string name) =>
        TryReadOpening(line, out string opened, out _) && opened == name;

    /// <summary>Whether the line closes an element with the given name.</summary>
    /// <param name="line">One line of the file.</param>
    /// <param name="name">The element name.</param>
    /// <returns><see langword="true"/> when the line is a closing tag for <paramref name="name"/>.</returns>
    public static bool Closes(string line, string name) =>
        TryReadClosing(line, out string closed) && closed == name;

    // s/ *([^=]*)= *("([^"]*)"|'([^']*)')// repeated: a name is everything before '=', and the
    // value is double- or single-quoted. Reading stops at the first attribute that is not.
    private static Dictionary<string, string> ReadAttributes(string text)
    {
        Dictionary<string, string> attributes = new(StringComparer.Ordinal);
        string rest = text.TrimStart(' ');
        int equals = rest.IndexOf('=');
        while (equals >= 0 && TryReadQuotedValue(rest[(equals + 1)..], out string value, out string afterValue))
        {
            attributes[rest[..equals]] = value;
            rest = afterValue.TrimStart(' ');
            equals = rest.IndexOf('=');
        }

        return attributes;
    }

    private static bool TryReadQuotedValue(string afterEquals, out string value, out string afterValue)
    {
        string quoted = afterEquals.TrimStart(' ');
        int closingQuote = quoted.StartsWith('"') || quoted.StartsWith('\'') ? quoted.IndexOf(quoted[0], 1) : -1;
        value = closingQuote < 0 ? string.Empty : quoted[1..closingQuote];
        afterValue = quoted[(closingQuote + 1)..];
        return closingQuote >= 0;
    }

    private static string NameAt(string text, int start) =>
        new([.. text.Skip(start).TakeWhile(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-')]);

    private static bool EndsName(string afterName) =>
        afterName.StartsWith(' ') || afterName.StartsWith('>');
}
