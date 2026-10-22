using System.Text.RegularExpressions;

namespace Curl.Cli.ManualPages;

/// <summary>
/// Writes one option document's part of the page, as <c>single</c> in upstream's <c>scripts/managen</c> does for the
/// main page: its names, its description, the lines its <c>Scope</c> and <c>Multi</c> add, its examples and its footer
/// of requirements, exclusions, version and see-also names.
/// </summary>
/// <param name="context">The run.</param>
/// <param name="manpage">Whether the page is nroff.</param>
internal sealed class ManualOptionPage(ManualPageContext context, bool manpage)
{
    private const int ExampleWidth = 60;

    private static readonly HashSet<string> KnownProtocols = new(StringComparer.Ordinal)
    {
        "DNS", "FILE", "FTP", "FTPS", "GSS/kerberos", "HTTP", "HTTPS", "IMAP", "IPFS", "LDAP", "MQTT", "POP3", "SCP", "SFTP",
        "SMTP", "SSL", "TELNET", "TFTP", "TLS",
    };

    private readonly ManualAsciiLayout layout = new(context);

    private string Prefix => manpage ? "\n" : "[1]";

    /// <summary>Writes the option document <paramref name="fileName"/>.</summary>
    /// <param name="fileName">The document's name in the run's folder.</param>
    /// <returns>0, or the code the script returns for a document it refuses.</returns>
    public int Write(string fileName)
    {
        List<string> lines = context.ReadLines(context.PathOf(fileName), $"could not find {context.PathOf(fileName)}");
        (ManualOptionHeader header, int? failure) = ManualOptionHeader.Parse(context, fileName, lines);
        if (failure is { } code)
        {
            return code;
        }

        List<string> description = new ManualMarkdownRenderer(context, manpage, fileName, header.LineNumber).Render(lines.Skip(header.BodyStart));
        WriteNames(header);
        List<string> leading = Leading(header, fileName);
        List<string> extra = [];
        if ((Scope(header, fileName, description) ?? Extra(header, fileName, extra)) is { } refused)
        {
            return refused;
        }

        layout.PrintDescription(manpage, 2, [.. leading, .. description, .. extra]);
        string seeAlso = SeeAlso(header, fileName);
        List<string> foot = Foot(header, fileName);
        if (Examples(header, fileName) is { } badExample)
        {
            return badExample;
        }

        foot.Add(ManualNroffText.IsTooOld(header.Added!) ? "" : $"Added in {header.Added}. ");
        foot.Add($"See also {seeAlso}. ");
        WriteFoot(string.Concat(foot));
        return 0;
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
            foreach (string protocol in ManualPerlValues.Split(header.Protocols, " +").Where(protocol => !KnownProtocols.Contains(protocol)))
            {
                context.Error.Append($"{fileName}:{header.LineNumber}:1:ERROR: unrecognized protocol: {protocol}\n");
                throw new ManualPageExit(2);
            }

            leading.Add($"{(manpage ? "" : "[1]")}({header.Protocols}) ");
        }

        if (header.Experimental)
        {
            leading.Add($"{(manpage ? "" : "[1]")}**WARNING**: this option is experimental. Do not use in production.\n\n");
        }

        return leading;
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
        string name = ManualNroffText.Manpageify(header.Long, manpage);
        string reverse = header.Long.StartsWith("no-", StringComparison.Ordinal) ? header.Long[3..] : $"no-{header.Long}";
        string? line = header.Multi switch
        {
            "single" => $"{Prefix}If {name} is provided several times, the last set value is used.\n",
            "append" => $"{Prefix}{name} can be used several times in a command line.\n",
            "boolean" => $"{Prefix}Providing {name} multiple times has no extra effect.\nDisable it again with {(manpage ? "\\-\\-" : "--")}{reverse}.\n",
            "mutex" => $"{Prefix}Providing {name} multiple times has no extra effect.\n",
            "per-URL" => $"{Prefix}{name} is associated with a single URL. Use it once per URL when you use several URLs in a command line.\n",
            _ => null,
        };
        extra.AddRange(line is null ? [] : [line]);
        if (line is null && header.Multi != "custom" && ManualPerlValues.IsTrue(header.Multi))
        {
            context.Error.Append($"{fileName}:{header.LineNumber}:1:ERROR: unrecognized Multi: '{header.Multi}'\n");
            return 2;
        }

        return null;
    }

    private string SeeAlso(ManualOptionHeader header, string fileName)
    {
        int last = header.SeeAlso.Count > 2 ? header.SeeAlso.Count - 1 : 0;
        string names = "";
        for (int index = 0; index < header.SeeAlso.Count; index++)
        {
            string option = header.SeeAlso[index];
            if (!context.Index.HasHelp(option))
            {
                context.Error.Append($"{fileName}:{header.LineNumber}:1:WARN: see-also a non-existing option: {option}\n");
            }

            string separator = last != 0 && index < last ? "," : " and";
            names += (names.Length > 0 ? $"{separator} " : "") + ManualNroffText.Manpageify(option, manpage);
        }

        return names;
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
            string names = "";
            for (int index = 0; index < options.Count; index++)
            {
                if (!context.Index.HasHelp(options[index]))
                {
                    context.Error.Append($"WARN: {fileName} mutexes a non-existing option: {options[index]}\n");
                }

                names += (names.Length > 0 ? index == options.Count - 1 ? " and " : ", " : "") + ManualNroffText.Manpageify(options[index], manpage);
            }

            foot.Add($"This option is mutually exclusive with {names}.\n");
        }

        return foot;
    }

    private int? Examples(ManualOptionHeader header, string fileName)
    {
        foreach (string word in header.Examples.SelectMany(example => example.Split(' ')))
        {
            if (UnknownExampleOption(word) is { } unknown)
            {
                context.Error.Append($"{fileName}:{header.LineNumber}:1:ERROR: unknown option in example: {unknown}\n");
                return 2;
            }
        }

        string plural = header.Examples.Count > 1 && ManualPerlValues.IsTrue(header.Examples[1]) ? "s" : "";
        List<string> examples = [.. header.Examples.Select(example => example.Replace("$URL", "https://example.com", StringComparison.Ordinal))];
        if (manpage)
        {
            context.Output.Append($"\nExample{plural}:\n.nf\n");
            context.Output.AppendJoin("", examples.Select(example => $"curl {example.Replace("\\", "\\\\", StringComparison.Ordinal)}\n"));
            context.Output.Append(".fi\n");
            return null;
        }

        List<string> lines = [$"[0q]Example{plural}:\n"];
        examples.ForEach(example => AddAsciiExample(lines, example));
        layout.PrintDescription(false, 2, lines);
        return null;
    }

    private string? UnknownExampleOption(string word)
    {
        if (Regex.Match(word, "^-([^- ])", RegexOptions.CultureInvariant) is { Success: true } shortOption)
        {
            return context.Index.HasShort(shortOption.Groups[1].Value) ? null : $"-{shortOption.Groups[1].Value}";
        }

        if (Regex.Match(word, "^--([^ =]*)", RegexOptions.CultureInvariant) is { Success: true } longOption)
        {
            string name = Regex.Replace(longOption.Groups[1].Value, "^expand-", "", RegexOptions.CultureInvariant);
            return context.Index.HasHelp(name) ? null : $"'--{name}'";
        }

        return null;
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

    private void WriteFoot(string foot)
    {
        context.Output.Append('\n');
        if (manpage)
        {
            context.Output.Append(foot.TrimEnd(' ')).Append('\n');
            return;
        }

        layout.PrintDescription(false, 2, [$"[1]{foot}"]);
    }
}
