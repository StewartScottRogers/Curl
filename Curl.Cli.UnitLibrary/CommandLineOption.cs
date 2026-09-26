namespace Curl.Cli;

/// <summary>
/// One row of <see cref="CommandLineOptionTable"/>: an option's long name, optional
/// short letter, whether it takes a value, how it sets <see cref="CommandLineOptions"/>, and
/// how <c>--no-</c> in front of its long name turns it off, when curl lets it.
/// </summary>
public sealed class CommandLineOption
{
    private CommandLineOption(string longName, char? shortName, bool takesValue, CommandLineOptionApplier apply, CommandLineOptionApplier? negate = null)
    {
        LongName = longName;
        ShortName = shortName;
        TakesValue = takesValue;
        Apply = apply;
        Negate = negate;
    }

    /// <summary>The long name without its leading <c>--</c>, matched exactly and case-sensitively.</summary>
    public string LongName { get; }

    /// <summary>The single-letter name used after <c>-</c>, or <see langword="null"/> when there is none.</summary>
    public char? ShortName { get; }

    /// <summary><see langword="true"/> when the option takes a value; <see langword="false"/> for a flag.</summary>
    public bool TakesValue { get; }

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
}
