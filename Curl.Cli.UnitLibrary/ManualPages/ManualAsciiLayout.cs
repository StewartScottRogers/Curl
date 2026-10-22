using System.Text.RegularExpressions;

namespace Curl.Cli.ManualPages;

/// <summary>
/// Lays out a page's description lines as <c>printdesc</c> in upstream's <c>scripts/managen</c> does: nroff lines as
/// they are, and ASCII lines, each prefixed <c>[level]</c> or <c>[levelq]</c> (quoted), as paragraphs justified to the
/// column width, indented four columns a level with tabs for every eight.
/// </summary>
/// <param name="context">The run.</param>
public sealed class ManualAsciiLayout(ManualPageContext context)
{
    private const int Indent = 4;

    /// <summary>Writes description lines at <paramref name="baseLevel"/>.</summary>
    /// <param name="manpage">Whether the page is nroff.</param>
    /// <param name="baseLevel">The level every line's own level is added to.</param>
    /// <param name="description">The lines.</param>
    public void PrintDescription(bool manpage, int baseLevel, IEnumerable<string> description)
    {
        ArgumentNullException.ThrowIfNull(description);
        if (manpage)
        {
            context.Output.AppendJoin("", description);
            return;
        }

        string previous = "-1";
        string paragraph = "";
        foreach (string entry in description)
        {
            (string level, string text) = SplitLevel(entry);
            if (previous.Contains('q', StringComparison.Ordinal) && !level.Contains('q', StringComparison.Ordinal))
            {
                context.Output.Append('\n');
            }

            if (level != previous)
            {
                OutputParagraph(baseLevel + LevelNumber(previous), paragraph);
                paragraph = "";
            }

            paragraph += AddLine(baseLevel, level, text);
            previous = level;
        }

        OutputParagraph(baseLevel + LevelNumber(previous), paragraph);
    }

    /// <summary>Writes one line unjustified at <paramref name="level"/>, its leading spaces added to the indent.</summary>
    /// <param name="level">The indent level.</param>
    /// <param name="words">The words, joined by one space.</param>
    public void LastLine(int level, IReadOnlyList<string> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        Match leading = Regex.Match(words[0], "^( +)", RegexOptions.CultureInvariant);
        PrefixLine((level * Indent) + leading.Length);
        context.Output.Append(words[0][leading.Length..]);
        foreach (string word in words.Skip(1))
        {
            context.Output.Append(' ').Append(word);
        }

        context.Output.Append('\n');
    }

    // A quoted line is written at once; any other joins the paragraph.
    private string AddLine(int baseLevel, string level, string text)
    {
        if (!level.Contains('q', StringComparison.Ordinal))
        {
            return text;
        }

        string line = ManualPerlValues.Chomp(text);
        int quotedLevel = baseLevel + LevelNumber(level) + 1;
        LastLine(quotedLevel, [line]);
        int width = (quotedLevel * Indent) + line.Length;
        if (width > context.ColumnWidth)
        {
            context.Error.Append($"ERROR: {width} columns is too long\n{line}\n");
            context.Errors++;
        }

        return "";
    }

    private void OutputParagraph(int level, string paragraph)
    {
        List<string> words = ManualPerlValues.Split(paragraph.Replace('\n', ' '), " +");
        int width = context.ColumnWidth - (level * Indent);
        int used = 0;
        List<string> line = [];
        foreach (string word in words)
        {
            if (used + word.Length + line.Count >= width)
            {
                JustLine(level, line);
                line = [];
                used = 0;
            }

            line.Add(word);
            used += word.Length;
        }

        if (used != 0)
        {
            LastLine(level, line);
            context.Output.Append('\n');
        }
    }

    private void JustLine(int level, List<string> line)
    {
        int used = line.Sum(word => word.Length + 1) - 1;
        int spaces = line.Count - 1;
        double inject = context.ColumnWidth - (level * Indent) - used;
        double ratio = spaces != 0 ? inject / spaces : 0;
        double spare = 0;
        PrefixLine(level * Indent);
        string separator = "";
        foreach (string word in line)
        {
            for (; spare >= 0.90; spare--)
            {
                context.Output.Append(' ');
            }

            context.Output.Append(separator).Append(word);
            separator = " ";
            spare += ratio;
        }

        context.Output.Append('\n');
    }

    private void PrefixLine(int columns) =>
        context.Output.Append('\t', Math.Max(0, columns / 8)).Append(' ', Math.Max(0, columns % 8));

    // The [level] prefix, which a line starting with a line break has not (its level is 0).
    private static (string Level, string Text) SplitLevel(string entry)
    {
        if (entry.StartsWith('\n') || entry.StartsWith('\r'))
        {
            return ("0", entry);
        }

        Match prefix = Regex.Match(entry, @"^\[([0-9q]*)\]", RegexOptions.CultureInvariant);
        return prefix.Success ? (prefix.Groups[1].Value, entry[prefix.Length..]) : ("", entry);
    }

    private static int LevelNumber(string level) => ManualPerlValues.Number(level.EndsWith('q') ? level[..^1] : level);
}
