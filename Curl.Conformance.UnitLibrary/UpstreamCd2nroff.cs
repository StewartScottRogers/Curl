using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Converts curl's "curldown" Markdown pages to the nroff man pages upstream's <c>scripts/cd2nroff</c> (curl 8.22.0)
/// writes from them at build time, so <see cref="UpstreamTest1222Script"/> can read the libcurl manual that no release
/// tarball carries (BL-3000, ADR-0752).
/// </summary>
/// <remarks>
/// It ports what the script does to each line: the front matter's <c>Title:</c>, <c>Section:</c>, <c>Added-in:</c>
/// and <c>See-also:</c> list; <c>## </c> headings as <c>.IP</c>, <c>##</c> as <c>.PP</c>, <c># </c> headings as
/// <c>.SH</c>; <c>~~~</c> and four-space blocks between <c>.nf</c> and <c>.fi</c>, their backslashes doubled and a
/// leading period or apostrophe escaped; HTML comments removed, bold, italics, backticks and <c>curl...(3)</c> mentions
/// as font changes, minuses, apostrophes and a leading quote or period escaped; and the blank lines it keeps. It leaves
/// out what the deprecation check never reads: the <c>.TH</c> line's date and source, the generated-by comment, the
/// prose of the generated PROTOCOLS section (only its <c>.SH PROTOCOLS</c> line is written), and the script's
/// validation of the front matter, whose messages go to standard error. A page without a closed front matter naming a
/// title and a section is not written, as the script writes none.
/// </remarks>
internal static class UpstreamCd2nroff
{
    private const RegexOptions Options = RegexOptions.CultureInvariant;

    private static readonly Regex HtmlComment = new("<!--.*?-->", Options);

    private static readonly Regex Bold = new(@"\*\*(\S.*?)\*\*", Options);

    private static readonly Regex Italics = new(@"\*(\w.*?[\w)])\*", Options);

    private static readonly Regex EscapedAngle = new(@"\\([<>])", Options);

    private static readonly Regex ManPageMention = new(@"((lib|)curl([^ ]*\(3\)))", Options | RegexOptions.IgnoreCase);

    private static readonly Regex Backticked = new("`(.*?)`", Options);

    private static readonly Regex Minus = new(@"([^\\])-", Options);

    private static readonly Regex LeadingDoubleQuote = new(@"^(\s*)""", Options);

    /// <summary>Converts every <c>*.md</c> page of a libcurl manual folder and of its <c>opts</c> folder.</summary>
    /// <param name="manualFolder">The <c>docs/libcurl</c> folder holding the Markdown sources.</param>
    /// <returns>Each written page's lines by its path in the built manual, <c>NAME.3</c> or <c>opts/NAME.3</c>.</returns>
    public static Dictionary<string, IReadOnlyList<string>> ConvertFolder(string manualFolder)
    {
        Dictionary<string, IReadOnlyList<string>> pages = new(StringComparer.Ordinal);
        AddPages(pages, manualFolder, string.Empty);
        AddPages(pages, $"{manualFolder}/opts", "opts/");
        return pages;
    }

    /// <summary>Converts one curldown page as the script does.</summary>
    /// <param name="markdownLines">The page's lines, without their line ends.</param>
    /// <returns>The page's file name, <c>TITLE.SECTION</c>, and its nroff lines; or <see langword="null"/> when the
    /// script writes no page.</returns>
    public static (string FileName, List<string> Lines)? Convert(IEnumerable<string> markdownLines)
    {
        ArgumentNullException.ThrowIfNull(markdownLines);
        using IEnumerator<string> lines = markdownLines.GetEnumerator();
        FrontMatter? front = FrontMatter.Read(lines);
        if (front is null)
        {
            return null;
        }

        PageWriter page = new(front);
        while (lines.MoveNext())
        {
            page.Add(lines.Current);
        }

        page.AddSeeAlso();
        return ($"{front.Title}.{front.Section}", page.Lines);
    }

    private static void AddPages(Dictionary<string, IReadOnlyList<string>> pages, string folder, string prefix)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (string markdown in Directory.EnumerateFiles(folder, "*.md"))
        {
            if (Convert(File.ReadLines(markdown)) is (string fileName, List<string> lines))
            {
                pages[prefix + fileName] = lines;
            }
        }
    }

    // Backslashes doubled and a leading period or apostrophe escaped, as in a verbatim block.
    private static string Quoted(string line)
    {
        string quoted = line.Replace("\\", "\\\\", StringComparison.Ordinal);
        return quoted.StartsWith('.') || quoted.StartsWith('\'') ? "\\&" + quoted : quoted;
    }

    private static bool IsBlank(string line) => line.All(character => character is ' ' or '\t');

    // A heading's word without the quotes enclosing it.
    private static string Unquoted(string word, string quotes) =>
        word.Length >= 2 && quotes.Contains(word[0], StringComparison.Ordinal) && quotes.Contains(word[^1], StringComparison.Ordinal) ? word[1..^1] : word;

    private sealed class FrontMatter
    {
        private int list;

        public string? Title { get; private set; }

        public string? Section { get; private set; }

        public string AddedIn { get; private set; } = string.Empty;

        public List<string> SeeAlso { get; } = [];

        // Skips to the first ---, reads to the second; null when it does not close or lacks a title or section.
        // The keywords read, each with what its value sets; any other line may be a list item.
        private static readonly (string Keyword, Action<FrontMatter, string> Read)[] Keywords =
        [
            ("Title:", (front, value) => front.Title = value.TrimStart(' ')),
            ("Section:", (front, value) => front.Section = value.TrimStart(' ')),
            ("See-also:", (front, value) => front.StartSeeAlso(value)),
            ("Protocol:", (front, _) => front.list = 2),
            ("TLS-backend:", (front, _) => front.list = 3),
            ("Added-in:", (front, value) => front.AddedIn = value.TrimStart(' ')),
        ];

        public static FrontMatter? Read(IEnumerator<string> lines)
        {
            FrontMatter front = new();
            bool closed = ReadToFence(lines, _ => { }) && ReadToFence(lines, front.ReadLine);
            return closed && front.Title is not null && front.Section is not null ? front : null;
        }

        // Reads lines up to a --- line; false when the lines end first.
        private static bool ReadToFence(IEnumerator<string> lines, Action<string> readLine)
        {
            while (lines.MoveNext())
            {
                if (lines.Current.StartsWith("---", StringComparison.Ordinal))
                {
                    return true;
                }

                readLine(lines.Current);
            }

            return false;
        }

        private void ReadLine(string line)
        {
            foreach ((string keyword, Action<FrontMatter, string> read) in Keywords)
            {
                if (line.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    read(this, line[keyword.Length..]);
                    return;
                }
            }

            AddListItem(line);
        }

        // See-also: with an item on its line starts the list with it.
        private void StartSeeAlso(string rest)
        {
            list = 1;
            if (rest.StartsWith(' '))
            {
                SeeAlso.Add(rest.TrimStart(' '));
            }
        }

        // An indented "- item" line belongs to the list last started.
        private void AddListItem(string line)
        {
            string item = line.TrimStart(' ');
            if (list == 1 && item.Length < line.Length && item.StartsWith("- ", StringComparison.Ordinal))
            {
                SeeAlso.Add(item[2..]);
            }
        }
    }

    private sealed class PageWriter(FrontMatter front)
    {
        private const int Fenced = 1;

        private const int Indented = 4;

        private int quote;

        private bool blankLine;

        private bool afterHeading;

        public List<string> Lines { get; } = [$".TH {front.Title} {front.Section}"];

        public void Add(string line)
        {
            if (quote == Fenced)
            {
                AddFenced(line);
            }
            else if (quote != Indented || !AddIndented(line))
            {
                AddMarkdown(line);
            }
        }

        public void AddSeeAlso()
        {
            Lines.Add(".SH SEE ALSO");
            string[] sorted = [.. front.SeeAlso.Order(StringComparer.Ordinal)];
            Lines.AddRange(sorted.Select((name, index) => index < sorted.Length - 1 ? $".BR {name}," : $".BR {name}"));
        }

        private void AddFenced(string line)
        {
            if (line.StartsWith("~~~", StringComparison.Ordinal))
            {
                quote = 0;
                Lines.Add(".fi");
            }
            else
            {
                Lines.Add(Quoted(line));
            }
        }

        // A line still indented stays in the block; any other ends it and is read as Markdown.
        private bool AddIndented(string line)
        {
            if (line.StartsWith("    ", StringComparison.Ordinal))
            {
                Lines.Add(Quoted(line[4..]));
                return true;
            }

            quote = 0;
            Lines.Add(".fi");
            return false;
        }

        private void AddMarkdown(string line)
        {
            if (!AddHeadingLine(line))
            {
                AddBodyLine(line);
            }
        }

        // ## item, ## alone and # section headings; false for any other line.
        private bool AddHeadingLine(string line)
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                string word = Unquoted(line[3..], "\"'`");
                AddHeading(word.Contains(' ', StringComparison.Ordinal) ? $".IP \"{word}\"" : $".IP {word}");
            }
            else if (line.StartsWith("##", StringComparison.Ordinal))
            {
                AddHeading(".PP");
            }
            else if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                AddSection(Unquoted(line[2..], "\"'"));
            }
            else
            {
                return false;
            }

            return true;
        }

        private void AddBodyLine(string line)
        {
            if (line.StartsWith("~~~", StringComparison.Ordinal))
            {
                StartBlock(Fenced);
            }
            else if (line.StartsWith("    ", StringComparison.Ordinal))
            {
                StartBlock(Indented);
                Lines.Add(Quoted(line[4..]));
            }
            else if (IsBlank(line))
            {
                blankLine = true;
            }
            else
            {
                AddText(Inline(line));
            }
        }

        private void AddHeading(string directive)
        {
            Lines.Add(directive);
            afterHeading = true;
        }

        private void AddSection(string word)
        {
            if (word == "%PROTOCOLS%")
            {
                AddHeading(".SH PROTOCOLS");
            }
            else if (word == "%AVAILABILITY%")
            {
                afterHeading = true;
                if (front.AddedIn != "n/a")
                {
                    Lines.Add(".SH AVAILABILITY");
                    Lines.Add($"Added in curl {front.AddedIn}");
                }
            }
            else
            {
                AddHeading($".SH {word}");
            }
        }

        private void StartBlock(int kind)
        {
            AddParagraphBreak();
            afterHeading = false;
            Lines.Add(".nf");
            quote = kind;
        }

        private void AddText(string text)
        {
            AddParagraphBreak();
            blankLine = false;
            afterHeading = false;
            string escaped = LeadingDoubleQuote.Replace(Minus.Replace(text, @"$1\-").Replace("'", @"\(aq", StringComparison.Ordinal), @"$1\&""", 1);
            escaped = escaped.StartsWith('.') ? "\\&" + escaped : escaped;
            if (IsBlank(escaped))
            {
                blankLine = true;
            }
            else
            {
                Lines.Add(escaped);
            }
        }

        // A blank line before content, unless the content follows a heading.
        private void AddParagraphBreak()
        {
            if (blankLine && !afterHeading)
            {
                Lines.Add(string.Empty);
            }
        }

        private static string Inline(string line)
        {
            string text = HtmlComment.Replace(line, string.Empty);
            text = Italics.Replace(Bold.Replace(text, @"\fB$1\fP"), @"\fI$1\fP");
            text = EscapedAngle.Replace(text, "$1");
            return Backticked.Replace(ManPageMention.Replace(text, @"\fI$1\fP"), @"\fI$1\fP");
        }
    }
}
