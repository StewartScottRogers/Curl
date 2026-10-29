namespace Curl.Extension.Abstractions.Arguments;

/// <summary>What an <see cref="ITrailingArgumentExpander" /> took from its <c>--next</c> group (ADR-1051).</summary>
/// <param name="Value">The expander's option's value; the empty string for a flag.</param>
/// <param name="CompanionValues">Each companion option given after the expander's option, with its value, in command-line order.</param>
/// <param name="PositionalArguments">The positional arguments the expander claimed, in command-line order.</param>
public sealed record ClaimedArguments(string Value, IReadOnlyList<CompanionOptionValue> CompanionValues, IReadOnlyList<string> PositionalArguments);
