using System.Text.RegularExpressions;

namespace Curl.Cli.ManualPages;

/// <summary>
/// The options the generator knows, as <c>getshortlong</c> in upstream's <c>scripts/managen</c> learns them from each
/// option document's header: short name to long name, and long name to help text.
/// </summary>
public sealed class ManualOptionIndex
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private readonly Dictionary<string, string?> longByShort = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> helpByLong = new(StringComparer.Ordinal);

    /// <summary>Whether a document names <paramref name="shortName"/> as the short name of a long option.</summary>
    /// <param name="shortName">The short name.</param>
    /// <returns><see langword="true"/> when known.</returns>
    public bool HasShort(string shortName) => longByShort.TryGetValue(shortName, out string? longName) && ManualPerlValues.IsTrue(longName);

    /// <summary>Whether a document gives <paramref name="longName"/> a help text.</summary>
    /// <param name="longName">The long name.</param>
    /// <returns><see langword="true"/> when known.</returns>
    public bool HasHelp(string longName) => helpByLong.TryGetValue(longName, out string? help) && ManualPerlValues.IsTrue(help);

    /// <summary>Reads one option document's header into the index, warning about an oversized help text.</summary>
    /// <param name="context">The run.</param>
    /// <param name="file">The document's name; anything up to its last slash is dropped.</param>
    public void Add(ManualPageContext context, string file)
    {
        string name = Regex.Replace(file, "^.*/", "", RegexOptions.CultureInvariant);
        HeaderFields fields = new();
        int line = 0;
        bool start = false;
        foreach (string text in context.ReadLines(context.PathOf(name), $"could not find {context.PathOf(name)}"))
        {
            line++;
            if (!start)
            {
                start = IsHeaderMarker(text);
                continue;
            }

            if (IsHeaderMarker(text))
            {
                break;
            }

            ReadHeaderLine(context, name, line, text, fields);
        }

        Record(fields.ShortName, fields.LongName, fields.Help);
    }

    private static bool IsHeaderMarker(string text) => text.StartsWith("---", StringComparison.Ordinal);

    private static void ReadHeaderLine(ManualPageContext context, string name, int line, string text, HeaderFields fields)
    {
        fields.ShortName = Field(text, "^Short: (.)") ?? fields.ShortName;
        fields.LongName = Field(text, "^Long: (.*)") ?? fields.LongName;
        fields.Help = Help(context, name, line, text) ?? fields.Help;
    }

    private sealed class HeaderFields
    {
        public string? ShortName { get; set; }

        public string? LongName { get; set; }

        public string? Help { get; set; }
    }

    private void Record(string? shortName, string? longName, string? help)
    {
        if (ManualPerlValues.IsTrue(shortName))
        {
            longByShort[shortName] = longName;
        }

        if (ManualPerlValues.IsTrue(longName))
        {
            helpByLong[longName] = help;
        }
    }

    private static string? Help(ManualPageContext context, string name, int line, string text)
    {
        string? help = Field(text, "^Help: (.*)");
        if (help is { Length: >= 49 })
        {
            context.Error.Append($"{name}:{line}:1:WARN: oversized help text: {help.Length} characters\n");
        }

        return help;
    }

    private static string? Field(string text, string pattern) =>
        Regex.Match(text, pattern, Options) is { Success: true } match ? match.Groups[1].Value : null;
}
