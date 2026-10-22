using System.Globalization;
using System.Text.RegularExpressions;

namespace Curl.Cli.ManualPages;

/// <summary>
/// Writes one option document's part of the page, as <c>single</c> in upstream's <c>scripts/managen</c> does for the
/// main page: its names, its description, the lines its <c>Scope</c> and <c>Multi</c> add, its examples and its footer
/// of requirements, exclusions, version and see-also names.
/// </summary>
/// <param name="context">The run.</param>
/// <param name="manpage">Whether the page is nroff.</param>
public sealed class ManualOptionPage(ManualPageContext context, bool manpage)
{
    private const int ExampleWidth = 60;

    private static readonly HashSet<string> KnownProtocols = new(StringComparer.Ordinal)
    {
        "DNS", "FILE", "FTP", "FTPS", "GSS/kerberos", "HTTP", "HTTPS", "IMAP", "IPFS", "LDAP", "MQTT", "POP3", "SCP", "SFTP",
        "SMTP", "SSL", "TELNET", "TFTP", "TLS",
    };

    // The line each Multi adds: {0} is the paragraph prefix, {1} the option's name, {2} the option that disables it.
    private static readonly Dictionary<string, string> MultiLines = new(StringComparer.Ordinal)
    {
        ["single"] = "{0}If {1} is provided several times, the last set value is used.\n",
        ["append"] = "{0}{1} can be used several times in a command line.\n",
        ["boolean"] = "{0}Providing {1} multiple times has no extra effect.\nDisable it again with {2}.\n",
        ["mutex"] = "{0}Providing {1} multiple times has no extra effect.\n",
        ["per-URL"] = "{0}{1} is associated with a single URL. Use it once per URL when you use several URLs in a command line.\n",
    };

    private readonly ManualAsciiLayout layout = new(context);

    private string Prefix => manpage ? "\n" : "[1]";

    private string LeadingPrefix => manpage ? "" : "[1]";

    /// <summary>Writes the option document <paramref name="fileName"/>.</summary>
    /// <param name="fileName">The document's name in the run's folder.</param>
    /// <returns>0, or the code the script returns for a document it refuses.</returns>
    public int Write(string fileName)
    {
        List<string> lines = context.ReadLines(context.PathOf(fileName), $"could not find {context.PathOf(fileName)}");
        (ManualOptionHeader header, int? failure) = ManualOptionHeader.Parse(context, fileName, lines);
        return failure ?? WriteOption(header, fileName, lines);
    }

    private int WriteOption(ManualOptionHeader header, string fileName, List<string> lines)
    {
        List<string> description = new ManualMarkdownRenderer(context, manpage, fileName, header.LineNumber).Render(lines.Skip(header.BodyStart));
        WriteNames(header);
        List<string> leading = Leading(header, fileName);
        List<string> extra = [];
        return Scope(header, fileName, description) ?? Extra(header, fileName, extra) ?? WriteDescription(header, fileName, [.. leading, .. description, .. extra]);
    }

    private int WriteDescription(ManualOptionHeader header, string fileName, List<string> description)
    {
        layout.PrintDescription(manpage, 2, description);
        string seeAlso = SeeAlso(header, fileName);
        List<string> foot = Foot(header, fileName);
        return Examples(header, fileName) ?? WriteFoot(header, foot, seeAlso);
    }

    private void WriteNames(ManualOptionHeader header)
    {
        string names = (header.Short is null ? "" : $"-{header.Short}, ") + $"--{header.Long}" + (ManualPerlValues.IsTrue(header.Arg) ? $" {header.Arg}" : "");
        if (manpage)
        {
            context.Output.Append($".IP \"{names.Replace("-", "\\-", StringComparison.Ordinal)}\"\n");
            return;
        }

        layout.LastLine(1, [names]);
    }

    private List<string> Leading(ManualOptionHeader header, string fileName)
    {
        List<string> leading = [];
        if (ManualPerlValues.IsTrue(header.Protocols))
        {
            RefuseUnknownProtocol(header.Protocols, header.LineNumber, fileName);
            leading.Add($"{LeadingPrefix}({header.Protocols}) ");
        }

        if (header.Experimental)
        {
            leading.Add($"{LeadingPrefix}**WARNING**: this option is experimental. Do not use in production.\n\n");
        }

        return leading;
    }

    private void RefuseUnknownProtocol(string protocols, int lineNumber, string fileName)
    {
        string? unknown = ManualPerlValues.Split(protocols, " +").FirstOrDefault(protocol => !KnownProtocols.Contains(protocol));
        if (unknown is null)
        {
            return;
        }

        context.Error.Append($"{fileName}:{lineNumber}:1:ERROR: unrecognized protocol: {unknown}\n");
        throw new ManualPageExit(2);
    }

    private int? Scope(ManualOptionHeader header, string fileName, List<string> description)
    {
        if (!ManualPerlValues.IsTrue(header.Scope))
        {
            return null;
        }

        string? refusal = !header.Category.Contains("global", StringComparison.Ordinal) ? "global scope option does not have global category"
            : header.Scope != "global" ? $"unrecognized scope: '{header.Scope}'"
            : null;
        if (refusal is not null)
        {
            context.Error.Append($"{fileName}:{header.LineNumber}:1:ERROR: {refusal}\n");
            return 2;
        }

        description.AddRange(manpage ? [] : ["\n"]);
        description.Add($"{Prefix}This option is global and does not need to be specified for each use of {ManualNroffText.Manpageify("next", manpage)}.\n");
        return null;
    }

    private int? Extra(ManualOptionHeader header, string fileName, List<string> extra)
    {
        if (MultiLines.TryGetValue(header.Multi, out string? template))
        {
            extra.Add(string.Format(CultureInfo.InvariantCulture, template, Prefix, ManualNroffText.Manpageify(header.Long, manpage), Disabler(header.Long)));
            return null;
        }

        if (header.Multi != "custom" && ManualPerlValues.IsTrue(header.Multi))
        {
            context.Error.Append($"{fileName}:{header.LineNumber}:1:ERROR: unrecognized Multi: '{header.Multi}'\n");
            return 2;
        }

        return null;
    }

    // The option that turns a boolean off again: --no-name, or --name for an option already named no-name.
    private string Disabler(string longName) =>
        (manpage ? "\\-\\-" : "--") + (longName.StartsWith("no-", StringComparison.Ordinal) ? longName[3..] : $"no-{longName}");

    private string SeeAlso(ManualOptionHeader header, string fileName)
    {
        foreach (string option in header.SeeAlso.Where(option => !context.Index.HasHelp(option)))
        {
            context.Error.Append($"{fileName}:{header.LineNumber}:1:WARN: see-also a non-existing option: {option}\n");
        }

        return JoinNames(header.SeeAlso);
    }

    private List<string> Foot(ManualOptionHeader header, string fileName)
    {
        List<string> foot = [];
        if (ManualPerlValues.IsTrue(header.Requires))
        {
            foot.Add($"For {ManualNroffText.Manpageify(header.Long, manpage)} to work, it requires that the underlying libcurl is built to support {header.Requires}.\n");
        }

        if (ManualPerlValues.IsTrue(header.Mutexed))
        {
            List<string> options = ManualPerlValues.Split(header.Mutexed, " ");
            foreach (string option in options.Where(option => !context.Index.HasHelp(option)))
            {
                context.Error.Append($"WARN: {fileName} mutexes a non-existing option: {option}\n");
            }

            foot.Add($"This option is mutually exclusive with {JoinNames(options)}.\n");
        }

        return foot;
    }

    // Names the options as the script lists them: "a", "a and b", "a, b and c".
    private string JoinNames(List<string> options)
    {
        List<string> names = [.. options.Select(option => ManualNroffText.Manpageify(option, manpage))];
        return names.Count < 2 ? string.Concat(names) : $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";
    }

    private int? Examples(ManualOptionHeader header, string fileName)
    {
        string? unknown = header.Examples.SelectMany(example => example.Split(' ')).Select(UnknownExampleOption).FirstOrDefault(option => option is not null);
        if (unknown is not null)
        {
            context.Error.Append($"{fileName}:{header.LineNumber}:1:ERROR: unknown option in example: {unknown}\n");
            return 2;
        }

        WriteExamples(header);
        return null;
    }

    private void WriteExamples(ManualOptionHeader header)
    {
        string plural = header.Examples.Count > 1 && ManualPerlValues.IsTrue(header.Examples[1]) ? "s" : "";
        List<string> examples = [.. header.Examples.Select(example => example.Replace("$URL", "https://example.com", StringComparison.Ordinal))];
        if (manpage)
        {
            context.Output.Append($"\nExample{plural}:\n.nf\n");
            context.Output.AppendJoin("", examples.Select(example => $"curl {example.Replace("\\", "\\\\", StringComparison.Ordinal)}\n"));
            context.Output.Append(".fi\n");
            return;
        }

        List<string> lines = [$"[0q]Example{plural}:\n"];
        examples.ForEach(example => AddAsciiExample(lines, example));
        layout.PrintDescription(false, 2, lines);
    }

    private string? UnknownExampleOption(string word)
    {
        Match shortOption = Regex.Match(word, "^-([^- ])", RegexOptions.CultureInvariant);
        if (shortOption.Success)
        {
            return context.Index.HasShort(shortOption.Groups[1].Value) ? null : $"-{shortOption.Groups[1].Value}";
        }

        Match longOption = Regex.Match(word, "^--([^ =]*)", RegexOptions.CultureInvariant);
        if (!longOption.Success)
        {
            return null;
        }

        string name = Regex.Replace(longOption.Groups[1].Value, "^expand-", "", RegexOptions.CultureInvariant);
        return context.Index.HasHelp(name) ? null : $"'--{name}'";
    }

    // Long ASCII examples wrap at the last space before the margin, or a colon, or else at the margin itself.
    private static void AddAsciiExample(List<string> lines, string example)
    {
        if (example.Length <= ExampleWidth)
        {
            lines.Add($"[0q] curl {example}\n");
            return;
        }

        string first = ManualNroffText.MaybeSpace(example[..ExampleWidth]);
        lines.Add($"[0q] curl {first}\\");
        string rest = example[first.Length..];
        while (rest.Length > 0)
        {
            string part = rest[..Math.Min(ExampleWidth, rest.Length)];
            part = rest.Length > ExampleWidth ? ManualNroffText.MaybeSpace(part) : part;
            rest = rest[part.Length..];
            if (part != "0")
            {
                lines.Add($"[0q]      {part}{(rest.Length > 0 ? "\\" : "")}");
            }
        }
    }

    private int WriteFoot(ManualOptionHeader header, List<string> foot, string seeAlso)
    {
        foot.Add(ManualNroffText.IsTooOld(header.Added!) ? "" : $"Added in {header.Added}. ");
        foot.Add($"See also {seeAlso}. ");
        string text = string.Concat(foot);
        context.Output.Append('\n');
        if (manpage)
        {
            context.Output.Append(text.TrimEnd(' ')).Append('\n');
            return 0;
        }

        layout.PrintDescription(false, 2, [$"[1]{text}"]);
        return 0;
    }
}
