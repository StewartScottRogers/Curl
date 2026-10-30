namespace Curl.Extension.Abstractions.Arguments;

/// <summary>
/// Replaces each expander's option and its value in a command line with the expander's arguments, before the host
/// parses it (ADR-1040). An option is read as the parser reads a long option: <c>--name value</c>, or
/// <c>--name=value</c>; a flag takes no value. Nothing after a bare <c>--</c> is expanded, an option with no value
/// after it is left for the parser to refuse, and the arguments an expander gives are not expanded again. An
/// <see cref="ITrailingArgumentExpander" /> also takes, from the rest of its <c>--next</c> group, its companion
/// options with their values and the positional arguments it claims (ADR-1051); a positional argument right after
/// an option that may take it as its value is never claimed.
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
        return expanders.Count == 0 ? arguments : new ExpansionPass(arguments, expanders, context).Expanded();
    }

    /// <summary>
    /// Gives the value of the option at <paramref name="index" />, as the parser reads it: the empty string for a
    /// flag, the attached value, else the next argument, else <see langword="null" />.
    /// </summary>
    /// <returns>The value, and the index of the argument after the option and its value.</returns>
    private static (string? Value, int Next) ValueOf(ExtensionOption option, IReadOnlyList<string> arguments, int index)
    {
        if (option.Argument == ExtensionOptionArgument.None)
        {
            return (string.Empty, index + 1);
        }

        int equals = arguments[index].IndexOf('=', StringComparison.Ordinal);
        if (equals >= 0)
        {
            return (arguments[index][(equals + 1)..], index + 1);
        }

        return index + 1 < arguments.Count ? (arguments[index + 1], index + 2) : (null, index + 1);
    }

    /// <summary>Gives the name of the long option <paramref name="argument" /> is, or the empty string for any other argument.</summary>
    private static string LongNameOf(string argument)
    {
        if (!argument.StartsWith(EndOfOptions, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        int equals = argument.IndexOf('=', StringComparison.Ordinal);
        return equals >= 0 ? argument[2..equals] : argument[2..];
    }

    /// <summary>
    /// Tells whether <paramref name="argument" /> is an option that may take the next argument as its value: any
    /// argument starting with <c>-</c> but <c>-</c> itself and a long option with an attached value.
    /// </summary>
    private static bool MayTakeTheNextArgument(string argument) =>
        argument.Length > 1
        && argument[0] == '-'
        && !(argument.StartsWith(EndOfOptions, StringComparison.Ordinal) && argument.Contains('=', StringComparison.Ordinal));

    /// <summary>One pass over a command line, holding each trailing expansion of the current <c>--next</c> group until the group ends.</summary>
    private sealed class ExpansionPass(IReadOnlyList<string> arguments, IReadOnlyList<IArgumentExpander> expanders, ArgumentExpansionContext context)
    {
        private readonly Dictionary<string, IArgumentExpander> byName = expanders.ToDictionary(expander => expander.Option.Name, StringComparer.Ordinal);

        private readonly List<string> expanded = new(arguments.Count);

        private readonly List<HeldExpansion> group = [];

        /// <summary>Whether the last argument kept is an option that may take the next argument as its value.</summary>
        private bool previousMayTakeTheNextArgument;

        /// <summary>Gives the expanded arguments.</summary>
        public IReadOnlyList<string> Expanded()
        {
            int index = 0;
            while (index < arguments.Count && arguments[index] != EndOfOptions)
            {
                index = ExpandAt(index);
            }

            EndGroup();
            expanded.AddRange(arguments.Skip(index));
            return expanded;
        }

        /// <summary>Reads the argument at <paramref name="index" />, and gives the index of the next argument to read.</summary>
        private int ExpandAt(int index)
        {
            string argument = arguments[index];
            if (argument is "--next" or "-:")
            {
                EndGroup();
                return Keep(index);
            }

            string name = LongNameOf(argument);
            if (byName.TryGetValue(name, out IArgumentExpander? expander))
            {
                return ExpandOption(expander, index);
            }

            return TakeOrKeep(name, index);
        }

        /// <summary>
        /// Gives the argument at <paramref name="index" />, named <paramref name="name" /> when it is a long option, to a
        /// held expansion as a companion or a claimed positional argument, or else keeps it.
        /// </summary>
        /// <returns>The index of the next argument to read.</returns>
        private int TakeOrKeep(string name, int index) =>
            TakeCompanion(name, index) ?? TakePositional(arguments[index], index) ?? Keep(index);

        /// <summary>Expands the expander's option at <paramref name="index" />, or holds it to the end of its group when it is a trailing one.</summary>
        private int ExpandOption(IArgumentExpander expander, int index)
        {
            (string? value, int next) = ValueOf(expander.Option, arguments, index);
            if (value is null)
            {
                // No value follows: the parser refuses the option as curl refuses a missing one.
                return Keep(index);
            }

            if (expander is ITrailingArgumentExpander trailing)
            {
                group.Add(new HeldExpansion(trailing, value, expanded.Count));
            }
            else
            {
                expanded.AddRange(expander.ArgumentsFor(value, context));
            }

            previousMayTakeTheNextArgument = false;
            return next;
        }

        /// <summary>Gives the last held expansion that names it the companion option named <paramref name="name" />, with its value.</summary>
        /// <returns>The index of the next argument, or <see langword="null" /> when no held expansion takes it.</returns>
        private int? TakeCompanion(string name, int index)
        {
            for (int held = group.Count - 1; held >= 0; held--)
            {
                ExtensionOption? companion = group[held].CompanionNamed(name);
                if (companion is null)
                {
                    continue;
                }

                (string? value, int next) = ValueOf(companion, arguments, index);
                if (value is null)
                {
                    return null;
                }

                group[held].CompanionValues.Add(new CompanionOptionValue(companion, value));
                previousMayTakeTheNextArgument = false;
                return next;
            }

            return null;
        }

        /// <summary>Gives the last held expansion that claims it the positional argument at <paramref name="index" />.</summary>
        /// <returns>The index of the next argument, or <see langword="null" /> when none claims it.</returns>
        private int? TakePositional(string argument, int index)
        {
            if (previousMayTakeTheNextArgument || argument.StartsWith('-'))
            {
                return null;
            }

            HeldExpansion? claimer = group.FindLast(held => held.Expander.ClaimsPositionalArgument(argument));
            if (claimer is null)
            {
                return null;
            }

            claimer.PositionalArguments.Add(argument);
            return index + 1;
        }

        /// <summary>Keeps the argument at <paramref name="index" /> as it is.</summary>
        private int Keep(int index)
        {
            expanded.Add(arguments[index]);
            previousMayTakeTheNextArgument = MayTakeTheNextArgument(arguments[index]);
            return index + 1;
        }

        /// <summary>Puts each held expansion's arguments in its option's place, last first so the earlier places stay put.</summary>
        private void EndGroup()
        {
            for (int held = group.Count - 1; held >= 0; held--)
            {
                HeldExpansion expansion = group[held];
                ClaimedArguments claimed = new(expansion.Value, expansion.CompanionValues, expansion.PositionalArguments);
                expanded.InsertRange(expansion.Place, expansion.Expander.ArgumentsFor(claimed, context));
            }

            group.Clear();
        }
    }

    /// <summary>A trailing expander's option, held with what it takes until its group ends.</summary>
    /// <param name="Expander">The expander.</param>
    /// <param name="Value">Its option's value.</param>
    /// <param name="Place">The index in the expanded arguments its arguments go to.</param>
    private sealed record HeldExpansion(ITrailingArgumentExpander Expander, string Value, int Place)
    {
        /// <summary>Gets the companion options taken so far, in command-line order.</summary>
        public List<CompanionOptionValue> CompanionValues { get; } = [];

        /// <summary>Gets the positional arguments claimed so far, in command-line order.</summary>
        public List<string> PositionalArguments { get; } = [];

        /// <summary>Gives the expander's companion option named <paramref name="name" />, or <see langword="null" />.</summary>
        public ExtensionOption? CompanionNamed(string name) =>
            Expander.CompanionOptions.FirstOrDefault(option => option.Name == name);
    }
}
