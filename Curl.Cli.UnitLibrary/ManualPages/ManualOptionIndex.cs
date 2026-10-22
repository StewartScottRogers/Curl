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
        string? shortName = null;
        string? longName = null;
        string? help = null;
        int line = 0;
        bool start = false;
        foreach (string text in context.ReadLines(context.PathOf(name), $"could not find {context.PathOf(name)}"))
        {
            line++;
            if (!start || text.StartsWith("---", StringComparison.Ordinal))
            {
                if (start)
                {
                    break;
                }

                start = text.StartsWith("---", StringComparison.Ordinal);
                continue;
            }

            shortName = Field(text, "^Short: (.)") ?? shortName;
            longName = Field(text, "^Long: (.*)") ?? longName;
            help = Help(context, name, line, text) ?? help;
        }

        Record(shortName, longName, help);
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
