namespace Curl.Cli;

/// <summary>
/// One row of <see cref="CommandLineOptionTable"/>: an option's long name, optional
/// short letter, whether it takes a value, and how it sets <see cref="CommandLineOptions"/>.
/// </summary>
public sealed class CommandLineOption
{
    private CommandLineOption(string longName, char? shortName, bool takesValue, CommandLineOptionApplier apply)
    {
        LongName = longName;
        ShortName = shortName;
        TakesValue = takesValue;
        Apply = apply;
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
    /// whether an empty value is refused. A flag's applier always returns <see langword="null"/>.
    /// </summary>
    public CommandLineOptionApplier Apply { get; }

    /// <summary>Creates a row for an option that takes no value.</summary>
    /// <param name="longName">The long name without its leading <c>--</c>.</param>
    /// <param name="shortName">The short letter, or <see langword="null"/> when there is none.</param>
    /// <param name="set">Sets the flag on the options being filled in.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="longName"/> or <paramref name="set"/> is <see langword="null"/>.</exception>
    public static CommandLineOption Flag(string longName, char? shortName, Action<CommandLineOptions> set)
    {
        ArgumentNullException.ThrowIfNull(longName);
        ArgumentNullException.ThrowIfNull(set);

        return new CommandLineOption(longName, shortName, takesValue: false, (options, _, _) =>
        {
            set(options);
            return null;
        });
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

        return new CommandLineOption(longName, shortName, takesValue: true, (options, value, spelledOption) =>
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
