namespace Curl.Extension.Abstractions.Arguments;

/// <summary>
/// Turns one extension option's value into curl command-line arguments (ADR-1040), so an extension can supply
/// whole requests: URLs, <c>-X</c>, <c>-H</c>, <c>--data-binary</c> and <c>--next</c>. A host replaces the option
/// and its value with <see cref="ArgumentsFor" />'s arguments before it parses the command line
/// (<see cref="ArgumentExpansion.Expand" />). An extension declares it in one file and registers it with one line in
/// <c>Curl.Extension.Composition</c>'s <c>ExtensionArgumentExpanderCatalogue</c>; its <see cref="Option" /> stays
/// registered in <c>ExtensionOptionCatalogue</c> too, so <c>--extensions-help</c> lists it.
/// </summary>
public interface IArgumentExpander
{
    /// <summary>Gets the option this expander replaces.</summary>
    ExtensionOption Option { get; }

    /// <summary>Gives the arguments the option's value stands for, parsed in the option's place.</summary>
    /// <param name="value">The option's value; the empty string for a flag.</param>
    /// <param name="context">Reads the files and environment variables the expansion needs.</param>
    /// <returns>The arguments, in order; none removes the option.</returns>
    /// <exception cref="ArgumentExpansionException">The value cannot be expanded; the host ends the run with its message and exit code.</exception>
    IReadOnlyList<string> ArgumentsFor(string value, ArgumentExpansionContext context);
}
