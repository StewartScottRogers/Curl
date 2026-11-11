namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// The method curl's <c>ldap_win_bind_auth</c> passes WinLDAP's <c>ldap_bind_s</c>: the first of
/// <c>--negotiate</c>, <c>--ntlm</c> and <c>--digest</c> the auth mask holds, or
/// <see cref="Negotiate" /> as the logged-on user when <c>-u</c> or all three are missing (ADR-1238).
/// </summary>
public enum WinLdapAuthMethod
{
    /// <summary><c>LDAP_AUTH_NEGOTIATE</c>: the rootDSE reads, then <c>GSS-SPNEGO</c> or the Sicily NTLM bind.</summary>
    Negotiate,

    /// <summary><c>LDAP_AUTH_NTLM</c>: <c>supportedCapabilities</c>, then the Sicily NTLM bind.</summary>
    Ntlm,

    /// <summary><c>LDAP_AUTH_DIGEST</c>: <c>supportedCapabilities</c> and <c>supportedSASLMechanisms</c>, then the SASL <c>DIGEST-MD5</c> bind.</summary>
    Digest,
}
