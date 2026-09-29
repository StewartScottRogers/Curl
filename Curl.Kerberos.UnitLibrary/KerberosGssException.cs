namespace Curl.Kerberos;

/// <summary>
/// Thrown when a GSS-API Kerberos context fails or refuses a per-message token.
/// <see cref="Error" /> says why.
/// </summary>
public sealed class KerberosGssException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="KerberosGssException" /> class.</summary>
    /// <param name="error">Why the context failed.</param>
    /// <param name="kerberosErrorCode">The acceptor's KRB-ERROR code, for <see cref="KerberosGssError.AcceptorError" />.</param>
    public KerberosGssException(KerberosGssError error, int? kerberosErrorCode = null)
        : base($"GSS-API Kerberos failed: {error}.")
    {
        Error = error;
        KerberosErrorCode = kerberosErrorCode;
    }

    /// <summary>Gets why the context failed.</summary>
    public KerberosGssError Error { get; }

    /// <summary>Gets the acceptor's KRB-ERROR code; <see langword="null" /> unless <see cref="Error" /> is <see cref="KerberosGssError.AcceptorError" />.</summary>
    public int? KerberosErrorCode { get; }
}
