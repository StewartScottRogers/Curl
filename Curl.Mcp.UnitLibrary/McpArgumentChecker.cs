using System.Collections.Frozen;
using Curl.Cli;

namespace Curl.Mcp;

/// <summary>
/// Checks a whole <c>curl</c> argument array against <c>CURL_MCP_ROOT</c> before the <c>curl</c>
/// tool runs it (ADR-0548, ADR-0564). It reads the arguments the way <c>Curl.Cli</c>'s parser does,
/// from <see cref="CommandLineOptionTable" />'s rows but applying nothing: bundled short letters
/// (<c>-sSo file</c>), a value attached to its letter (<c>-ofile</c>) or its long name
/// (<c>--output=file</c>), <c>--no-</c> and <c>--expand-</c> spellings, <c>--next</c> groups, the
/// first <c>--</c> ending options, and every other argument as a URL. Each option's path use comes
/// from <see cref="McpOptionPathTable" />; paths inside values (<c>-d @file</c>, <c>-F name=@file</c>,
/// <c>-w %output{file}</c>, <c>file://</c> URLs) are found as curl reads them. A <c>-K</c> config file
/// inside the root is read and its lines checked the same way, at most
/// <see cref="MaximumConfigDepth" /> files deep, and a <c>-w @file</c> template inside the root is read
/// and its <c>%output{file}</c> directives checked (ADR-0600). It never writes, and reads no file but
/// a <c>-K</c> config or a <c>-w @file</c> template.
/// </summary>
public sealed class McpArgumentChecker
{
    /// <summary>How many <c>-K</c> files deep a check reads before it refuses, as a config that includes itself would nest forever.</summary>
    internal const int MaximumConfigDepth = 8;

    private const string EndOfOptions = "--";

    /// <summary>The option table's rows <see cref="McpOptionPathTable" /> classifies; any other is unknown, and refused.</summary>
    private static readonly CommandLineOption[] ClassifiedRows =
        [.. CommandLineOptionTable.Rows.Where(row => McpOptionPathTable.Classifications.ContainsKey(row.LongName))];

    private static readonly FrozenDictionary<string, CommandLineOption> RowsByLongName =
        ClassifiedRows.ToFrozenDictionary(row => row.LongName, StringComparer.Ordinal);

    private static readonly FrozenDictionary<char, CommandLineOption> RowsByShortName =
        ClassifiedRows.Where(row => row.ShortName.HasValue).ToFrozenDictionary(row => row.ShortName!.Value);

    private readonly McpConfinementRoot root;

    /// <summary>Creates the checker for <paramref name="root" />.</summary>
    /// <param name="root">The folder every path must lie in; <see cref="McpConfinementRoot.NotConfigured" /> refuses every path.</param>
    public McpArgumentChecker(McpConfinementRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);
        this.root = root;
    }

    /// <summary>
    /// Checks <paramref name="arguments" />, the command line without the program name, and answers
    /// with the first option whose path lies outside the root or cannot be checked, or
    /// <see cref="McpArgumentCheck.Allowed" />. An unknown option is refused, as are a <c>-K</c> file
    /// that cannot be read or that nests too deep, <c>-K -</c>, and <c>--proto-default file</c>.
    /// </summary>
    /// <param name="arguments">The arguments; a <see langword="null" /> element reads as an empty argument.</param>
    /// <returns>The verdict; a refusal is a result, never an exception.</returns>
    public McpArgumentCheck Check(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return CheckArguments(new ArgumentCursor(arguments), 0);
    }

    /// <summary>Checks every argument the cursor still holds; <paramref name="configDepth" /> is how many <c>-K</c> files deep this array is (0 for the tool's own).</summary>
    private McpArgumentCheck CheckArguments(ArgumentCursor cursor, int configDepth)
    {
        while (cursor.TryTake(out string argument))
        {
            McpArgumentCheck check = cursor.OptionsEnded
                ? CheckPaths(McpArgumentCheck.UrlArgument, McpOptionValuePaths.UrlPaths(argument))
                : CheckArgument(argument, cursor, configDepth);
            if (!check.IsAllowed)
            {
                return check;
            }
        }

        return McpArgumentCheck.Allowed;
    }

    /// <summary>Checks one argument before options have ended: <c>--</c> ends them, <c>--name</c> is a long option, <c>-letters</c> a bundle, anything else (a lone <c>-</c> included) a URL.</summary>
    private McpArgumentCheck CheckArgument(string argument, ArgumentCursor cursor, int configDepth)
    {
        if (argument == EndOfOptions)
        {
            cursor.OptionsEnded = true;
            return McpArgumentCheck.Allowed;
        }

        if (argument.StartsWith(EndOfOptions, StringComparison.Ordinal))
        {
            return CheckLong(argument, cursor, configDepth);
        }

        return argument.Length > 1 && argument[0] == '-'
            ? CheckShortBundle(argument, cursor, configDepth)
            : CheckPaths(McpArgumentCheck.UrlArgument, McpOptionValuePaths.UrlPaths(argument));
    }

    private McpArgumentCheck CheckLong(string argument, ArgumentCursor cursor, int configDepth)
    {
        string nameAndValue = argument[EndOfOptions.Length..];
        int equals = nameAndValue.IndexOf('=', StringComparison.Ordinal);
        string longName = equals >= 0 ? nameAndValue[..equals] : nameAndValue;
        string? attachedValue = equals >= 0 ? nameAndValue[(equals + 1)..] : null;
        string written = EndOfOptions + longName;
        if (RowsByLongName.TryGetValue(longName, out CommandLineOption? row))
        {
            return CheckLongRow(written, row, attachedValue, cursor, configDepth, expanded: false);
        }

        return CheckUnlistedLong(written, longName, attachedValue, cursor, configDepth);
    }

    /// <summary>
    /// A long name with no row is <c>--expand-&lt;name&gt;</c>, whose value is expanded first, or
    /// <c>--no-&lt;name&gt;</c>, which takes no value; any other is unknown.
    /// </summary>
    private McpArgumentCheck CheckUnlistedLong(string written, string longName, string? attachedValue, ArgumentCursor cursor, int configDepth)
    {
        const string ExpansionPrefix = "expand-";
        const string NegationPrefix = "no-";
        if (longName.StartsWith(ExpansionPrefix, StringComparison.Ordinal)
            && RowsByLongName.TryGetValue(longName[ExpansionPrefix.Length..], out CommandLineOption? expandedRow))
        {
            return CheckLongRow(written, expandedRow, attachedValue, cursor, configDepth, expanded: true);
        }

        return longName.StartsWith(NegationPrefix, StringComparison.Ordinal) && RowsByLongName.ContainsKey(longName[NegationPrefix.Length..])
            ? McpArgumentCheck.Allowed
            : Unknown(written);
    }

    /// <summary>
    /// Checks a long option that has a row: a subject option (<c>--help</c>) takes its subject and names
    /// no path, a flag goes to <see cref="CheckFlag" />, and a value option takes the attached value
    /// (<c>--name=value</c>) or else the next argument. A missing value is allowed: curl itself rejects it.
    /// </summary>
    private McpArgumentCheck CheckLongRow(string written, CommandLineOption row, string? attachedValue, ArgumentCursor cursor, int configDepth, bool expanded)
    {
        if (row.TakesSubject)
        {
            cursor.TakeUnless(attachedValue is not null);
            return McpArgumentCheck.Allowed;
        }

        if (!row.TakesValue)
        {
            return CheckFlag(written, row);
        }

        string? value = attachedValue ?? cursor.TakeOrNull();
        return value is null ? McpArgumentCheck.Allowed : CheckValue(written, row.LongName, value, configDepth, expanded);
    }

    private McpArgumentCheck CheckShortBundle(string argument, ArgumentCursor cursor, int configDepth)
    {
        for (int letter = 1; letter < argument.Length; letter++)
        {
            McpArgumentCheck? bundleEnd = CheckBundleLetter(argument, letter, cursor, configDepth);
            if (bundleEnd is not null)
            {
                return bundleEnd;
            }
        }

        return McpArgumentCheck.Allowed;
    }

    /// <summary>
    /// Checks the letter at <paramref name="letter" /> of a bundle; answers <see langword="null" /> when
    /// the bundle goes on, or the bundle's verdict when it ends there: at an unknown letter, a letter
    /// that takes a value or subject, a refused flag, or a flag that ends a bundle (<c>-:</c>).
    /// </summary>
    private McpArgumentCheck? CheckBundleLetter(string argument, int letter, ArgumentCursor cursor, int configDepth)
    {
        string written = "-" + argument[letter];
        if (!RowsByShortName.TryGetValue(argument[letter], out CommandLineOption? row))
        {
            return Unknown(written);
        }

        if (row.TakesSubject || row.TakesValue)
        {
            return CheckShortValue(written, row, argument[(letter + 1)..], cursor, configDepth);
        }

        McpArgumentCheck flag = CheckFlag(written, row);
        return !flag.IsAllowed || row.EndsBundle ? flag : null;
    }

    /// <summary>
    /// A value letter takes the rest of its bundle or else the next argument; a subject letter
    /// (<c>-h</c>) takes the next argument only as the bundle's last letter, and names no path.
    /// </summary>
    private McpArgumentCheck CheckShortValue(string written, CommandLineOption row, string restOfBundle, ArgumentCursor cursor, int configDepth)
    {
        if (row.TakesSubject)
        {
            cursor.TakeUnless(restOfBundle.Length > 0);
            return McpArgumentCheck.Allowed;
        }

        string? value = restOfBundle.Length > 0 ? restOfBundle : cursor.TakeOrNull();
        return value is null ? McpArgumentCheck.Allowed : CheckValue(written, row.LongName, value, configDepth, expanded: false);
    }

    /// <summary>
    /// A flag names no path, except one that reads a file it implies rather than names
    /// (<c>-n</c> reads the home folder's <c>.netrc</c>), which lies outside the root's reach.
    /// <c>-O</c> and <c>-J</c> write into the working folder, which the <c>curl</c> tool sets to the root.
    /// </summary>
    private static McpArgumentCheck CheckFlag(string written, CommandLineOption row)
    {
        return McpOptionPathTable.Classifications[row.LongName] == McpOptionPathUse.ReadsFile
            ? McpArgumentCheck.Refused(written, written, $"{written} reads a file it implies rather than names, outside {McpConfinementRoot.VariableName}.")
            : McpArgumentCheck.Allowed;
    }

    /// <summary>
    /// Checks the value of an option. <paramref name="expanded" /> is set for <c>--expand-</c>, whose
    /// <c>{{variable}}</c> references curl substitutes before use, so a path holding one is refused.
    /// </summary>
    private McpArgumentCheck CheckValue(string written, string longName, string value, int configDepth, bool expanded)
    {
        McpOptionPathUse use = McpOptionPathTable.Classifications[longName];
        if (use == McpOptionPathUse.None)
        {
            return CheckPathlessValue(written, longName, value, expanded);
        }

        if (expanded && value.Contains("{{", StringComparison.Ordinal))
        {
            return McpArgumentCheck.Refused(written, value, $"{written} takes a value with {{{{variables}}}}, whose expanded path cannot be checked before curl runs.");
        }

        return CheckPathValue(written, longName, use, value, configDepth);
    }

    /// <summary>A <c>-K</c> file and a <c>-w @file</c> template are read and checked; any other value's paths are checked as named.</summary>
    private McpArgumentCheck CheckPathValue(string written, string longName, McpOptionPathUse use, string value, int configDepth)
    {
        return longName switch
        {
            "config" => CheckConfig(written, value, configDepth),
            "write-out" when value.StartsWith('@') => CheckWriteOutTemplate(written, value[1..]),
            _ => CheckPaths(written, McpOptionValuePaths.PathsIn(longName, use, value)),
        };
    }

    /// <summary>
    /// A <c>-w @file</c> template must lie inside the root and be readable; it is then read and its
    /// <c>%output{file}</c> directives checked as an inline template's are (ADR-0600), a refusal naming the template.
    /// <c>-w @-</c> reads the template from standard input, which the <c>curl</c> tool's caller supplies unchecked.
    /// </summary>
    private McpArgumentCheck CheckWriteOutTemplate(string written, string file)
    {
        if (file == "-")
        {
            return McpArgumentCheck.Refused(written, "@-", $"{written} @- reads its template from standard input, which cannot be checked before curl runs.");
        }

        McpPathCheck check = root.Check(file);
        if (!check.IsAllowed)
        {
            return McpArgumentCheck.Refused(written, file, $"{written} {check.Reason}");
        }

        string? template = ReadCheckedFile(check.ResolvedPath!, out string failure);
        if (template is null)
        {
            return McpArgumentCheck.Refused(written, file, $"{written} '{file}' cannot be read: {failure}");
        }

        McpArgumentCheck paths = CheckPaths(written, McpOptionValuePaths.TemplatePaths(template));
        return paths.IsAllowed ? paths : paths with { Reason = $"In write-out template '{file}': {paths.Reason}" };
    }

    /// <summary>
    /// A value naming no path is allowed, except <c>--proto-default file</c>, which would read every
    /// URL argument without a scheme as a local file.
    /// </summary>
    private static McpArgumentCheck CheckPathlessValue(string written, string longName, string value, bool expanded)
    {
        bool defaultsToFile = longName == "proto-default"
            && (value.Equals("file", StringComparison.OrdinalIgnoreCase) || (expanded && value.Contains("{{", StringComparison.Ordinal)));
        return defaultsToFile
            ? McpArgumentCheck.Refused(written, value, $"{written} {value} would read URL arguments without a scheme as local files, which are not checked.")
            : McpArgumentCheck.Allowed;
    }

    /// <summary>Refuses on the first path that is uncheckable or outside the root; the refusal names the option as <paramref name="written" />.</summary>
    private McpArgumentCheck CheckPaths(string written, IReadOnlyList<McpValuePath> paths)
    {
        foreach (McpValuePath path in paths)
        {
            if (path.Uncheckable is not null)
            {
                return McpArgumentCheck.Refused(written, path.Path, $"{written} '{path.Path}' {path.Uncheckable}");
            }

            McpPathCheck check = root.Check(path.Path);
            if (!check.IsAllowed)
            {
                return McpArgumentCheck.Refused(written, path.Path, $"{written} {check.Reason}");
            }
        }

        return McpArgumentCheck.Allowed;
    }

    /// <summary>
    /// A <c>-K</c> file must lie inside the root and be readable; its lines are then checked as
    /// options, a refusal naming the file and line. <c>-K -</c> reads standard input, which is not checked.
    /// </summary>
    private McpArgumentCheck CheckConfig(string written, string value, int configDepth)
    {
        if (value == "-")
        {
            return McpArgumentCheck.Refused(written, value, $"{written} - reads a config file from standard input, which cannot be checked before curl runs.");
        }

        McpPathCheck check = root.Check(value);
        if (!check.IsAllowed)
        {
            return McpArgumentCheck.Refused(written, value, $"{written} {check.Reason}");
        }

        if (configDepth == MaximumConfigDepth)
        {
            return McpArgumentCheck.Refused(written, value, $"{written} '{value}' nests config files more than {MaximumConfigDepth} deep, as a config that includes itself does.");
        }

        string? text = ReadCheckedFile(check.ResolvedPath!, out string failure);
        return text is null
            ? McpArgumentCheck.Refused(written, value, $"{written} '{value}' cannot be read: {failure}")
            : CheckConfigLines(value, McpConfigFileSyntax.ReadLines(text), configDepth + 1);
    }

    /// <summary>Reads a file the root already allowed; <see langword="null" /> with the reason in <paramref name="failure" /> when it cannot be read.</summary>
    private static string? ReadCheckedFile(string resolvedPath, out string failure)
    {
        failure = string.Empty;
        try
        {
            return File.ReadAllText(resolvedPath);
        }
        catch (Exception exception) when (McpConfinementRoot.IsResolutionFailure(exception))
        {
            failure = exception.Message;
            return null;
        }
    }

    /// <summary>Checks each line of a <c>-K</c> file in order; a refusal's reason is prefixed with the file and the 1-based line.</summary>
    private McpArgumentCheck CheckConfigLines(string file, IReadOnlyList<KeyValuePair<string, string?>> lines, int configDepth)
    {
        for (int line = 0; line < lines.Count; line++)
        {
            McpArgumentCheck check = CheckConfigLine(lines[line].Key, lines[line].Value, configDepth);
            if (!check.IsAllowed)
            {
                return check with { Reason = $"In config file '{file}', line {line + 1}: {check.Reason}" };
            }
        }

        return McpArgumentCheck.Allowed;
    }

    /// <summary>
    /// A config line's option starting with one <c>-</c> is a bundle of short letters; any other is a
    /// long name with or without its <c>--</c>. The line's parameter is the one argument that may follow.
    /// </summary>
    private McpArgumentCheck CheckConfigLine(string option, string? parameter, int configDepth)
    {
        ArgumentCursor cursor = new(parameter is null ? [] : [parameter]);
        if (option.StartsWith(EndOfOptions, StringComparison.Ordinal))
        {
            return CheckLong(option, cursor, configDepth);
        }

        return option.StartsWith('-') ? CheckShortBundle(option, cursor, configDepth) : CheckLong(EndOfOptions + option, cursor, configDepth);
    }

    /// <summary>The refusal for an option with no row in the classified table; failing closed means a new curl option is refused until classified.</summary>
    private static McpArgumentCheck Unknown(string written) =>
        McpArgumentCheck.Refused(written, written, $"{written} is not an option this server knows, so it cannot tell whether it uses a local path.");

    /// <summary>Walks the arguments in order and remembers whether <c>--</c> has ended the options.</summary>
    private sealed class ArgumentCursor(IReadOnlyList<string> arguments)
    {
        private int next;

        /// <summary>Whether a <c>--</c> argument has been seen, so every later argument is a URL.</summary>
        public bool OptionsEnded { get; set; }

        /// <summary>Takes the next argument; <see langword="false" /> when none is left.</summary>
        public bool TryTake(out string argument)
        {
            string? taken = TakeOrNull();
            argument = taken ?? string.Empty;
            return taken is not null;
        }

        /// <summary>Takes the next argument, a <see langword="null" /> element as an empty one; <see langword="null" /> when none is left.</summary>
        public string? TakeOrNull() => next < arguments.Count ? arguments[next++] ?? string.Empty : null;

        /// <summary>Skips the next argument, the option's separate value or subject, unless the option carried it attached.</summary>
        public void TakeUnless(bool valueAttached)
        {
            if (!valueAttached)
            {
                TakeOrNull();
            }
        }
    }
}
