namespace Curl.Cli;

/// <summary>
/// Reads a command line into <see cref="CommandLineOptions"/> the way curl 8.21.0 does,
/// driven by <see cref="CommandLineOptionTable"/>. Short options bundle (<c>-sS</c>) and a
/// value letter takes the rest of its bundle (<c>-ofile</c>) or else the next argument;
/// long options match exactly and accept <c>--name=value</c>; the first <c>--</c> ends
/// option parsing; every other argument is a URL. An empty URL argument is refused as
/// blank; an option's value, empty or not, is handed unchanged to the row's
/// <see cref="CommandLineOption.Apply"/>, which decides whether to refuse it. Parsing stops
/// at the first refusal.
/// </summary>
/// <remarks>
/// It does not implement <c>--no-</c> negation (<c>--no-silent</c> is refused as unknown),
/// <c>-K</c>/<c>--config</c>, <c>.curlrc</c>, <c>--variable</c> or <c>--next</c>, and it
/// neither validates URLs nor opens files. Checked against the local curl 8.21.0 on
/// 2026-09-26; options per <see href="https://curl.se/docs/manpage.html"/>.
/// </remarks>
public static class CommandLineParser
{
    private const string EndOfOptions = "--";

    /// <summary>Parses <paramref name="arguments"/>, the command line without the program name.</summary>
    /// <param name="arguments">The arguments; a <see langword="null"/> element reads as an empty argument.</param>
    /// <returns>
    /// The parsed options, or the first refusal met; every refusal carries
    /// <see cref="Curl.Protocol.Abstractions.CurlExitCode.FailedInit"/>. Never throws for a non-null list.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="arguments"/> is <see langword="null"/>.</exception>
    public static CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        CommandLineOptions options = new();
        ArgumentReader reader = new(arguments);
        while (reader.TryTakeNext(out string argument))
        {
            CommandLineRefusal? refusal = reader.OptionsEnded
                ? AddPositionalUrl(options, argument)
                : ParseArgument(options, argument, reader);
            if (refusal is not null)
            {
                return CommandLineParseResult.Refused(refusal);
            }
        }

        return CommandLineParseResult.Accepted(options);
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
            return CommandLineRefusal.UnknownOption(argument);
        }

        if (!option.TakesValue)
        {
            // curl accepts and ignores a value attached to a flag (--silent=x).
            return option.Apply(options, string.Empty, argument);
        }

        return hasAttachedValue
            ? option.Apply(options, nameAndValue[(equals + 1)..], argument)
            : ApplyNextArgument(options, option, argument, reader);
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
                    ? option.Apply(options, restOfBundle, argument)
                    : ApplyNextArgument(options, option, argument, reader);
            }

            // A flag's applier never refuses, so its result is not inspected.
            _ = option.Apply(options, string.Empty, argument);
        }

        return null;
    }

    private static CommandLineRefusal? ApplyNextArgument(CommandLineOptions options, CommandLineOption option, string argument, ArgumentReader reader) =>
        reader.TryTakeNext(out string value)
            ? option.Apply(options, value, argument)
            : CommandLineRefusal.RequiresParameter(argument);

    /// <summary>Walks the arguments in order and remembers whether <c>--</c> has ended option parsing.</summary>
    private sealed class ArgumentReader(IReadOnlyList<string> arguments)
    {
        private int next;

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
