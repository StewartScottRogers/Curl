namespace Curl.Kerberos;

/// <summary>
/// Thrown when a credential cache or keytab cannot be read. <see cref="Error" /> says why.
/// </summary>
public sealed class KerberosFileException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KerberosFileException" /> class.
    /// </summary>
    /// <param name="error">Why the file could not be read.</param>
    public KerberosFileException(KerberosFileError error)
        : base($"Kerberos file could not be read: {error}.")
    {
        Error = error;
    }

    /// <summary>Gets why the file could not be read.</summary>
    public KerberosFileError Error { get; }
}
