namespace Curl.Extension.WireGuard.Configuration;

/// <summary>Collects the <c>key = value</c> lines of one section of a wg-quick configuration file.</summary>
internal interface IWireGuardSectionBuilder
{
    /// <summary>Takes one setting of the section, throwing <see cref="WireGuardConfigurationException" /> when it is unknown or invalid.</summary>
    /// <param name="key">The key as written; keys are case-insensitive.</param>
    /// <param name="value">The value, trimmed.</param>
    /// <param name="lineNumber">The one-based line the setting is on.</param>
    void AddSetting(string key, string value, int lineNumber);
}
