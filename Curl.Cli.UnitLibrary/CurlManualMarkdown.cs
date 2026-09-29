using System.Buffers;
using System.Text;

namespace Curl.Cli;

/// <summary>
/// Turns parts of <see cref="CurlManual"/> into Markdown for <see cref="CurlAiHelpText"/>: each option's
/// section by its long name, the exit codes and the <c>--write-out</c> variables. The manual is laid out
/// for a 79-column terminal: a paragraph's lines are joined into one line and its justifying runs of
/// spaces collapsed, a paragraph one column deeper than the text around it (curl's examples) becomes a
/// fenced code block kept as written, and a one-line paragraph followed by deeper ones (an exit code, a
/// <c>--write-out</c> variable) becomes a list item with those paragraphs under it.
/// </summary>
internal static class CurlManualMarkdown
{
    private const int TabWidth = 8;
    private const int OptionTextIndent = 12;
    private const int SectionTextIndent = 4;
    private const string OptionHeadingStart = "    -";
    private const string WriteOutVariablesIntroduction = "The variables available are:";

    /// <summary>The characters <see cref="Escape"/> puts a backslash before.</summary>
    private static readonly SearchValues<char> MarkdownSpecialCharacters = SearchValues.Create("\\`*<");

    private static readonly Lazy<IReadOnlyList<string>> ManualLines = new(CurlManual.Lines);

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<string>>> SectionsByLongName = new(ReadOptionSections);

    /// <summary>The Markdown of the option's manual section, its heading line left out.</summary>
    /// <param name="longName">The long name as its heading shows it, without <c>--</c>: <c>data</c>, <c>no-alpn</c>.</param>
    /// <param name="markdown">The Markdown lines; empty when the method returns <see langword="false"/>.</param>
    /// <returns><see langword="false"/> when the manual has no section for <paramref name="longName"/>.</returns>
    internal static bool TryGetOptionMarkdown(string longName, out IReadOnlyList<string> markdown)
    {
        bool found = SectionsByLongName.Value.TryGetValue(longName, out IReadOnlyList<string>? section);
        markdown = found ? Render(Blocks(section!, OptionTextIndent)) : [];
        return found;
    }

    /// <summary>The option's manual section as plain text: its paragraphs joined, one space between words.</summary>
    /// <param name="longName">The long name as its heading shows it, without <c>--</c>.</param>
    /// <returns>The text; empty when the manual has no section for <paramref name="longName"/>.</returns>
    internal static string OptionPlainText(string longName) =>
        SectionsByLongName.Value.TryGetValue(longName, out IReadOnlyList<string>? section)
            ? string.Join(' ', Blocks(section, OptionTextIndent).Select(block => block.Text))
            : string.Empty;

    /// <summary>The Markdown of the manual's <c>EXIT CODES</c> section, without its heading.</summary>
    /// <returns>The Markdown lines.</returns>
    internal static IReadOnlyList<string> ExitCodesMarkdown() =>
        Render(Blocks(Region("EXIT CODES", "BUGS"), SectionTextIndent));

    /// <summary>
    /// The Markdown list of the <c>--write-out</c> variables: the entries after <c>The variables available
    /// are:</c> in that option's section.
    /// </summary>
    /// <returns>The Markdown lines.</returns>
    internal static IReadOnlyList<string> WriteOutVariablesMarkdown()
    {
        List<ManualBlock> blocks = Blocks(SectionsByLongName.Value["write-out"], OptionTextIndent);
        int introduction = blocks.FindIndex(block => block.Text == WriteOutVariablesIntroduction);
        return Render(blocks.Skip(introduction + 1).TakeWhile(block => block.Kind != ManualBlockKind.Paragraph));
    }

    /// <summary>Escapes the characters that would make prose render as something else in Markdown.</summary>
    /// <param name="text">The prose.</param>
    /// <returns>The prose with <c>\</c>, <c>`</c>, <c>*</c> and <c>&lt;</c> escaped.</returns>
    internal static string Escape(string text)
    {
        StringBuilder escaped = new(text.Length);
        foreach (char character in text)
        {
            if (MarkdownSpecialCharacters.Contains(character))
            {
                escaped.Append('\\');
            }

            escaped.Append(character);
        }

        return escaped.ToString();
    }

    /// <summary>The lines strictly between the heading line <paramref name="heading"/> and <paramref name="nextHeading"/>.</summary>
    private static IReadOnlyList<string> Region(string heading, string nextHeading)
    {
        IReadOnlyList<string> lines = ManualLines.Value;
        int start = IndexOf(lines, heading, 0) + 1;
        return [.. lines.Skip(start).Take(IndexOf(lines, nextHeading, start) - start)];
    }

    private static int IndexOf(IReadOnlyList<string> lines, string line, int start)
    {
        int index = start;
        while (lines[index] != line)
        {
            index++;
        }

        return index;
    }

    /// <summary>Every option section of <c>ALL OPTIONS</c>, keyed by the long name its heading shows.</summary>
    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ReadOptionSections()
    {
        Dictionary<string, IReadOnlyList<string>> sections = new(StringComparer.Ordinal);
        IReadOnlyList<string> region = Region("ALL OPTIONS", "FILES");
        List<string>? section = null;
        foreach (string line in region)
        {
            if (line.StartsWith(OptionHeadingStart, StringComparison.Ordinal))
            {
                section = [];
                sections[HeadingLongName(line)] = section;
            }
            else
            {
                section?.Add(line);
            }
        }

        return sections;
    }

    /// <summary>The long name in a heading line: <c>    -d, --data &lt;data&gt;</c> gives <c>data</c>.</summary>
    private static string HeadingLongName(string heading)
    {
        string name = heading[(heading.IndexOf("--", StringComparison.Ordinal) + 2)..];
        int space = name.IndexOf(' ');
        return space < 0 ? name : name[..space];
    }

    /// <summary>
    /// Splits <paramref name="lines"/> into paragraphs at blank lines and classifies each against
    /// <paramref name="textIndent"/>, the column the section's prose starts at.
    /// </summary>
    private static List<ManualBlock> Blocks(IReadOnlyList<string> lines, int textIndent)
    {
        List<List<string>> paragraphs = Paragraphs(lines);
        List<ManualBlock> blocks = [];
        for (int index = 0; index < paragraphs.Count; index++)
        {
            int indent = Indent(paragraphs[index][0]);
            int nextIndent = index + 1 < paragraphs.Count ? Indent(paragraphs[index + 1][0]) : 0;
            ManualBlockKind kind = Classify(indent, nextIndent, paragraphs[index].Count, textIndent);
            blocks.Add(new ManualBlock(kind, kind == ManualBlockKind.Code ? CodeText(paragraphs[index], indent) : ProseText(paragraphs[index])));
        }

        return blocks;
    }

    private static ManualBlockKind Classify(int indent, int nextIndent, int lineCount, int textIndent)
    {
        if (indent == textIndent + 1)
        {
            return ManualBlockKind.Code;
        }

        if (indent > textIndent + 1)
        {
            return ManualBlockKind.Definition;
        }

        return lineCount == 1 && nextIndent > textIndent + 1 ? ManualBlockKind.Term : ManualBlockKind.Paragraph;
    }

    private static List<List<string>> Paragraphs(IReadOnlyList<string> lines)
    {
        List<List<string>> paragraphs = [];
        List<string>? paragraph = null;
        foreach (string line in lines)
        {
            if (line.Length == 0)
            {
                paragraph = null;
                continue;
            }

            if (paragraph is null || IntroducesDeeperLines(paragraph[^1], line))
            {
                paragraph = [];
                paragraphs.Add(paragraph);
            }

            paragraph.Add(line);
        }

        return paragraphs;
    }

    /// <summary>
    /// Whether <paramref name="line"/> starts a paragraph of its own although no blank line comes before it:
    /// <paramref name="previous"/> ends in <c>:</c> and <paramref name="line"/> is indented deeper, as the
    /// examples after <c>Examples:</c> are.
    /// </summary>
    private static bool IntroducesDeeperLines(string previous, string line) =>
        previous.EndsWith(':') && Indent(line) > Indent(previous);

    /// <summary>The column a line's text starts at, a tab advancing to the next multiple of eight.</summary>
    private static int Indent(string line) => ExpandTabs(line).TakeWhile(character => character == ' ').Count();

    private static string ExpandTabs(string line)
    {
        StringBuilder expanded = new(line.Length);
        foreach (char character in line)
        {
            expanded.Append(character == '\t' ? new string(' ', TabWidth - (expanded.Length % TabWidth)) : character.ToString());
        }

        return expanded.ToString();
    }

    private static string ProseText(List<string> paragraph) =>
        string.Join(' ', paragraph.SelectMany(line => line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)));

    /// <summary>
    /// The code lines joined with <c>\n</c>, each with the paragraph's first-line indent removed, or as much
    /// of it as a shallower line has.
    /// </summary>
    private static string CodeText(List<string> paragraph, int indent) =>
        string.Join('\n', paragraph.Select(line => ExpandTabs(line)[Math.Min(indent, Indent(line))..].TrimEnd()));

    private static List<string> Render(IEnumerable<ManualBlock> blocks)
    {
        List<string> markdown = [];
        foreach (ManualBlock block in blocks)
        {
            if (markdown.Count > 0)
            {
                markdown.Add(string.Empty);
            }

            markdown.AddRange(RenderBlock(block));
        }

        return markdown;
    }

    private static IEnumerable<string> RenderBlock(ManualBlock block)
    {
        return block.Kind switch
        {
            ManualBlockKind.Code => ["```", .. block.Text.Split('\n'), "```"],
            ManualBlockKind.Term => ["- `" + block.Text + "`"],
            ManualBlockKind.Definition => ["  " + Escape(block.Text)],
            _ => [Escape(block.Text)],
        };
    }

    private enum ManualBlockKind
    {
        Paragraph,
        Code,
        Term,
        Definition,
    }

    /// <summary>One paragraph of the manual: its kind and its text.</summary>
    private sealed record ManualBlock(ManualBlockKind Kind, string Text);
}
