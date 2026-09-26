namespace Curl.Cli;

/// <summary>
/// Reads a command line into <see cref="CommandLineOptions"/> the way curl 8.21.0 does,
/// driven by <see cref="CommandLineOptionTable"/>. Short options bundle (<c>-sS</c>) and a
/// value letter takes the rest of its bundle (<c>-ofile</c>) or else the next argument;
/// long options match exactly and accept <c>--name=value</c>; <c>--no-&lt;name&gt;</c> turns off
/// a row built with <see cref="CommandLineOption.NegatableFlag"/> and is refused for any other
/// row (see <see cref="CommandLineOptionTable"/>); the first <c>--</c> ends
/// option parsing; every other argument is a URL. An empty URL argument is refused as
/// blank; an option's value, empty or not, is handed unchanged to the row's
/// <see cref="CommandLineOption.Apply"/>, which decides whether to refuse it. Parsing stops
/// at the first refusal. Warning lines a row adds while applying its value (a file name that
/// looks like a flag) are carried on the result, accepted or refused. A command line that is
/// read without refusal but names no URL is
/// refused with <see cref="CommandLineRefusal.NoUrlSpecified"/>; an empty command line is
/// accepted, because curl answers it differently. The one file-system question it asks is
/// whether the <c>--cacert</c> path exists, through a check <see cref="Parse(IReadOnlyList{string}, Func{string, bool})"/>
/// takes as a parameter.
/// </summary>
/// <remarks>
/// It does not implement <c>-K</c>/<c>--config</c>, <c>.curlrc</c>, <c>--variable</c> or <c>--next</c>, and it
/// neither validates URLs nor opens files. Checked against the local curl 8.21.0 on
/// 2026-09-26; options per <see href="https://curl.se/docs/manpage.html"/>.
/// </remarks>
public static class CommandLineParser
{
    private const string EndOfOptions = "--";

    private const string NegationPrefix = "no-";

    /// <summary>Parses <paramref name="arguments"/>, the command line without the program name.</summary>
    /// <param name="arguments">The arguments; a <see langword="null"/> element reads as an empty argument.</param>
    /// <returns>
    /// The parsed options, or the first refusal met; every refusal carries
    /// <see cref="Curl.Protocol.Abstractions.CurlExitCode.FailedInit"/>. Never throws for a non-null list.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="arguments"/> is <see langword="null"/>.</exception>
    public static CommandLineParseResult Parse(IReadOnlyList<string> arguments) =>
        Parse(arguments, PathExistsOnDisk);

    /// <summary>
    /// Parses <paramref name="arguments"/>, asking <paramref name="pathExists"/> whether a path an
    /// option names exists, where curl checks while parsing (<c>--cacert</c>).
    /// </summary>
    /// <param name="arguments">The arguments; a <see langword="null"/> element reads as an empty argument.</param>
    /// <param name="pathExists">Reports whether a file or directory exists at a path.</param>
    /// <returns>
    /// The parsed options, or the first refusal met; every refusal carries
    /// <see cref="Curl.Protocol.Abstractions.CurlExitCode.FailedInit"/>. Never throws for non-null arguments.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="arguments"/> or <paramref name="pathExists"/> is <see langword="null"/>.</exception>
    public static CommandLineParseResult Parse(IReadOnlyList<string> arguments, Func<string, bool> pathExists)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(pathExists);

        CommandLineOptions options = new();
        ArgumentReader reader = new(arguments, pathExists);
        while (reader.TryTakeNext(out string argument))
        {
            CommandLineRefusal? refusal = reader.OptionsEnded
                ? AddPositionalUrl(options, argument)
                : ParseArgument(options, argument, reader);
            if (refusal is not null)
            {
                return CommandLineParseResult.Refused(refusal, options.WarningLines);
            }
        }

        return options.Urls.Count == 0 && arguments.Count > 0
            ? CommandLineParseResult.Refused(CommandLineRefusal.NoUrlSpecified(), options.WarningLines)
            : CommandLineParseResult.Accepted(options);
    }

    private static CommandLineRefusal? ParseArgument(CommandLineOptions options, string argument, ArgumentReader reader)
    {
        if (argument == EndOfOptions)
        {
            reader.OptionsEnded = true;
            return null;
        }

        if (argument.StartsWith(EndOfOptions, StringComparison.Ordinal))
        {
            return ParseLong(options, argument, reader);
        }

        if (argument.StartsWith('-'))
        {
            return ParseShortBundle(options, argument, reader);
        }

        return AddPositionalUrl(options, argument);
    }

    private static CommandLineRefusal? AddPositionalUrl(CommandLineOptions options, string argument)
    {
        if (argument.Length == 0)
        {
            return CommandLineRefusal.BlankArgument(string.Empty);
        }

        options.AddUrl(argument);
        return null;
    }

    private static CommandLineRefusal? ParseLong(CommandLineOptions options, string argument, ArgumentReader reader)
    {
        string nameAndValue = argument[EndOfOptions.Length..];
        int equals = nameAndValue.IndexOf('=', StringComparison.Ordinal);
        bool hasAttachedValue = equals >= 0;
        string longName = hasAttachedValue ? nameAndValue[..equals] : nameAndValue;
        if (!CommandLineOptionTable.TryFindLong(longName, out CommandLineOption? option))
        {
            return ParseNegatedLong(options, argument, longName);
        }

        if (!option.TakesValue)
        {
            // curl accepts and ignores a value attached to a flag (--silent=x).
            return option.Apply(options, string.Empty, argument, reader.PathExists);
        }

        return hasAttachedValue
            ? option.Apply(options, nameAndValue[(equals + 1)..], argument, reader.PathExists)
            : ApplyNextArgument(options, option, argument, reader);
    }

    /// <summary>
    /// Reads a long name that is not in the table as <c>--no-&lt;name&gt;</c>: it turns off a
    /// negatable flag (any attached value ignored), is refused as not reversible for any other row,
    /// and is unknown when there is no <c>no-</c> or no row after it. Checked before a value is
    /// taken, so <c>--no-output</c> as the last argument is refused as not reversible.
    /// </summary>
    private static CommandLineRefusal? ParseNegatedLong(CommandLineOptions options, string argument, string longName)
    {
        if (!longName.StartsWith(NegationPrefix, StringComparison.Ordinal)
            || !CommandLineOptionTable.TryFindLong(longName[NegationPrefix.Length..], out CommandLineOption? option))
        {
            return CommandLineRefusal.UnknownOption(argument);
        }

        if (option.Negate is null)
        {
            return CommandLineRefusal.CannotBeReversed(argument);
        }

        option.Negate(options);
        return null;
    }

    private static CommandLineRefusal? ParseShortBundle(CommandLineOptions options, string argument, ArgumentReader reader)
    {
        if (argument.Length == 1)
        {
            return CommandLineRefusal.UnknownOption(argument);
        }

        for (int letter = 1; letter < argument.Length; letter++)
        {
            if (!CommandLineOptionTable.TryFindShort(argument[letter], out CommandLineOption? option))
            {
                return CommandLineRefusal.UnknownOption(argument);
            }

            if (option.TakesValue)
            {
                string restOfBundle = argument[(letter + 1)..];
                return restOfBundle.Length > 0
                    ? option.Apply(options, restOfBundle, argument, reader.PathExists)
                    : ApplyNextArgument(options, option, argument, reader);
            }

            // A flag's applier never refuses, so its result is not inspected.
            _ = option.Apply(options, string.Empty, argument, reader.PathExists);
        }

        return null;
    }

    private static CommandLineRefusal? ApplyNextArgument(CommandLineOptions options, CommandLineOption option, string argument, ArgumentReader reader) =>
        reader.TryTakeNext(out string value)
            ? option.Apply(options, value, argument, reader.PathExists)
            : CommandLineRefusal.RequiresParameter(argument);

    /// <summary>Reports whether a file or a directory exists at <paramref name="path"/>, as curl's check does.</summary>
    private static bool PathExistsOnDisk(string path) => Path.Exists(path);

    /// <summary>
    /// Walks the arguments in order, remembers whether <c>--</c> has ended option parsing, and
    /// carries the path-existence check the appliers are given.
    /// </summary>
    private sealed class ArgumentReader(IReadOnlyList<string> arguments, Func<string, bool> pathExists)
    {
        private int next;

        public Func<string, bool> PathExists => pathExists;

        public bool OptionsEnded { get; set; }

        public bool TryTakeNext(out string argument)
        {
            if (next == arguments.Count)
            {
                argument = string.Empty;
                return false;
            }

            argument = arguments[next++] ?? string.Empty;
            return true;
        }
    }
}
