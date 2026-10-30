using Curl.Extension.Abstractions;

namespace Curl.Extension.Composition;

/// <summary>
/// The one place a host learns which long command-line options Curl's extensions add beyond
/// upstream curl's (ADR-1006). The host hands <see cref="For" />'s options to the parser, which reads
/// them only where curl has no option of that name; none when extensions are off, so the parser then
/// refuses every such name exactly as curl refuses an unknown option.
/// </summary>
public static class ExtensionCommandLineOptions
{
    /// <summary>
    /// Gives every registered extension option, or none when <see cref="ExtensionProtocolHandlers.SwitchVariable" />
    /// is <c>0</c>, after checking that no two share a name and none has the name of one of curl's options.
    /// </summary>
    /// <param name="readEnvironmentVariable">Reads the run's environment.</param>
    /// <param name="isCurlLongOption">Reports whether a long name is one of curl's own options.</param>
    /// <returns>The options, in registration order.</returns>
    /// <exception cref="InvalidOperationException">An option name is null, empty, starts with a dash or holds an equals sign; two options share a name; or one has the name of a curl option.</exception>
    public static IReadOnlyList<ExtensionOption> For(Func<string, string?> readEnvironmentVariable, Func<string, bool> isCurlLongOption)
    {
        ArgumentNullException.ThrowIfNull(readEnvironmentVariable);
        ArgumentNullException.ThrowIfNull(isCurlLongOption);
        return readEnvironmentVariable(ExtensionProtocolHandlers.SwitchVariable) == "0"
            ? []
            : Checked(ExtensionOptionCatalogue.Entries, isCurlLongOption);
    }

    /// <summary>
    /// Gives <paramref name="options" /> unchanged once no two share a name and none has the name of one of
    /// curl's options; otherwise fails, naming both options.
    /// </summary>
    /// <param name="options">The extensions' options.</param>
    /// <param name="isCurlLongOption">Reports whether a long name is one of curl's own options.</param>
    /// <returns><paramref name="options" />.</returns>
    /// <exception cref="InvalidOperationException">An option name is null, empty, starts with a dash or holds an equals sign; two options share a name; or one has the name of a curl option.</exception>
    internal static IReadOnlyList<ExtensionOption> Checked(IReadOnlyList<ExtensionOption> options, Func<string, bool> isCurlLongOption)
    {
        Dictionary<string, ExtensionOption> byName = new(StringComparer.Ordinal);
        foreach (ExtensionOption option in options)
        {
            if (!IsSelectableName(option.Name))
            {
                throw new InvalidOperationException($"The extension option \"{option.Name}\" (\"{option.Help}\") has a name the command line can never select: a name is not empty, does not start with '-' and holds no '='.");
            }

            if (isCurlLongOption(option.Name))
            {
                throw new InvalidOperationException($"The extension option --{option.Name} (\"{option.Help}\") has the name of curl's option --{option.Name}.");
            }

            if (!byName.TryAdd(option.Name, option))
            {
                throw new InvalidOperationException($"The extension options \"{byName[option.Name].Help}\" and \"{option.Help}\" are both named --{option.Name}.");
            }
        }

        return options;
    }

    /// <summary>Reports whether the command line can select <paramref name="name" /> as <c>--name</c>: it is not empty, does not start with a dash and holds no equals sign.</summary>
    /// <param name="name">An extension option's name.</param>
    /// <returns><c>true</c> when the parser can match the name.</returns>
    private static bool IsSelectableName(string? name) =>
        !string.IsNullOrEmpty(name) && name[0] != '-' && !name.Contains('=');
}
