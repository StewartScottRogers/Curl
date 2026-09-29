namespace Curl.Kerberos;

/// <summary>
/// Finds and reads <c>krb5.conf</c> as MIT Kerberos does: the colon-separated files
/// <c>KRB5_CONFIG</c> names, else <see cref="DefaultConfigurationPath" />; a file that does
/// not exist is skipped, and when none exists the configuration is empty (ADR-0160).
/// </summary>
/// <param name="files">Reads the configuration files.</param>
/// <param name="readEnvironmentVariable">Reads an environment variable; <see langword="null" /> when unset.</param>
public sealed class KerberosConfigurationStore(IKerberosFileReader files, Func<string, string?> readEnvironmentVariable)
{
    /// <summary>The environment variable that names the configuration files.</summary>
    public const string ConfigurationVariable = "KRB5_CONFIG";

    /// <summary>MIT's built-in configuration file.</summary>
    public const string DefaultConfigurationPath = "/etc/krb5.conf";

    /// <summary>
    /// Gets the configuration files in the order they are read: <c>KRB5_CONFIG</c> split on
    /// colons, empty parts dropped, when set; otherwise <see cref="DefaultConfigurationPath" />.
    /// </summary>
    /// <returns>The files' paths; earlier files win when a relation is read for one value.</returns>
    public IReadOnlyList<string> ConfigurationPaths()
    {
        string? named = readEnvironmentVariable(ConfigurationVariable);
        return named is null ? [DefaultConfigurationPath] : named.Split(':', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>Reads every file <see cref="ConfigurationPaths" /> names into one configuration.</summary>
    /// <returns>The configuration; empty when no file exists.</returns>
    /// <exception cref="KerberosConfigurationException">A file, or a file it includes, is malformed.</exception>
    public KerberosConfiguration Read()
    {
        KerberosConfigurationNode root = new(string.Empty, null);
        KerberosConfigurationReader reader = new(files);
        foreach (string path in ConfigurationPaths())
        {
            reader.ReadFile(path, root);
        }

        return new KerberosConfiguration(root);
    }
}
