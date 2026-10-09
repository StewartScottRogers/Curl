namespace Curl.Cli;

/// <summary>
/// One row of <see cref="CommandLineOptionTable"/>: an option's long name, optional
/// short letter, whether it takes a value, how it sets <see cref="CommandLineOptions"/>, and
/// how <c>--no-</c> in front of its long name turns it off, when curl lets it.
/// </summary>
public sealed class CommandLineOption
{
    private CommandLineOption(string longName, char? shortName, bool takesValue, CommandLineOptionApplier apply, CommandLineOptionApplier? negate = null, bool takesSubject = false, bool endsBundle = false, bool shortNameTurnsOff = false, bool warnsAboutLeadingUnicode = true, bool refusedByWindowsSchannelBuild = false, bool refusedWhenTurnedOn = false)
    {
        RefusedWhenTurnedOn = refusedWhenTurnedOn;
        LongName = longName;
        ShortName = shortName;
        TakesValue = takesValue;
        Apply = takesValue && warnsAboutLeadingUnicode ? WarnAboutLeadingUnicodeThen(apply) : apply;
        Negate = negate;
        TakesSubject = takesSubject;
        EndsBundle = endsBundle;
        ShortNameTurnsOff = shortNameTurnsOff;
        RefusedByWindowsSchannelBuild = refusedByWindowsSchannelBuild;
    }

    /// <summary>
    /// <see langword="true"/> for a row built with <see cref="NegatableFlagTurnedOffByShortName"/>: its
    /// short letter applies <see cref="Negate"/>, not <see cref="Apply"/>, as curl 8.21.0's <c>-N</c> means
    /// <c>--no-buffer</c>.
    /// </summary>
    public bool ShortNameTurnsOff { get; }

    /// <summary>
    /// <see langword="true"/> for a row built with <see cref="NoFunctionFlag"/> or <see cref="NextGroup"/>: as a letter of a
    /// short-option bundle it ends the bundle, and the letters after it are read as nothing at all.
    /// </summary>
    public bool EndsBundle { get; }

    /// <summary>The long name without its leading <c>--</c>, matched exactly and case-sensitively.</summary>
    public string LongName { get; }

    /// <summary>The single-letter name used after <c>-</c>, or <see langword="null"/> when there is none.</summary>
    public char? ShortName { get; }

    /// <summary><see langword="true"/> when the option takes a value; <see langword="false"/> for a flag.</summary>
    public bool TakesValue { get; }

    /// <summary>
    /// <see langword="true"/> for a row built with <see cref="Subject"/>: it takes no value of its own
    /// but reads the attached value, or else the next argument when there is one, as its subject,
    /// and as the letter of a bundle it counts only when it is the bundle's last letter.
    /// </summary>
    public bool TakesSubject { get; }

    /// <summary>
    /// Checks the value and sets the option on <see cref="CommandLineOptions"/>, or returns the
    /// refusal. The parser passes every value unchanged, empty included; the applier decides
    /// whether an empty value is refused. A flag's applier returns <see langword="null"/> unless the
    /// row was built with <see cref="NegatableFlagThatCanRefuse"/>.
    /// </summary>
    public CommandLineOptionApplier Apply { get; }

    /// <summary>
    /// Turns the flag off for <c>--no-&lt;long name&gt;</c>, or refuses to, called like
    /// <see cref="Apply"/> with an empty value; <see langword="null"/> when curl 8.21.0 refuses the
    /// <c>--no-</c> spelling of this option with <see cref="CommandLineRefusal.CannotBeReversed(string)"/>.
    /// Only a row built with <see cref="NegatableFlag"/> or <see cref="NegatableFlagThatCanRefuse"/> has one.
    /// </summary>
    public CommandLineOptionApplier? Negate { get; }

    /// <summary>
    /// <see langword="true"/> for a row returned by <see cref="RefusedBySchannelBuild"/>: curl's Windows Schannel
    /// build refuses the option with exit 2, and so does Curl when it reads as that build.
    /// </summary>
    public bool RefusedByWindowsSchannelBuild { get; }

    /// <summary>
    /// <see langword="true"/> for a row built with <see cref="UnsupportedFlagTurnedOffQuietly"/>: turning the
    /// flag on is refused on every build, so the option is not supported, though its <c>--no-</c> spelling is accepted.
    /// </summary>
    public bool RefusedWhenTurnedOn { get; }

    /// <summary>
    /// Creates a row for a flag that turning on is refused with
    /// <see cref="CommandLineRefusal.InstalledLibcurlDoesNotSupport"/> on every build, while its <c>--no-</c>
    /// spelling is accepted and changes nothing: curl 8.21.0 checks the feature only when the flag is turned
    /// on (<c>--proxy-http3</c> refused, <c>--no-proxy-http3</c> accepted, measured with the Schannel build on
    /// 2026-10-08, BL-1829).
    /// </summary>
    /// <param name="longName">The long name without its leading <c>--</c> or <c>--no-</c>.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="longName"/> is <see langword="null"/>.</exception>
    public static CommandLineOption UnsupportedFlagTurnedOffQuietly(string longName)
    {
        ArgumentNullException.ThrowIfNull(longName);

        return new CommandLineOption(
            longName,
            null,
            takesValue: false,
            (_, _, spelledOption, _, _) => CommandLineRefusal.InstalledLibcurlDoesNotSupport(spelledOption),
            (_, _, _, _, _) => null,
            refusedWhenTurnedOn: true);
    }

    /// <summary>
    /// Returns this row refused with <see cref="CommandLineRefusal.InstalledLibcurlDoesNotSupport"/> whenever
    /// the parse reads as curl's Windows Schannel build (<see cref="CommandLineOptions.ActsAsWindowsSchannelBuild"/>),
    /// whose libcurl was built without the feature the option asks for, and otherwise applied as before. The
    /// refusal comes after a missing value is refused as <c>requires parameter</c> and before the value is
    /// checked, so <c>--tlsauthtype ''</c> is refused as unsupported, not as blank (measured with curl 8.21.0
    /// Schannel, 2026-10-02, BL-1278).
    /// </summary>
    /// <returns>The row, refused on the Windows Schannel build.</returns>
    public CommandLineOption RefusedBySchannelBuild()
    {
        return new CommandLineOption(
            LongName,
            ShortName,
            TakesValue,
            RefusingOnSchannelBuild(Apply),
            Negate is null ? null : RefusingOnSchannelBuild(Negate),
            TakesSubject,
            EndsBundle,
            ShortNameTurnsOff,
            warnsAboutLeadingUnicode: false,
            refusedByWindowsSchannelBuild: true);
    }

    /// <summary>
    /// Wraps <paramref name="apply"/> so it refuses on the Windows Schannel build. curl 8.21.0 checks the
    /// feature before it reads the toggle, so the <c>--no-</c> spelling is refused too (<c>--no-proxy-http2</c>).
    /// </summary>
    private static CommandLineOptionApplier RefusingOnSchannelBuild(CommandLineOptionApplier apply) =>
        (options, value, spelledOption, pathExists, dataFileReader) => options.ActsAsWindowsSchannelBuild
            ? CommandLineRefusal.InstalledLibcurlDoesNotSupport(spelledOption)
            : apply(options, value, spelledOption, pathExists, dataFileReader);

    /// <summary>
    /// Creates a row for an option that takes no value and whose <c>--no-</c> spelling curl refuses
    /// (<c>--tlsv1.2</c>); a flag curl lets be negated is <see cref="NegatableFlag"/>.
    /// </summary>
    /// <param name="longName">The long name without its leading <c>--</c>.</param>
    /// <param name="shortName">The short letter, or <see langword="null"/> when there is none.</param>
    /// <param name="set">Sets the flag on the options being filled in.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="longName"/> or <paramref name="set"/> is <see langword="null"/>.</exception>
    public static CommandLineOption Flag(string longName, char? shortName, Action<CommandLineOptions> set)
    {
        ArgumentNullException.ThrowIfNull(longName);
        ArgumentNullException.ThrowIfNull(set);

        return new CommandLineOption(longName, shortName, takesValue: false, (options, _, _, _, _) =>
        {
            set(options);
            return null;
        });
    }

    /// <summary>
    /// Creates a row for an option that takes no value, whose <c>--no-</c> spelling curl refuses, and
    /// that can itself be refused given what was read before it, as curl 8.21.0 refuses <c>--tlsv1.3</c>
    /// after <c>--tls-max 1.2</c>.
    /// </summary>
    /// <param name="longName">The long name without its leading <c>--</c>.</param>
    /// <param name="shortName">The short letter, or <see langword="null"/> when there is none.</param>
    /// <param name="setOrRefuse">
    /// Sets the flag on the options being filled in and returns <see langword="null"/>, or returns the
    /// refusal; it is given the argument as typed, for naming it in a refusal.
    /// </param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="longName"/> or <paramref name="setOrRefuse"/> is <see langword="null"/>.</exception>
    public static CommandLineOption FlagThatCanRefuse(string longName, char? shortName, Func<CommandLineOptions, string, CommandLineRefusal?> setOrRefuse)
    {
        ArgumentNullException.ThrowIfNull(longName);
        ArgumentNullException.ThrowIfNull(setOrRefuse);

        return new CommandLineOption(longName, shortName, takesValue: false, (options, _, spelledOption, _, _) =>
            setOrRefuse(options, spelledOption));
    }

    /// <summary>
    /// Creates the row for <c>-:</c> / <c>--next</c>, which ends one option group and starts the next:
    /// it takes no value (<c>--next=x</c> ignores the <c>x</c>), starts the next group through
    /// <see cref="CommandLineOptions.StartNextGroup"/>, and as a letter of a bundle ends the bundle, so
    /// <c>-:s x</c> reads only <c>-:</c> and takes <c>x</c> as the next group's URL (measured 2026-09-28,
    /// BL-508 Notes). Its <c>--no-</c> spelling is refused as not reversible.
    /// </summary>
    /// <param name="longName">The long name without its leading <c>--</c>.</param>
    /// <param name="shortName">The short letter.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="longName"/> is <see langword="null"/>.</exception>
    public static CommandLineOption NextGroup(string longName, char shortName)
    {
        ArgumentNullException.ThrowIfNull(longName);

        return new CommandLineOption(
            longName,
            shortName,
            takesValue: false,
            (options, _, spelledOption, _, _) => options.StartNextGroup(spelledOption),
            endsBundle: true);
    }

    /// <summary>
    /// Creates a row for a flag curl 8.21.0 still accepts but that no longer does anything
    /// (<c>--sslv2</c>, <c>--metalink</c>): every spelling of it, its <c>--no-</c> one too when
    /// <paramref name="negatable"/>, changes no setting and adds
    /// <see cref="CommandLineWarning.DeprecatedWithNoFunction(string)"/> unless <c>-s</c> / <c>--silent</c>
    /// has been read already. As a letter of a bundle it ends the bundle: curl 8.21.0 reads <c>-2s</c>
    /// and <c>-23</c> as <c>-2</c> alone (measured 2026-09-28: <c>-2s</c> still shows the progress meter,
    /// <c>-23</c> warns about <c>--sslv2</c> only).
    /// </summary>
    /// <param name="longName">The long name without its leading <c>--</c>.</param>
    /// <param name="shortName">The short letter, or <see langword="null"/> when there is none.</param>
    /// <param name="negatable">Whether curl accepts the <c>--no-</c> spelling rather than refusing it as not reversible.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="longName"/> is <see langword="null"/>.</exception>
    public static CommandLineOption NoFunctionFlag(string longName, char? shortName, bool negatable)
    {
        ArgumentNullException.ThrowIfNull(longName);

        CommandLineOptionApplier warn = WarnDeprecatedWithNoFunction(longName);
        return new CommandLineOption(longName, shortName, takesValue: false, warn, negatable ? warn : null, endsBundle: true);
    }

    /// <summary>
    /// Creates a row for an option that takes a value and that curl 8.21.0 still accepts but that no
    /// longer does anything (<c>--egd-file</c>, <c>--random-file</c>, <c>--krb4</c>): the value, empty
    /// included, is taken and ignored, and <see cref="CommandLineWarning.DeprecatedWithNoFunction(string)"/>
    /// is added unless <c>-s</c> / <c>--silent</c> has been read already. Its <c>--no-</c> spelling is
    /// refused as not reversible, and a missing value as for any value option.
    /// </summary>
    /// <param name="longName">The long name without its leading <c>--</c>.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="longName"/> is <see langword="null"/>.</exception>
    public static CommandLineOption NoFunctionValue(string longName)
    {
        ArgumentNullException.ThrowIfNull(longName);

        return new CommandLineOption(longName, shortName: null, takesValue: true, WarnDeprecatedWithNoFunction(longName), warnsAboutLeadingUnicode: false);
    }

    /// <summary>
    /// An applier that first adds <see cref="CommandLineWarning.ArgumentStartsWithUnicode(string)"/>, unless
    /// <c>-s</c> / <c>--silent</c> has been read already, when the value starts with a character in
    /// U+2000-U+203F and the value is read as UTF-8: the parse reads its arguments so (<see cref="CommandLineOptions.ReadsArgumentsAsUtf8"/>)
    /// or it comes from a <c>-K</c> file, whose lines are UTF-8 bytes on every platform (BL-1438), or it
    /// is an <c>--expand-</c> value led by a variable's bytes (<see cref="CommandLineOptions.ApplyingValueLedByVariableBytes"/>, BL-1814),
    /// and then runs <paramref name="apply"/>. curl 8.21.0's <c>getparameter</c> checks every option value
    /// this way before using it, except a deprecated option's, so each value row but
    /// <see cref="NoFunctionValue"/> is built with it (BL-1224).
    /// </summary>
    private static CommandLineOptionApplier WarnAboutLeadingUnicodeThen(CommandLineOptionApplier apply) =>
        (options, value, spelledOption, pathExists, dataFileReader) =>
        {
            if ((options.ReadsArgumentsAsUtf8 || options.ReadingConfigFile || options.ApplyingValueLedByVariableBytes) && value.Length > 0 && value[0] is >= ' ' and <= '‿')
            {
                options.AddWarningLinesUnlessSilent(CommandLineWarning.ArgumentStartsWithUnicode(value));
            }

            return apply(options, value, spelledOption, pathExists, dataFileReader);
        };

    /// <summary>An applier that ignores its value and adds curl's no-function warning for <paramref name="longName"/>.</summary>
    private static CommandLineOptionApplier WarnDeprecatedWithNoFunction(string longName) =>
        (options, _, _, _, _) =>
        {
            options.AddWarningLinesUnlessSilent(CommandLineWarning.DeprecatedWithNoFunction(longName));
            return null;
        };

    /// <summary>
    /// Creates a row for an option that takes no value and that <c>--no-&lt;long name&gt;</c> turns
    /// off, as curl 8.21.0 allows for the boolean options its manual marks as negatable
    /// (<see href="https://curl.se/docs/manpage.html"/>). The positive spelling passes
    /// <see langword="true"/> to <paramref name="set"/>, the <c>--no-</c> spelling <see langword="false"/>;
    /// whichever comes last on the command line wins.
    /// </summary>
    /// <param name="longName">The long name without its leading <c>--</c> or <c>--no-</c>.</param>
    /// <param name="shortName">The short letter, or <see langword="null"/> when there is none. It is never negated.</param>
    /// <param name="set">Sets the flag on, or off, on the options being filled in.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="longName"/> or <paramref name="set"/> is <see langword="null"/>.</exception>
    public static CommandLineOption NegatableFlag(string longName, char? shortName, Action<CommandLineOptions, bool> set)
    {
        ArgumentNullException.ThrowIfNull(longName);
        ArgumentNullException.ThrowIfNull(set);

        return new CommandLineOption(
            longName,
            shortName,
            takesValue: false,
            (options, _, _, _, _) =>
            {
                set(options, true);
                return null;
            },
            (options, _, _, _, _) =>
            {
                set(options, false);
                return null;
            });
    }

    /// <summary>
    /// Creates a row for a flag like <see cref="NegatableFlag"/>, except that its short letter turns
    /// it off, as the <c>--no-</c> spelling does: curl 8.21.0 reads <c>-N</c> as <c>--no-buffer</c>,
    /// while <c>--buffer</c> turns buffering back on.
    /// </summary>
    /// <param name="longName">The long name without its leading <c>--</c> or <c>--no-</c>.</param>
    /// <param name="shortName">The short letter, which passes <see langword="false"/> to <paramref name="set"/>.</param>
    /// <param name="set">Sets the flag on, or off, on the options being filled in.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="longName"/> or <paramref name="set"/> is <see langword="null"/>.</exception>
    public static CommandLineOption NegatableFlagTurnedOffByShortName(string longName, char shortName, Action<CommandLineOptions, bool> set)
    {
        CommandLineOption row = NegatableFlag(longName, shortName, set);

        return new CommandLineOption(longName, shortName, takesValue: false, row.Apply, row.Negate, shortNameTurnsOff: true);
    }

    /// <summary>
    /// Creates a row for a flag like <see cref="NegatableFlag"/>, except that turning it on or off can
    /// be refused, as curl 8.21.0 refuses <c>-I</c> after <c>--no-head</c>. The positive spelling passes
    /// <see langword="true"/> to <paramref name="setOrRefuse"/>, the <c>--no-</c> spelling
    /// <see langword="false"/>, each with the argument as typed for naming it in a refusal.
    /// </summary>
    /// <param name="longName">The long name without its leading <c>--</c> or <c>--no-</c>.</param>
    /// <param name="shortName">The short letter, or <see langword="null"/> when there is none. It is never negated.</param>
    /// <param name="setOrRefuse">
    /// Sets the flag on, or off, on the options being filled in and returns <see langword="null"/>, or
    /// returns the refusal.
    /// </param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="longName"/> or <paramref name="setOrRefuse"/> is <see langword="null"/>.</exception>
    public static CommandLineOption NegatableFlagThatCanRefuse(string longName, char? shortName, Func<CommandLineOptions, bool, string, CommandLineRefusal?> setOrRefuse)
    {
        ArgumentNullException.ThrowIfNull(longName);
        ArgumentNullException.ThrowIfNull(setOrRefuse);

        return new CommandLineOption(
            longName,
            shortName,
            takesValue: false,
            (options, _, spelledOption, _, _) => setOrRefuse(options, true, spelledOption),
            (options, _, spelledOption, _, _) => setOrRefuse(options, false, spelledOption));
    }

    /// <summary>
    /// Creates a row for an option that takes non-blank text: an empty value is refused with
    /// <see cref="CommandLineRefusal.BlankArgument(string)"/> for the option as typed, as curl
    /// 8.21.0 does; any other value is passed to <paramref name="set"/>.
    /// </summary>
    /// <param name="longName">The long name without its leading <c>--</c>.</param>
    /// <param name="shortName">The short letter, or <see langword="null"/> when there is none.</param>
    /// <param name="set">Stores the non-empty text on the options being filled in.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="longName"/> or <paramref name="set"/> is <see langword="null"/>.</exception>
    public static CommandLineOption Text(string longName, char? shortName, Action<CommandLineOptions, string> set)
    {
        ArgumentNullException.ThrowIfNull(longName);
        ArgumentNullException.ThrowIfNull(set);

        return new CommandLineOption(longName, shortName, takesValue: true, (options, value, spelledOption, _, _) =>
        {
            if (value.Length == 0)
            {
                return CommandLineRefusal.BlankArgument(spelledOption);
            }

            set(options, value);
            return null;
        });
    }

    /// <summary>
    /// Creates a row for an option that takes a file name: refused when empty, as
    /// <see cref="Text"/> is, and otherwise passed to <paramref name="set"/>, after adding
    /// <see cref="CommandLineWarning.FileNameLooksLikeFlag(string)"/> when the value looks like a flag
    /// and <c>-s</c> / <c>--silent</c> has not been read yet.
    /// </summary>
    /// <remarks>
    /// A value looks like a flag when it starts with <c>-</c> and is longer than that one
    /// character. Measured with the local curl 8.21.0 on 2026-09-26: <c>-o -s</c>, <c>-o -x</c>,
    /// <c>-o --</c>, <c>--output --output</c>, <c>-o-s</c> and <c>--output=-s</c> all warn and still
    /// take the value as the file name; <c>-o -</c> (standard output) and <c>-o file</c> do not warn.
    /// </remarks>
    /// <param name="longName">The long name without its leading <c>--</c>.</param>
    /// <param name="shortName">The short letter, or <see langword="null"/> when there is none.</param>
    /// <param name="set">Stores the non-empty file name on the options being filled in.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="longName"/> or <paramref name="set"/> is <see langword="null"/>.</exception>
    public static CommandLineOption FileName(string longName, char? shortName, Action<CommandLineOptions, string> set)
    {
        ArgumentNullException.ThrowIfNull(set);

        return Text(longName, shortName, (options, fileName) =>
        {
            WarnWhenFileNameLooksLikeFlag(options, fileName);
            set(options, fileName);
        });
    }

    /// <summary>
    /// Adds <see cref="CommandLineWarning.FileNameLooksLikeFlag(string)"/> to the warning lines when
    /// <paramref name="fileName"/> starts with <c>-</c> and is longer than that one character, unless
    /// <c>-s</c> / <c>--silent</c> has been read already. <see cref="FileName"/> rows call it, and so
    /// does <c>--cacert</c>, which warns before it checks that the file exists.
    /// </summary>
    /// <param name="options">The options being filled in.</param>
    /// <param name="fileName">The file name as given on the command line.</param>
    internal static void WarnWhenFileNameLooksLikeFlag(CommandLineOptions options, string fileName)
    {
        if (fileName.Length > 1 && fileName[0] == '-')
        {
            options.AddWarningLinesUnlessSilent([CommandLineWarning.FileNameLooksLikeFlag(fileName)]);
        }
    }

    /// <summary>
    /// Creates a row for an option that takes a value, with an applier that does all of the
    /// value's checking itself, including what an empty value means. Use <see cref="Text"/>
    /// for plain text; a numeric option passes an applier built on <see cref="CommandLineNumber"/>.
    /// </summary>
    /// <param name="longName">The long name without its leading <c>--</c>.</param>
    /// <param name="shortName">The short letter, or <see langword="null"/> when there is none.</param>
    /// <param name="apply">Checks the value and applies it to the options being filled in, or refuses it.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="longName"/> or <paramref name="apply"/> is <see langword="null"/>.</exception>
    public static CommandLineOption Value(string longName, char? shortName, CommandLineOptionApplier apply)
    {
        ArgumentNullException.ThrowIfNull(longName);
        ArgumentNullException.ThrowIfNull(apply);

        return new CommandLineOption(longName, shortName, takesValue: true, apply);
    }

    /// <summary>
    /// Creates a row for an option that is not reversible and reads an optional subject, as curl 8.21.0
    /// reads <c>-h</c> / <c>--help</c>: the attached value (<c>--help=all</c>), or else the next argument
    /// when there is one, whatever it looks like (<c>--help -v</c>), or an empty subject when there is
    /// none. As a letter inside a bundle it counts only when it is the last one: <c>-vh</c> asks for
    /// help, while <c>-hv</c> is read as nothing at all, the letters after it included (measured
    /// 2026-09-27: <c>curl -hv</c> reports no URL, and <c>curl -hv &lt;url&gt;</c> transfers without
    /// <c>-v</c>).
    /// </summary>
    /// <param name="longName">The long name without its leading <c>--</c>.</param>
    /// <param name="shortName">The short letter, or <see langword="null"/> when there is none.</param>
    /// <param name="set">Records the request and its subject, empty when none was given.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="longName"/> or <paramref name="set"/> is <see langword="null"/>.</exception>
    public static CommandLineOption Subject(string longName, char? shortName, Action<CommandLineOptions, string> set)
    {
        ArgumentNullException.ThrowIfNull(longName);
        ArgumentNullException.ThrowIfNull(set);

        return new CommandLineOption(
            longName,
            shortName,
            takesValue: false,
            (options, subject, _, _, _) =>
            {
                set(options, subject);
                return null;
            },
            takesSubject: true);
    }
}
