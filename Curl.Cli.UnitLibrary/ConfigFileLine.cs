namespace Curl.Cli;

/// <summary>
/// One line of a <c>-K</c> / <c>--config</c> file that is neither blank nor a comment, split the way
/// curl 8.21.0 splits it by <see cref="ConfigFileSyntax.ReadLines"/>.
/// </summary>
/// <param name="Number">
/// The line's number as curl reports it: blank and comment lines are not counted, so the first line
/// holding an option is 1 wherever it sits in the file.
/// </param>
/// <param name="Option">The option as written: a long name with or without its <c>--</c>, or a <c>-</c> and short letters.</param>
/// <param name="Parameter">The parameter, unquoted and unescaped; <see langword="null"/> when the line has none, empty for <c>""</c>.</param>
/// <param name="WarningLines">The warning lines curl prints for the line before applying it, wrapped as curl wraps them; empty when there are none.</param>
internal sealed class ConfigFileLine(int Number, string Option, string? Parameter, IReadOnlyList<string> WarningLines)
{
    /// <summary>The line's number as curl reports it.</summary>
    public int Number { get; } = Number;

    /// <summary>The option as written.</summary>
    public string Option { get; } = Option;

    /// <summary>The parameter, or <see langword="null"/> when the line has none.</summary>
    public string? Parameter { get; } = Parameter;

    /// <summary>The warning lines curl prints for the line before applying it.</summary>
    public IReadOnlyList<string> WarningLines { get; } = WarningLines;
}
