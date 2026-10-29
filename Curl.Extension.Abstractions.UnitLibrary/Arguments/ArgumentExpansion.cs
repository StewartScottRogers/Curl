namespace Curl.Extension.Abstractions.Arguments;

/// <summary>
/// Replaces each expander's option and its value in a command line with the expander's arguments, before the host
/// parses it (ADR-1037). An option is read as the parser reads a long option: <c>--name value</c>, or
/// <c>--name=value</c>; a flag takes no value. Nothing after a bare <c>--</c> is expanded, an option with no value
/// after it is left for the parser to refuse, and the arguments an expander gives are not expanded again.
/// </summary>
public static class ArgumentExpansion
{
    /// <summary>The argument after which every argument is a URL, as curl reads it.</summary>
    private const string EndOfOptions = "--";

    /// <summary>Gives <paramref name="arguments" /> with each expander's option and value replaced by its arguments.</summary>
    /// <param name="arguments">The command-line arguments, without the program name.</param>
    /// <param name="expanders">The expanders; none gives <paramref name="arguments" /> back unchanged.</param>
    /// <param name="context">What the expanders read the files and environment through.</param>
    /// <returns>The expanded arguments.</returns>
    /// <exception cref="ArgumentExpansionException">An expander could not expand its option's value.</exception>
    public static IReadOnlyList<string> Expand(IReadOnlyList<string> arguments, IReadOnlyList<IArgumentExpander> expanders, ArgumentExpansionContext context)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(expanders);
        ArgumentNullException.ThrowIfNull(context);
        if (expanders.Count == 0)
        {
            return arguments;
        }

        Dictionary<string, IArgumentExpander> byName = expanders.ToDictionary(expander => expander.Option.Name, StringComparer.Ordinal);
        List<string> expanded = new(arguments.Count);
        int index = 0;
        while (index < arguments.Count && arguments[index] != EndOfOptions)
        {
            index = ExpandAt(arguments, index, byName, context, expanded);
        }

        expanded.AddRange(arguments.Skip(index));
        return expanded;
    }

    /// <summary>
    /// Adds the argument at <paramref name="index" /> to <paramref name="expanded" />, or the expansion of the
    /// expander's option it names and its value.
    /// </summary>
    /// <returns>The index of the next argument to read.</returns>
    private static int ExpandAt(IReadOnlyList<string> arguments, int index, Dictionary<string, IArgumentExpander> byName, ArgumentExpansionContext context, List<string> expanded)
    {
        string argument = arguments[index];
        int equals = argument.IndexOf('=', StringComparison.Ordinal);
        string name = argument.StartsWith(EndOfOptions, StringComparison.Ordinal) ? (equals >= 0 ? argument[2..equals] : argument[2..]) : string.Empty;
        if (!byName.TryGetValue(name, out IArgumentExpander? expander))
        {
            expanded.Add(argument);
            return index + 1;
        }

        (string? value, int next) = ValueOf(expander.Option, arguments, index, equals);
        if (value is null)
        {
            // No value follows: the parser refuses the option as curl refuses a missing one.
            expanded.Add(argument);
            return next;
        }

        expanded.AddRange(expander.ArgumentsFor(value, context));
        return next;
    }

    /// <summary>
    /// Gives the value of the option at <paramref name="index" />, as the parser reads it: the empty string for a
    /// flag, the attached value, else the next argument, else <see langword="null" />.
    /// </summary>
    /// <returns>The value, and the index of the argument after the option and its value.</returns>
    private static (string? Value, int Next) ValueOf(ExtensionOption option, IReadOnlyList<string> arguments, int index, int equals)
    {
        if (option.Argument == ExtensionOptionArgument.None)
        {
            return (string.Empty, index + 1);
        }

        if (equals >= 0)
        {
            return (arguments[index][(equals + 1)..], index + 1);
        }

        return index + 1 < arguments.Count ? (arguments[index + 1], index + 2) : (null, index + 1);
    }
}
