namespace Curl.Kerberos;

/// <summary>
/// Thrown when a <c>krb5.conf</c> file cannot be read or one of its values cannot be used.
/// <see cref="Error" /> says why.
/// </summary>
public sealed class KerberosConfigurationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KerberosConfigurationException" /> class.
    /// </summary>
    /// <param name="error">Why the file or value could not be used.</param>
    /// <param name="detail">The file and line, or the value, that failed.</param>
    public KerberosConfigurationException(KerberosConfigurationError error, string detail)
        : base($"Kerberos configuration could not be used: {error} ({detail}).")
    {
        Error = error;
    }

    /// <summary>Gets why the file or value could not be used.</summary>
    public KerberosConfigurationError Error { get; }
}
