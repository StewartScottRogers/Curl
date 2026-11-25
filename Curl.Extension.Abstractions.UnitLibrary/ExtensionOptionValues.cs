namespace Curl.Extension.Abstractions;

/// <summary>
/// The extension options one option group of a command line gave, which an extension reads at
/// transfer time: the value of each option given, or the empty string for a flag (ADR-1006).
/// </summary>
/// <param name="valuesByName">Each given option's value by its long name; the last one given wins.</param>
/// <param name="tlsTrust">The transfer's TLS trust settings; <see langword="null" /> for the system store with verification.</param>
public sealed class ExtensionOptionValues(IReadOnlyDictionary<string, string> valuesByName, ExtensionTlsTrust? tlsTrust = null)
{
    /// <summary>Gets the transfer's TLS trust settings (<c>--cacert</c>, <c>--capath</c>, <c>-k</c>).</summary>
    public ExtensionTlsTrust TlsTrust { get; } = tlsTrust ?? new ExtensionTlsTrust();

    /// <summary>Gets the values of a command line that gave no extension option.</summary>
    public static ExtensionOptionValues None { get; } = new(new Dictionary<string, string>());

    /// <summary>Reports whether the command line gave <paramref name="option" />.</summary>
    /// <param name="option">The extension's option.</param>
    /// <returns><see langword="true" /> when it was given.</returns>
    public bool IsGiven(ExtensionOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        return valuesByName.ContainsKey(option.Name);
    }

    /// <summary>Gives the value the command line gave <paramref name="option" />.</summary>
    /// <param name="option">The extension's option.</param>
    /// <returns>The value, the empty string for a flag, or <see langword="null" /> when it was not given.</returns>
    public string? ValueOf(ExtensionOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        return valuesByName.GetValueOrDefault(option.Name);
    }
}
