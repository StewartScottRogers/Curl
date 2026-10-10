namespace Curl.Conformance;

/// <summary>
/// One of upstream's <c>tests/certs/*.prm</c> files, read the way OpenSSL reads a config file
/// for <c>genserv.pl</c>: <c>[ section ]</c> headers, <c>key = value</c> lines in order, and
/// <c>#</c> comment lines and blank lines skipped.
/// </summary>
public sealed class UpstreamCertificateParameters
{
    private readonly Dictionary<string, List<KeyValuePair<string, string>>> sections;

    private UpstreamCertificateParameters(Dictionary<string, List<KeyValuePair<string, string>>> sections)
    {
        this.sections = sections;
    }

    /// <summary>Reads a <c>.prm</c> file's text.</summary>
    /// <exception cref="FormatException">A line is neither a section header nor <c>key = value</c>.</exception>
    public static UpstreamCertificateParameters Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Dictionary<string, List<KeyValuePair<string, string>>> sections = new(StringComparer.Ordinal);
        List<KeyValuePair<string, string>> current = [];
        sections[string.Empty] = current;
        foreach (string line in text.Split('\n'))
        {
            current = ReadLine(line.Trim(), sections, current);
        }

        return new(sections);
    }

    // Adds one trimmed line to the section being read; returns the section the next line goes to.
    private static List<KeyValuePair<string, string>> ReadLine(
        string line, Dictionary<string, List<KeyValuePair<string, string>>> sections, List<KeyValuePair<string, string>> current)
    {
        if (line.Length == 0 || line[0] == '#')
        {
            return current;
        }

        if (line[0] == '[' && line[^1] == ']')
        {
            List<KeyValuePair<string, string>> section = [];
            sections[line[1..^1].Trim()] = section;
            return section;
        }

        int equals = line.IndexOf('=', StringComparison.Ordinal);
        if (equals < 1)
        {
            throw new FormatException($"A .prm line is neither a [ section ] nor key = value: {line}");
        }

        current.Add(new(line[..equals].Trim(), line[(equals + 1)..].Trim()));
        return current;
    }

    /// <summary>The <c>key = value</c> pairs of <paramref name="section"/> in file order; none when it is absent.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Section(string section) =>
        sections.TryGetValue(section, out List<KeyValuePair<string, string>>? pairs) ? pairs : [];

    /// <summary>The value of <paramref name="key"/> in <paramref name="section"/>, or <see langword="null"/> when it is absent.</summary>
    public string? Find(string section, string key)
    {
        foreach (KeyValuePair<string, string> pair in Section(section))
        {
            if (pair.Key == key)
            {
                return pair.Value;
            }
        }

        return null;
    }
}
