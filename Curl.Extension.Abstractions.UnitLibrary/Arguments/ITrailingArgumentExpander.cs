namespace Curl.Extension.Abstractions.Arguments;

/// <summary>
/// An <see cref="IArgumentExpander" /> that also takes what follows its option in the same <c>--next</c> group
/// (ADR-1051): the values of its <see cref="CompanionOptions" />, and the positional arguments
/// <see cref="ClaimsPositionalArgument" /> accepts, such as <c>--openapi spec.json --operation getPet petId=5</c>.
/// <see cref="ArgumentExpansion.Expand" /> removes them from the command line and hands them to
/// <see cref="ArgumentsFor(ClaimedArguments, ArgumentExpansionContext)" /> beside the option's value; a positional
/// argument it does not accept stays a URL, as curl parses it.
/// </summary>
public interface ITrailingArgumentExpander : IArgumentExpander
{
    /// <summary>Gets the extension options whose values this expander takes, when they follow its option in its group.</summary>
    IReadOnlyList<ExtensionOption> CompanionOptions { get; }

    /// <summary>Tells whether this expander takes a positional argument that follows its option in its group.</summary>
    /// <param name="argument">The positional argument, which does not start with <c>-</c>.</param>
    /// <returns><see langword="true" /> to take it; <see langword="false" /> leaves it a URL.</returns>
    bool ClaimsPositionalArgument(string argument);

    /// <summary>Gives the arguments the option's value, its companions' values and its positional arguments stand for.</summary>
    /// <param name="claimed">What the expander took, each list in command-line order.</param>
    /// <param name="context">Reads the files and environment variables the expansion needs.</param>
    /// <returns>The arguments, in order, parsed in the option's place; none removes the option.</returns>
    /// <exception cref="ArgumentExpansionException">The arguments cannot be expanded; the host ends the run with its message and exit code.</exception>
    IReadOnlyList<string> ArgumentsFor(ClaimedArguments claimed, ArgumentExpansionContext context);

    /// <summary>Gives the arguments the option's value stands for alone, with no companion or positional argument.</summary>
    /// <param name="value">The option's value; the empty string for a flag.</param>
    /// <param name="context">Reads the files and environment variables the expansion needs.</param>
    /// <returns>The arguments, in order.</returns>
    IReadOnlyList<string> IArgumentExpander.ArgumentsFor(string value, ArgumentExpansionContext context) =>
        ArgumentsFor(new ClaimedArguments(value, [], []), context);
}
