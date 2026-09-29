namespace Curl.Kerberos;

/// <summary>
/// Thrown when a Kerberos V5 message or one of the structures inside it cannot be decoded.
/// <see cref="Error" /> says why.
/// </summary>
public sealed class KerberosMessageException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KerberosMessageException" /> class.
    /// </summary>
    /// <param name="error">Why the message could not be decoded.</param>
    public KerberosMessageException(KerberosMessageError error)
        : base($"Kerberos message could not be decoded: {error}.")
    {
        Error = error;
    }

    /// <summary>Gets why the message could not be decoded.</summary>
    public KerberosMessageError Error { get; }
}
