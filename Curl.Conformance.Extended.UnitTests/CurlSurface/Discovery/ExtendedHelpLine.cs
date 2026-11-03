namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Discovery;

/// <summary>
/// One line of <c>curl --extended-help</c>, <c>name: summary Schemes: a, b. Options: --x, --y.</c>, split into
/// its extension's name, schemes and long options (<c>none</c> is an empty list).
/// </summary>
/// <param name="Text">The whole line.</param>
/// <param name="Name">The extension's name.</param>
/// <param name="Schemes">The schemes it serves.</param>
/// <param name="Options">Its long options, each with its leading <c>--</c>.</param>
internal sealed record ExtendedHelpLine(string Text, string Name, IReadOnlyList<string> Schemes, IReadOnlyList<string> Options)
{
    private const string SchemesLabel = " Schemes: ";
    private const string OptionsLabel = ". Options: ";

    /// <summary>Splits <paramref name="line" />.</summary>
    internal static ExtendedHelpLine Parse(string line)
    {
        int options = line.LastIndexOf(OptionsLabel, StringComparison.Ordinal);
        int schemes = line.LastIndexOf(SchemesLabel, options, StringComparison.Ordinal);
        string schemeList = line[(schemes + SchemesLabel.Length)..options];
        string optionList = line[(options + OptionsLabel.Length)..].TrimEnd('.');
        return new(line, line[..line.IndexOf(": ", StringComparison.Ordinal)], ListOf(schemeList), ListOf(optionList));
    }

    private static string[] ListOf(string list) => list == "none" ? [] : list.Split(", ");
}
