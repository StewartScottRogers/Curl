namespace Curl.Kerberos;

/// <summary>Why a ticket could not be got from the KDC or the credential cache.</summary>
public enum KerberosKdcError
{
    /// <summary>The KDC answered with a KRB-ERROR this enumeration has no name for; <see cref="KerberosKdcException.KdcErrorCode" /> holds its code.</summary>
    KdcRefused,

    /// <summary><c>KDC_ERR_C_PRINCIPAL_UNKNOWN</c> (6): the KDC does not know the client.</summary>
    ClientPrincipalUnknown,

    /// <summary><c>KDC_ERR_S_PRINCIPAL_UNKNOWN</c> (7): the KDC does not know the service.</summary>
    ServerPrincipalUnknown,

    /// <summary><c>KDC_ERR_ETYPE_NOSUPP</c> (14), or no encryption type the KDC offers is one this library has.</summary>
    EncryptionTypeNotSupported,

    /// <summary><c>KDC_ERR_CLIENT_REVOKED</c> (18): the client's account is disabled or locked.</summary>
    ClientRevoked,

    /// <summary><c>KDC_ERR_KEY_EXPIRED</c> (23): the client's password has expired.</summary>
    PasswordExpired,

    /// <summary><c>KDC_ERR_PREAUTH_FAILED</c> (24): the password is wrong.</summary>
    PreAuthenticationFailed,

    /// <summary><c>KDC_ERR_PREAUTH_REQUIRED</c> (25) in answer to a request that already pre-authenticated.</summary>
    PreAuthenticationRequired,

    /// <summary><c>KRB_AP_ERR_TKT_EXPIRED</c> (32): the ticket-granting ticket has expired.</summary>
    TicketExpired,

    /// <summary><c>KRB_AP_ERR_SKEW</c> (37): the client's clock is too far from the KDC's.</summary>
    ClockSkew,

    /// <summary>The AS-REP did not decrypt in the key made from the password: the password is wrong.</summary>
    ReplyIntegrityCheckFailed,

    /// <summary>The reply is not the one asked for: another message, a different nonce, another service's ticket, or bytes that do not decode.</summary>
    UnexpectedReply,

    /// <summary>The realm has no KDC that can be reached over UDP or TCP.</summary>
    NoKdc,

    /// <summary>Every KDC of the realm failed to answer.</summary>
    KdcUnreachable,

    /// <summary>No password was given and the credential cache holds neither the service ticket nor a ticket-granting ticket.</summary>
    NoCredentials,
}
