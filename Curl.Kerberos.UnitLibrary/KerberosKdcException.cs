namespace Curl.Kerberos;

/// <summary>
/// Thrown when a ticket cannot be got. <see cref="Error" /> says why; for a KRB-ERROR,
/// <see cref="KdcErrorCode" /> and <see cref="KdcErrorText" /> hold what the KDC said.
/// </summary>
public sealed class KerberosKdcException : Exception
{
    private static readonly Dictionary<int, KerberosKdcError> ErrorsByCode = new()
    {
        [6] = KerberosKdcError.ClientPrincipalUnknown,
        [7] = KerberosKdcError.ServerPrincipalUnknown,
        [14] = KerberosKdcError.EncryptionTypeNotSupported,
        [18] = KerberosKdcError.ClientRevoked,
        [23] = KerberosKdcError.PasswordExpired,
        [24] = KerberosKdcError.PreAuthenticationFailed,
        [KerberosErrorMessage.PreAuthenticationRequired] = KerberosKdcError.PreAuthenticationRequired,
        [32] = KerberosKdcError.TicketExpired,
        [37] = KerberosKdcError.ClockSkew,
    };

    /// <summary>Initializes a new instance of the <see cref="KerberosKdcException" /> class.</summary>
    /// <param name="error">Why no ticket was got.</param>
    /// <param name="kdcErrorCode">The KRB-ERROR's code, or <see langword="null" /> when the KDC did not refuse.</param>
    /// <param name="kdcErrorText">The KRB-ERROR's text, or <see langword="null" /> when it gave none.</param>
    public KerberosKdcException(KerberosKdcError error, int? kdcErrorCode = null, string? kdcErrorText = null)
        : base($"Kerberos ticket could not be got: {error}.")
    {
        Error = error;
        KdcErrorCode = kdcErrorCode;
        KdcErrorText = kdcErrorText;
    }

    /// <summary>Gets why no ticket was got.</summary>
    public KerberosKdcError Error { get; }

    /// <summary>Gets the KRB-ERROR's code (RFC 4120 section 7.5.9), or <see langword="null" /> when the KDC did not refuse.</summary>
    public int? KdcErrorCode { get; }

    /// <summary>Gets the KRB-ERROR's text, e.g. MIT's <c>NEEDED_PREAUTH</c>, or <see langword="null" /> when it gave none.</summary>
    public string? KdcErrorText { get; }

    /// <summary>Turns a KDC's KRB-ERROR into the failure it means.</summary>
    /// <param name="message">The KRB-ERROR.</param>
    /// <returns>The exception to throw.</returns>
    internal static KerberosKdcException FromErrorMessage(KerberosErrorMessage message) =>
        new(ErrorOf(message.ErrorCode), message.ErrorCode, message.ErrorText);

    private static KerberosKdcError ErrorOf(int code) => ErrorsByCode.GetValueOrDefault(code, KerberosKdcError.KdcRefused);
}
