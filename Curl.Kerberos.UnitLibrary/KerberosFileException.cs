using System.Globalization;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="KerberosFileException" /> class for a KCM
    /// daemon's refusal.
    /// </summary>
    /// <param name="error">Why the cache could not be read.</param>
    /// <param name="kcmStatus">The status code the KCM daemon answered with.</param>
    public KerberosFileException(KerberosFileError error, int kcmStatus)
        : base(string.Create(CultureInfo.InvariantCulture, $"Kerberos file could not be read: {error} (KCM status {kcmStatus})."))
    {
        Error = error;
        KcmStatus = kcmStatus;
    }

    /// <summary>Gets why the file could not be read.</summary>
    public KerberosFileError Error { get; }

    /// <summary>Gets the KCM daemon's non-zero status code, or <see langword="null" /> when no KCM daemon refused anything.</summary>
    public int? KcmStatus { get; }
}
