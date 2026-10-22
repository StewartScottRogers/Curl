using System.Globalization;
using System.Text.RegularExpressions;

namespace Curl.Cli.ManualPages;

/// <summary>
/// Turns the Markdown body of a page or option document into description lines, as <c>render</c> in upstream's
/// <c>scripts/managen</c> does: nroff text with <c>.SH</c>, <c>.IP</c>, <c>.RS</c>/<c>.RE</c> and <c>.nf</c>/<c>.fi</c>,
/// or ASCII text with <c>[level]</c> prefixes for <see cref="ManualAsciiLayout"/>. A failed check returns its code as
/// the one line, as the script's <c>return</c> does into its list.
/// </summary>
/// <param name="context">The run.</param>
/// <param name="manpage">Whether the page is nroff.</param>
/// <param name="fileName">The document's name, for messages.</param>
/// <param name="line">The line before the body; 1 means a top-level page, not an option.</param>
internal sealed class ManualMarkdownRenderer(ManualPageContext context, bool manpage, string fileName, int line)
{
    private const RegexOptions Options = RegexOptions.CultureInvariant;

    private readonly bool top = line == 1;
    private readonly List<string> description = [];
    private int lineNumber = line;
    private bool tableMode;
    private bool header;
    private bool start;
    private bool finalBlank;
    private int quote;
    private int level;
    private int blankLines;

    /// <summary>Renders the body's lines.</summary>
    /// <param name="lines">The lines after the header, each with its line feed.</param>
    /// <returns>The description lines, or one line holding a failed check's code.</returns>
    public List<string> Render(IEnumerable<string> lines)
    {
        foreach (string text in lines)
        {
            lineNumber++;
            finalBlank = text == "\n";
            if (RenderLine(text) is { } code)
            {
                return [code.ToString(CultureInfo.InvariantCulture)];
            }
        }

        if (finalBlank)
        {
            throw Exit($"{fileName}:{lineNumber}:1:ERROR: trailing blank line\n", 3);
        }

        AddIf(manpage && quote != 0, ".fi\n");
        AddIf(manpage && tableMode, ".RE\n.IP\n");
        return description;
    }

    private int? RenderLine(string text)
    {
        if (Regex.Match(text, @"^\.(SH|BR|IP|B)", Options) is { Success: true } nroff)
        {
            context.Error.Append($"{fileName}:{lineNumber}:1:ERROR: nroff instruction in input: \".{nroff.Groups[1].Value}\"\n");
            return 4;
        }

        if (Regex.IsMatch(text, "^ *<!--", Options) || (!start && Regex.IsMatch(text, @"^[\r\n]*\z", Options)))
        {
            return null;
        }

        start = true;
        if (Regex.IsMatch(text, "^[ \t]*\n", Options))
        {
            blankLines++;
            return null;
        }

        string? body = QuoteLine(text);
        if (body is null)
        {
            return null;
        }

        return HeaderLine(text) is { } handled ? NullWhenZero(handled) : TextLine(body);
    }

    private string? QuoteLine(string text)
    {
        if (Regex.Match(text, "^    (.*)", Options) is { Success: true } indented)
        {
            OpenQuoteIf(quote == 0);
            quote = 1;
            return indented.Groups[1].Value + "\n";
        }

        if (text.StartsWith("```", StringComparison.Ordinal) || text.StartsWith("~~~", StringComparison.Ordinal))
        {
            FenceLine();
            return null;
        }

        AddIf(manpage && quote == 1, ".fi\n");
        quote = quote == 1 ? 0 : quote;
        return text;
    }

    private void FenceLine()
    {
        if (quote == 0)
        {
            OpenQuoteIf(true);
            quote = 2;
            return;
        }

        AddIf(manpage, ".fi\n");
        quote = 0;
    }

    private void OpenQuoteIf(bool opens)
    {
        if (opens && manpage)
        {
            AddIf(blankLines != 0, "\n");
            description.Add(".nf\n");
            blankLines = 0;
        }
    }

    // null: not a header; 0: handled; otherwise the code a failed check returns.
    private int? HeaderLine(string text)
    {
        if (Regex.Match(text, "^# (.*)", Options) is { Success: true } title)
        {
            header = true;
            blankLines += top ? 0 : 1;
            AddIf(top, manpage ? $".SH {title.Groups[1].Value}\n" : $"[0]{title.Groups[1].Value}\n");
            return 0;
        }

        if (text.StartsWith("###", StringComparison.Ordinal))
        {
            throw Exit($"{fileName}:{lineNumber}:1:ERROR: ### header is not supported\n", 3);
        }

        if (Regex.Match(text, "^## (.*)", Options) is { Success: true } item)
        {
            SubHeader(item.Groups[1].Value);
            return 0;
        }

        if (text.StartsWith("##", StringComparison.Ordinal))
        {
            EndTable();
            return 0;
        }

        return NroffBlock(text);
    }

    private int? NroffBlock(string text)
    {
        if (Regex.Match(text, @"^\.(IP|RS|RE)", Options) is { Success: true } block)
        {
            context.Error.Append($"{fileName}:{lineNumber}:1:ERROR: {block.Groups[1].Value} detected, use ##-style\n");
            return 3;
        }

        return null;
    }

    private void SubHeader(string title)
    {
        string word = Regex.Replace(title, "[\"'](.*)[\"']\\z", "$1", Options).Replace("`", "", StringComparison.Ordinal);
        string nonTick = Regex.Replace(title, "`[^`]*`", "", Options);
        if (Regex.IsMatch(nonTick, @"[^\\][<>]", Options))
        {
            context.Error.Append($"{fileName}:{lineNumber}:1:WARN: un-escaped < or > used: {nonTick}\n");
        }

        word = manpage && word.Contains(' ', StringComparison.Ordinal) ? $"\"{word}\"" : word;
        level = 1;
        AddIf(!top && !tableMode && manpage, ".RS\n");
        tableMode = tableMode || !top;
        description.AddRange(manpage ? [$".IP {word}\n"] : ["\n", $"[1]{word}\n"]);
        header = true;
    }

    private void EndTable()
    {
        if (top)
        {
            throw Exit($"{fileName}:{lineNumber}:1:ERROR: ## empty header top-level mode\n", 3);
        }

        if (tableMode)
        {
            description.Add(manpage ? ".RE\n.IP\n" : "\n");
            tableMode = false;
            level = 0;
        }

        header = true;
    }

    private int? TextLine(string body)
    {
        string text = body.Replace("`%DATE`", context.Date, StringComparison.Ordinal)
            .Replace("`%VERSION`", context.Version, StringComparison.Ordinal)
            .Replace("`%GLOBALS`", context.Globals, StringComparison.Ordinal);
        if (((quote == 0 ? CheckPlainText(text) : null) ?? CheckAddedIn(text.Replace('`', '"'))) is { } code)
        {
            return code;
        }

        text = RemoveOldAddedIn(text.Replace('`', '"'));
        text = Regex.Replace(text, @"\\([><])", "$1", Options);
        text = Regex.Replace(manpage ? NroffText(text) : AsciiText(text), "[ \t]+\\z", "", Options);
        AddIf(blankLines != 0 && !header, "\n");
        blankLines = 0;
        description.Add(manpage ? text : $"[{1 + level}{(quote != 0 ? "q" : "")}]{text}");
        header = false;
        return null;
    }

    private int? CheckPlainText(string text)
    {
        if (Regex.Match(text, "^(.*)  ", Options) is { Success: true } twoSpaces)
        {
            context.Error.Append($"{fileName}:{lineNumber}:{twoSpaces.Groups[1].Length}:ERROR: 2 spaces detected\n");
            return 3;
        }

        string back = Regex.Replace(text, "`(.*?)`", "", Options);
        if (Regex.IsMatch(back, @"[^\\][<>]", Options))
        {
            context.Error.Append($"{fileName}:{lineNumber}:1:WARN: un-escaped < or > used: {back}\n");
            return 3;
        }

        return null;
    }

    private int? CheckAddedIn(string text)
    {
        if (Regex.Match(text, @"\(added in(.*)", Options | RegexOptions.IgnoreCase) is { Success: true } added && added.Groups[1].Length < 2)
        {
            context.Error.Append($"{fileName}:{lineNumber}:1:ERROR: broken up added-in line:\n{fileName}:{lineNumber}:1:ERROR: {text}");
            return 3;
        }

        return null;
    }

    private static string RemoveOldAddedIn(string text)
    {
        for (Match added = AddedIn(text); added.Success && ManualNroffText.IsTooOld(added.Groups[1].Value); added = AddedIn(text))
        {
            text = Regex.Replace(text, $" *\\(Added in {added.Groups[1].Value}\\)", "", Options | RegexOptions.IgnoreCase);
        }

        return text;
    }

    private static Match AddedIn(string text) => Regex.Match(text, @"\(Added in ([0-9.]+)\)", Options | RegexOptions.IgnoreCase);

    private string NroffText(string text)
    {
        text = text.Replace("\\", "\\\\", StringComparison.Ordinal);
        if (quote == 0)
        {
            text = Regex.Replace(text, "--([a-z0-9.-]+)", match => ManualNroffText.Manpageify(match.Groups[1].Value, true), Options);
        }

        text = Regex.Replace(text, @"([^\\])-", @"$1\-", Options).Replace("'", @"\(aq", StringComparison.Ordinal);
        text = Regex.Replace(text, "^([.\"])", @"\&$1", Options);
        text = Regex.Replace(text, @"\*\*(\S.*?)\*\*", @"\fB$1\fP", Options);
        return Regex.Replace(text, @"\*(\S.*?)\*", @"\fI$1\fP", Options);
    }

    private static string AsciiText(string text) =>
        Regex.Replace(Regex.Replace(text, @"\*\*(\S.*?)\*\*", "$1", Options), @"\*(\S.*?)\*", "$1", Options);

    private void AddIf(bool adds, string text)
    {
        if (adds)
        {
            description.Add(text);
        }
    }

    private ManualPageExit Exit(string message, int code)
    {
        context.Error.Append(message);
        return new ManualPageExit(code);
    }

    private static int? NullWhenZero(int handled) => handled == 0 ? null : handled;
}
