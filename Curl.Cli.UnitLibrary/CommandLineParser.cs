namespace Curl.Cli;

/// <summary>
/// Reads a command line into <see cref="CommandLineOptions"/> the way curl 8.21.0 does,
/// driven by <see cref="CommandLineOptionTable"/>. Short options bundle (<c>-sS</c>) and a
/// value letter takes the rest of its bundle (<c>-ofile</c>) or else the next argument;
/// long options match exactly and accept <c>--name=value</c>; <c>--no-&lt;name&gt;</c> turns off
/// a row built with <see cref="CommandLineOption.NegatableFlag"/> or <see cref="CommandLineOption.NegatableFlagThatCanRefuse"/> and is refused for any other
/// row (see <see cref="CommandLineOptionTable"/>); the first <c>--</c> ends
/// option parsing; every other argument is a URL. An empty URL argument is refused as
/// blank; an option's value, empty or not, is handed unchanged to the row's
/// <see cref="CommandLineOption.Apply"/>, which decides whether to refuse it. Parsing stops
/// at the first refusal. Warning lines a row adds while applying its value (a file name that
/// looks like a flag) are carried on the result, accepted or refused. A command line that is
/// read without refusal but names no URL is
/// refused with <see cref="CommandLineRefusal.NoUrlSpecified"/>; an empty command line is
/// refused with <see cref="CommandLineRefusal.EmptyCommandLine"/>, the try-help line alone. The one file-system question it asks is
/// whether the <c>--cacert</c> path exists, through a check <see cref="Parse(IReadOnlyList{string}, Func{string, bool}, IPasswordPrompt, IDataFileReader)"/>
/// takes as a parameter. When the whole command line is read without refusal and the last
/// <c>-u</c> / <c>--user</c> or <c>-U</c> / <c>--proxy-user</c> names a user with no colon, it asks the
/// injected <see cref="IPasswordPrompt"/> for the password, host first, before the no-URL check, as curl 8.21.0 does
/// (<c>curl -u bob</c> prompts, then reports no URL; <c>curl -u bob --bogus</c> never prompts).
/// A <c>-d</c> / <c>--data</c> value starting with <c>@</c> is read, while parsing, through the
/// injected <see cref="IDataFileReader"/>: the file it names, or standard input for <c>@-</c>.
/// A <c>-F</c> / <c>--form</c> value is read into form parts by <see cref="MultipartFormField"/>,
/// and a command line that asks for a form and a <c>-d</c> body both is refused once read, with
/// <see cref="CommandLineRefusal.FormAndDataBoth"/>.
/// A <c>-K</c> / <c>--config</c> file is read through the same reader and its lines applied in
/// place of the option (see <see cref="ConfigFileApplier"/>). Only the overload taking a
/// <see cref="DefaultConfigFileSearch"/> reads the default config file (<c>.curlrc</c>) first; the others
/// never do, so what they return does not depend on the machine they run on, except that a
/// <c>--variable %name</c> reads this process's environment (ADR-0064). An option given as
/// <c>--expand-&lt;name&gt;</c> has its value expanded by <see cref="VariableExpansion"/> first.
/// <c>-V</c> / <c>--version</c> on the command line ends parsing where it stands, even inside a bundle,
/// with <see cref="CommandLineParseResult.InformationRequested(CommandLineOptions)"/>: the arguments after it
/// are neither read nor refused, no password is asked for and no URL is needed, as in curl 8.21.0
/// (<c>curl -V --bogus</c> prints the version; <c>curl --bogus -V</c> is refused). <c>-M</c> / <c>--manual</c>
/// and <c>-h</c> / <c>--help</c> end it the same way, <c>--help</c> first reading its subject (see
/// <see cref="CommandLineOption.Subject"/>), with <see cref="CommandLineParseResult.InformationRequested(CommandLineOptions)"/>.
/// <c>-:</c> / <c>--next</c>, on the command line or as a <c>next</c> line of a config file, ends one
/// option group and starts the next (see <see cref="CommandLineOptions.StartNextGroup"/>): every
/// argument and config file line is read into the group being filled in, the options
/// <see cref="CommandLineOptionTable.GlobalOptionLongNames"/> lists are shared by every group, and
/// <see cref="CommandLineParseResult.Groups"/> returns the groups in order.
/// </summary>
/// <remarks>
/// It neither validates URLs nor opens files itself. Checked against the local curl 8.21.0 on
/// 2026-09-26; options per <see href="https://curl.se/docs/manpage.html"/>.
/// </remarks>
public static class CommandLineParser
{
    private const string EndOfOptions = "--";

    private const string NegationPrefix = "no-";

    private const string ExpansionPrefix = "expand-";

    /// <summary>Parses <paramref name="arguments"/>, the command line without the program name.</summary>
    /// <param name="arguments">The arguments; a <see langword="null"/> element reads as an empty argument.</param>
    /// <returns>
    /// The parsed options, or the first refusal met; every refusal carries
    /// <see cref="Curl.Protocol.Abstractions.CurlExitCode.FailedInit"/>, except a <c>-d @file</c> that
    /// cannot be read, which carries <see cref="Curl.Protocol.Abstractions.CurlExitCode.ReadError"/>.
    /// Never throws for a non-null list, unless reading standard input for <c>-d @-</c> fails.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="arguments"/> is <see langword="null"/>.</exception>
    public static CommandLineParseResult Parse(IReadOnlyList<string> arguments) =>
        Parse(arguments, PathExistsOnDisk, ConsolePasswordPrompt.ForProcessConsole, DiskDataFileReader.ForProcess);

    /// <summary>
    /// Parses <paramref name="arguments"/>, asking <paramref name="pathExists"/> whether a path an
    /// option names exists, where curl checks while parsing (<c>--cacert</c>).
    /// </summary>
    /// <param name="arguments">The arguments; a <see langword="null"/> element reads as an empty argument.</param>
    /// <param name="pathExists">Reports whether a file or directory exists at a path.</param>
    /// <returns>
    /// The parsed options, or the first refusal met; every refusal carries
    /// <see cref="Curl.Protocol.Abstractions.CurlExitCode.FailedInit"/>, except a <c>-d @file</c> that
    /// cannot be read, which carries <see cref="Curl.Protocol.Abstractions.CurlExitCode.ReadError"/>.
    /// Never throws for non-null arguments, unless reading standard input for <c>-d @-</c> fails.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="arguments"/> or <paramref name="pathExists"/> is <see langword="null"/>.</exception>
    public static CommandLineParseResult Parse(IReadOnlyList<string> arguments, Func<string, bool> pathExists) =>
        Parse(arguments, pathExists, ConsolePasswordPrompt.ForProcessConsole, DiskDataFileReader.ForProcess);

    /// <summary>
    /// Parses <paramref name="arguments"/>, asking <paramref name="pathExists"/> whether a path an
    /// option names exists (<c>--cacert</c>), <paramref name="passwordPrompt"/> for the password
    /// when <c>-u</c> / <c>--user</c> names a user with no colon, and <paramref name="dataFileReader"/>
    /// for the bytes of a <c>-d @file</c> or <c>-d @-</c>.
    /// </summary>
    /// <param name="arguments">The arguments; a <see langword="null"/> element reads as an empty argument.</param>
    /// <param name="pathExists">Reports whether a file or directory exists at a path.</param>
    /// <param name="passwordPrompt">Asks for the password of a <c>-u</c> user given without one; called at most once.</param>
    /// <param name="dataFileReader">Reads the file, or standard input, a <c>-d</c> / <c>--data</c> value starting with <c>@</c> or a <c>-K</c> / <c>--config</c> value names.</param>
    /// <returns>
    /// The parsed options, or the first refusal met; every refusal carries
    /// <see cref="Curl.Protocol.Abstractions.CurlExitCode.FailedInit"/>, except a <c>-d @file</c> that
    /// cannot be read, which carries <see cref="Curl.Protocol.Abstractions.CurlExitCode.ReadError"/>.
    /// Never throws for non-null arguments, unless reading standard input for <c>-d @-</c> fails.
    /// </returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static CommandLineParseResult Parse(IReadOnlyList<string> arguments, Func<string, bool> pathExists, IPasswordPrompt passwordPrompt, IDataFileReader dataFileReader) =>
        Parse(arguments, pathExists, passwordPrompt, dataFileReader, []);

    /// <summary>
    /// Parses <paramref name="arguments"/> as the four-argument overload does, after first reading
    /// curl's default config file (<c>.curlrc</c>) where <paramref name="defaultConfigFileSearch"/> finds
    /// it, as curl 8.21.0 does, unless the first argument starts with <c>-q</c> or is exactly
    /// <c>--disable</c>. See <see cref="ConfigFileApplier.ApplyDefaultFile"/> for how it is read.
    /// </summary>
    /// <param name="arguments">The arguments; a <see langword="null"/> element reads as an empty argument.</param>
    /// <param name="pathExists">Reports whether a file or directory exists at a path.</param>
    /// <param name="passwordPrompt">Asks for the password of a <c>-u</c> user given without one; called at most once.</param>
    /// <param name="dataFileReader">Reads the default config file, and every file an option names.</param>
    /// <param name="defaultConfigFileSearch">Lists where to look for the default config file; <see cref="DefaultConfigFileSearch.ForProcess"/> for this process.</param>
    /// <returns>
    /// As the four-argument overload returns, with the default config file's options applied before the
    /// command line's and <see cref="CommandLineOptions.DefaultConfigFile"/> naming it. An empty command
    /// line is accepted when the file names a URL.
    /// </returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static CommandLineParseResult Parse(IReadOnlyList<string> arguments, Func<string, bool> pathExists, IPasswordPrompt passwordPrompt, IDataFileReader dataFileReader, DefaultConfigFileSearch defaultConfigFileSearch)
    {
        ArgumentNullException.ThrowIfNull(defaultConfigFileSearch);

        return Parse(arguments, pathExists, passwordPrompt, dataFileReader, defaultConfigFileSearch.CandidatePaths());
    }

    private static CommandLineParseResult Parse(IReadOnlyList<string> arguments, Func<string, bool> pathExists, IPasswordPrompt passwordPrompt, IDataFileReader dataFileReader, IReadOnlyList<string> defaultConfigFileCandidates)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(pathExists);
        ArgumentNullException.ThrowIfNull(passwordPrompt);
        ArgumentNullException.ThrowIfNull(dataFileReader);

        CommandLineOptions options = new();
        if (!SkipsDefaultConfigFile(arguments))
        {
            ConfigFileApplier.ApplyDefaultFile(options, defaultConfigFileCandidates, pathExists, dataFileReader);
        }

        return arguments.Count == 0 && options.Urls.Count == 0
            ? CommandLineParseResult.RefusedEmpty(options)
            : ParseArguments(options, arguments, pathExists, passwordPrompt, dataFileReader);
    }

    /// <summary>
    /// Reads <paramref name="arguments"/> into <paramref name="options"/>, which may already hold the
    /// default config file's settings, stopping at the first refusal or at <c>-V</c>, <c>-M</c> or <c>-h</c>.
    /// </summary>
    private static CommandLineParseResult ParseArguments(CommandLineOptions options, IReadOnlyList<string> arguments, Func<string, bool> pathExists, IPasswordPrompt passwordPrompt, IDataFileReader dataFileReader)
    {
        ArgumentReader reader = new(arguments, pathExists, dataFileReader);
        while (reader.TryTakeNext(out string argument))
        {
            CommandLineOptions group = options.CurrentGroup;
            CommandLineRefusal? refusal = reader.OptionsEnded
                ? AddPositionalUrl(group, argument)
                : ParseArgument(group, argument, reader);
            if (refusal is not null)
            {
                return CommandLineParseResult.Refused(refusal, options);
            }

            if (options.InformationRequested)
            {
                return CommandLineParseResult.InformationRequested(options);
            }
        }

        foreach (CommandLineOptions group in options.Groups)
        {
            group.ReadMissingPasswords(passwordPrompt);
        }

        return Finish(options);
    }

    /// <summary>
    /// Whether curl 8.21.0 skips its default config file: when the first argument starts with <c>-q</c>
    /// (so <c>-q</c>, <c>-qs</c> and <c>-qV</c> all do) or is exactly <c>--disable</c> (not <c>--disable=x</c>
    /// or <c>--no-disable</c>), as its <c>operate</c> checks; measured 2026-09-27.
    /// </summary>
    private static bool SkipsDefaultConfigFile(IReadOnlyList<string> arguments) =>
        arguments.Count > 0
        && ((arguments[0] ?? string.Empty).StartsWith("-q", StringComparison.Ordinal) || arguments[0] == "--disable");

    /// <summary>
    /// Checks each option group once the command line is all read, as curl 8.21.0 does while setting
    /// up that group's transfers: a group is refused when it names no URL, or when it asks for a
    /// multipart form post and a <c>-d</c> body both. A refused first group refuses the command line;
    /// a later one is accepted with the groups before it and <see cref="CommandLineParseResult.RefusalAfterGroups"/>,
    /// as curl runs those groups first (measured 2026-09-28, BL-508 Notes).
    /// </summary>
    private static CommandLineParseResult Finish(CommandLineOptions options)
    {
        CommandLineRefusal? refusal = TransferSetupRefusal(options);
        if (refusal is not null)
        {
            return CommandLineParseResult.Refused(refusal, options);
        }

        for (int group = 1; group < options.Groups.Count; group++)
        {
            refusal = TransferSetupRefusal(options.Groups[group]);
            if (refusal is not null)
            {
                return CommandLineParseResult.Accepted(options, group, refusal);
            }
        }

        return CommandLineParseResult.Accepted(options, options.Groups.Count, null);
    }

    /// <summary>The refusal curl 8.21.0 meets setting up <paramref name="group"/>'s transfers; <see langword="null"/> when there is none.</summary>
    private static CommandLineRefusal? TransferSetupRefusal(CommandLineOptions group)
    {
        if (group.Urls.Count == 0)
        {
            return CommandLineRefusal.NoUrlSpecified();
        }

        return group.HttpMethodSelected == SelectedHttpMethod.MultipartFormPost && group.PostData is not null
            ? RefuseFormAndDataBoth(group)
            : null;
    }

    /// <summary>
    /// Refuses a multipart form post that also has a <c>-d</c> body, after curl's warning naming the
    /// body's method: <c>GET</c> when <c>-G</c> sends it as the query, else <c>POST</c>.
    /// </summary>
    private static CommandLineRefusal RefuseFormAndDataBoth(CommandLineOptions group)
    {
        SelectedHttpMethod dataMethod = group.DataInQuery ? SelectedHttpMethod.Get : SelectedHttpMethod.Post;
        IReadOnlyList<string> warningLines = group.Silent
            ? []
            : CommandLineWarning.OnlyOneRequestMethod(dataMethod, SelectedHttpMethod.MultipartFormPost);
        return CommandLineRefusal.FormAndDataBoth(warningLines);
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

        return options.AddUrl(argument, argument);
    }

    private static CommandLineRefusal? ParseLong(CommandLineOptions options, string argument, ArgumentReader reader)
    {
        string nameAndValue = argument[EndOfOptions.Length..];
        int equals = nameAndValue.IndexOf('=', StringComparison.Ordinal);
        string longName = equals >= 0 ? nameAndValue[..equals] : nameAndValue;
        string? attachedValue = equals >= 0 ? nameAndValue[(equals + 1)..] : null;
        options.FirstOptionOfArgument = true;
        return CommandLineOptionTable.TryFindLong(longName, out CommandLineOption? option)
            ? ApplyLong(options, option, argument, attachedValue, reader)
            : ParseUnlistedLong(options, argument, longName, attachedValue, reader);
    }

    /// <summary>
    /// Applies a long option found in the table: a subject row reads its subject, a flag ignores any
    /// attached value (<c>--silent=x</c>), and a value row takes the attached value or else the next argument.
    /// </summary>
    private static CommandLineRefusal? ApplyLong(CommandLineOptions options, CommandLineOption option, string argument, string? attachedValue, ArgumentReader reader)
    {
        if (option.TakesSubject)
        {
            return ApplySubject(options, option, argument, attachedValue, reader);
        }

        if (!option.TakesValue)
        {
            return option.Apply(options, string.Empty, argument, reader.PathExists, reader.DataFileReader);
        }

        return attachedValue is not null
            ? option.Apply(options, attachedValue, argument, reader.PathExists, reader.DataFileReader)
            : ApplyNextArgument(options, option, argument, reader);
    }

    /// <summary>
    /// Reads a long name that is not in the table as <c>--no-&lt;name&gt;</c>: it turns off a
    /// negatable flag (any attached value ignored) unless the row refuses that, is refused as not reversible for any other row,
    /// and, when there is no <c>no-</c> or no row after it, is refused by <see cref="RefuseUnlistedName"/>
    /// or <see cref="RefuseUnlistedNegation"/>. Checked before a value is
    /// taken, so <c>--no-output</c> as the last argument is refused as not reversible.
    /// </summary>
    private static CommandLineRefusal? ParseNegatedLong(CommandLineOptions options, string argument, string longName, ArgumentReader reader)
    {
        if (!longName.StartsWith(NegationPrefix, StringComparison.Ordinal))
        {
            return RefuseUnlistedName(argument, longName);
        }

        string negatedName = longName[NegationPrefix.Length..];
        if (!CommandLineOptionTable.TryFindLong(negatedName, out CommandLineOption? option))
        {
            return RefuseUnlistedNegation(argument, negatedName);
        }

        if (option.Negate is null)
        {
            return CommandLineRefusal.CannotBeReversed(argument);
        }

        return option.Negate(options, string.Empty, argument, reader.PathExists, reader.DataFileReader);
    }

    /// <summary>
    /// Refuses a long name <see cref="CommandLineOptionTable"/> has no row for, as ADR-0137 decides: a name
    /// curl 8.21.0 knows (<see cref="CurlOptionAliasTable"/>) is not implemented yet and gets
    /// <see cref="CommandLineRefusal.InstalledLibcurlDoesNotSupport"/>; any other name is <see cref="CommandLineRefusal.UnknownOption"/>.
    /// </summary>
    private static CommandLineRefusal RefuseUnlistedName(string argument, string name) =>
        CurlOptionAliasTable.TryFindName(name, out _)
            ? CommandLineRefusal.InstalledLibcurlDoesNotSupport(argument)
            : CommandLineRefusal.UnknownOption(argument);

    /// <summary>
    /// Refuses <c>--no-&lt;name&gt;</c> where <see cref="CommandLineOptionTable"/> has no row for
    /// <paramref name="negatedName"/>, as ADR-0137 decides: not reversible when curl 8.21.0 knows the name
    /// but gives it no <c>--no-</c> prefix, not implemented yet when it gives it one, and unknown otherwise.
    /// </summary>
    private static CommandLineRefusal RefuseUnlistedNegation(string argument, string negatedName)
    {
        if (!CurlOptionAliasTable.TryFindName(negatedName, out CurlOptionAlias? alias))
        {
            return CommandLineRefusal.UnknownOption(argument);
        }

        return alias.NoPrefix == CurlOptionNoPrefix.NotAccepted
            ? CommandLineRefusal.CannotBeReversed(argument)
            : CommandLineRefusal.InstalledLibcurlDoesNotSupport(argument);
    }

    /// <summary>
    /// Refuses the short letter at <paramref name="letter"/> that <see cref="CommandLineOptionTable"/> has no
    /// row for, spelled as the whole argument: not implemented yet when it is one of curl 8.21.0's letters
    /// (ADR-0137), unknown otherwise.
    /// </summary>
    private static CommandLineRefusal RefuseUnlistedLetter(string argument, int letter) =>
        CurlOptionAliasTable.IsLetter(argument[letter])
            ? CommandLineRefusal.InstalledLibcurlDoesNotSupport(argument)
            : CommandLineRefusal.UnknownOption(argument);

    /// <summary>Reads a long name that is not in the table: as <c>--expand-&lt;name&gt;</c>, or else as <c>--no-&lt;name&gt;</c>.</summary>
    private static CommandLineRefusal? ParseUnlistedLong(CommandLineOptions options, string argument, string longName, string? attachedValue, ArgumentReader reader) =>
        longName.StartsWith(ExpansionPrefix, StringComparison.Ordinal)
            ? ParseExpandedLong(options, argument, longName[ExpansionPrefix.Length..], attachedValue, reader)
            : ParseNegatedLong(options, argument, longName, reader);

    /// <summary>
    /// Reads <c>--expand-&lt;name&gt;</c> as curl 8.21.0's <c>getparameter</c> does: the option
    /// <c>&lt;name&gt;</c>, its value first expanded by <see cref="VariableExpansion"/>. The value is the
    /// attached one or the next argument; when there is one, an option that takes no value is refused
    /// with <see cref="CommandLineRefusal.VariableExpansionFailure"/> (<c>--expand-silent x</c> is refused,
    /// <c>--expand-silent</c> as the last argument turns <c>-s</c> on). <c>--no-expand-</c> and
    /// <c>--expand-no-</c> spellings are unknown options, as <c>no-</c> is looked for first and only once.
    /// </summary>
    private static CommandLineRefusal? ParseExpandedLong(CommandLineOptions options, string argument, string longName, string? attachedValue, ArgumentReader reader)
    {
        if (!CommandLineOptionTable.TryFindLong(longName, out CommandLineOption? option))
        {
            return RefuseUnlistedName(argument, longName);
        }

        string? template = attachedValue ?? reader.PeekNext();
        if (template is null)
        {
            return option.TakesValue
                ? CommandLineRefusal.RequiresParameter(argument)
                : option.Apply(options, string.Empty, argument, reader.PathExists, reader.DataFileReader);
        }

        return option.TakesValue
            ? ApplyExpandedValue(options, option, argument, template, attachedValue is null, reader)
            : CommandLineRefusal.VariableExpansionFailure(argument, null, options.ErrorsHidden);
    }

    /// <summary>
    /// Expands <paramref name="template"/> and applies the result, first taking the next argument when
    /// <paramref name="valueIsNextArgument"/> and the expansion was not refused.
    /// </summary>
    private static CommandLineRefusal? ApplyExpandedValue(CommandLineOptions options, CommandLineOption option, string argument, string template, bool valueIsNextArgument, ArgumentReader reader)
    {
        CommandLineRefusal? refusal = VariableExpansion.Expand(options, template, argument, out string value);
        if (refusal is not null)
        {
            return refusal;
        }

        if (valueIsNextArgument)
        {
            reader.TryTakeNext(out _);
        }

        return option.Apply(options, value, argument, reader.PathExists, reader.DataFileReader);
    }

    private static CommandLineRefusal? ParseShortBundle(CommandLineOptions options, string argument, ArgumentReader reader)
    {
        if (argument.Length == 1)
        {
            return CommandLineRefusal.UnknownOption(argument);
        }

        for (int letter = 1; letter < argument.Length; letter++)
        {
            if (ApplyBundleLetterEndsBundle(options, argument, letter, reader, out CommandLineRefusal? refusal))
            {
                return refusal;
            }
        }

        return null;
    }

    /// <summary>
    /// Applies the option letter at <paramref name="letter"/> in a short-option bundle. Returns
    /// <see langword="true"/> when the bundle ends there: an unknown letter, a letter that takes the
    /// rest of the bundle or the next argument, or a flag that ends it; <paramref name="refusal"/> is
    /// then its outcome.
    /// </summary>
    private static bool ApplyBundleLetterEndsBundle(CommandLineOptions options, string argument, int letter, ArgumentReader reader, out CommandLineRefusal? refusal)
    {
        if (!CommandLineOptionTable.TryFindShort(argument[letter], out CommandLineOption? option))
        {
            refusal = RefuseUnlistedLetter(argument, letter);
            return true;
        }

        options.FirstOptionOfArgument = letter == 1;

        if (option.TakesSubject)
        {
            refusal = ApplySubjectLetter(options, option, argument, letter, reader);
            return true;
        }

        if (option.TakesValue)
        {
            refusal = ApplyRestOfBundle(options, option, argument, argument[(letter + 1)..], reader);
            return true;
        }

        return ApplyFlagLetterEndsBundle(options, option, argument, reader, out refusal);
    }

    /// <summary>
    /// Applies a flag letter of a bundle and says whether the bundle ends there: on a refusal, or
    /// after <c>-V</c> or <c>-M</c>, which end the bundle as they end the command line (<c>curl -Vo</c> prints the version),
    /// or after a <see cref="CommandLineOption.EndsBundle"/> letter (<c>curl -2s</c> is <c>-2</c> alone).
    /// A <see cref="CommandLineOption.ShortNameTurnsOff"/> letter (<c>-N</c>) turns its flag off.
    /// </summary>
    private static bool ApplyFlagLetterEndsBundle(CommandLineOptions options, CommandLineOption option, string argument, ArgumentReader reader, out CommandLineRefusal? refusal)
    {
        CommandLineOptionApplier apply = option.ShortNameTurnsOff ? option.Negate! : option.Apply;
        refusal = apply(options, string.Empty, argument, reader.PathExists, reader.DataFileReader);
        return refusal is not null || options.InformationRequested || option.EndsBundle;
    }

    /// <summary>
    /// Applies a <see cref="CommandLineOption.Subject"/> letter (<c>-h</c>) of a bundle: as the last letter
    /// it reads the next argument as its subject; before other letters it ends the bundle having applied
    /// nothing, as curl 8.21.0 reads <c>-hv</c>.
    /// </summary>
    private static CommandLineRefusal? ApplySubjectLetter(CommandLineOptions options, CommandLineOption option, string argument, int letter, ArgumentReader reader) =>
        letter == argument.Length - 1
            ? ApplySubject(options, option, argument, null, reader)
            : null;

    /// <summary>
    /// Applies a <see cref="CommandLineOption.Subject"/> row with its attached value, or else the next
    /// argument, taken, or else an empty subject.
    /// </summary>
    private static CommandLineRefusal? ApplySubject(CommandLineOptions options, CommandLineOption option, string argument, string? attachedValue, ArgumentReader reader)
    {
        string subject = attachedValue ?? (reader.TryTakeNext(out string next) ? next : string.Empty);
        return option.Apply(options, subject, argument, reader.PathExists, reader.DataFileReader);
    }

    /// <summary>
    /// Applies a value letter's value: the rest of its bundle (<c>-ofile</c>), or, when nothing follows
    /// the letter, the next argument.
    /// </summary>
    private static CommandLineRefusal? ApplyRestOfBundle(CommandLineOptions options, CommandLineOption option, string argument, string restOfBundle, ArgumentReader reader) =>
        restOfBundle.Length > 0
            ? option.Apply(options, restOfBundle, argument, reader.PathExists, reader.DataFileReader)
            : ApplyNextArgument(options, option, argument, reader);

    private static CommandLineRefusal? ApplyNextArgument(CommandLineOptions options, CommandLineOption option, string argument, ArgumentReader reader) =>
        reader.TryTakeNext(out string value)
            ? option.Apply(options, value, argument, reader.PathExists, reader.DataFileReader)
            : CommandLineRefusal.RequiresParameter(argument);

    /// <summary>
    /// Applies one line of a <c>-K</c> file as curl 8.21.0's <c>getparameter</c> does: an option that
    /// starts with <c>-</c> but not <c>--</c> is a bundle of short letters, any other is a long name with
    /// or without its <c>--</c> (so <c>--no-</c> and <c>--name=value</c> work as on the command line), and
    /// <paramref name="parameter"/> is the one argument that may follow it. A non-empty parameter the
    /// option does not take is refused with <see cref="CommandLineRefusal.UnusedConfigFileParameter"/>.
    /// </summary>
    /// <param name="options">The options being filled in.</param>
    /// <param name="option">The option as written on the line.</param>
    /// <param name="parameter">The line's parameter, or <see langword="null"/> when it has none.</param>
    /// <param name="pathExists">The parse's path-existence check.</param>
    /// <param name="dataFileReader">The parse's data file reader.</param>
    /// <returns><see langword="null"/> when the line was applied; otherwise why it was refused.</returns>
    internal static CommandLineRefusal? ApplyConfigFileLine(CommandLineOptions options, string option, string? parameter, Func<string, bool> pathExists, IDataFileReader dataFileReader)
    {
        ArgumentReader reader = new(parameter is null ? [] : [parameter], pathExists, dataFileReader);
        bool readingEnclosingConfigFile = options.ReadingConfigFile;
        options.ReadingConfigFile = true;
        CommandLineRefusal? refusal = ParseConfigFileOption(options.CurrentGroup, option, reader);
        options.ReadingConfigFile = readingEnclosingConfigFile;

        // curl 8.21.0 ignores version and manual in a -K file: a file of either alone reports no URL.
        // It prints the page for help there and carries on, so the console prints that page first.
        options.MoveConfigFileInformationRequests();
        return refusal is null && !string.IsNullOrEmpty(parameter) && reader.TryTakeNext(out _)
            ? CommandLineRefusal.UnusedConfigFileParameter(option)
            : refusal;
    }

    /// <summary>Reads a <c>-K</c> file line's option as a short bundle when it starts with one <c>-</c>, else as a long name.</summary>
    private static CommandLineRefusal? ParseConfigFileOption(CommandLineOptions options, string option, ArgumentReader reader)
    {
        if (option.StartsWith(EndOfOptions, StringComparison.Ordinal))
        {
            return ParseLong(options, option, reader);
        }

        return option.StartsWith('-')
            ? ParseShortBundle(options, option, reader)
            : ParseLong(options, EndOfOptions + option, reader);
    }

    /// <summary>Reports whether a file or a directory exists at <paramref name="path"/>, as curl's check does.</summary>
    private static bool PathExistsOnDisk(string path) => Path.Exists(path);

    /// <summary>
    /// Walks the arguments in order, remembers whether <c>--</c> has ended option parsing, and
    /// carries the path-existence check and the data file reader the appliers are given.
    /// </summary>
    private sealed class ArgumentReader(IReadOnlyList<string> arguments, Func<string, bool> pathExists, IDataFileReader dataFileReader)
    {
        private int next;

        public Func<string, bool> PathExists => pathExists;

        public IDataFileReader DataFileReader => dataFileReader;

        public bool OptionsEnded { get; set; }

        /// <summary>The next argument, not taken; <see langword="null"/> when none is left.</summary>
        public string? PeekNext() => next == arguments.Count ? null : arguments[next] ?? string.Empty;

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
