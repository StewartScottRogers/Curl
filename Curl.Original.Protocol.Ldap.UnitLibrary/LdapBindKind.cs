namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// How the WinLDAP dialect binds, as curl's <c>ldap_win_bind</c> and <c>ldap_win_bind_auth</c>
/// choose from <c>-u</c> and the auth mask (ADR-1244).
/// </summary>
public enum LdapBindKind
{
    /// <summary>A simple bind (<c>[0]</c>, tag <c>0x80</c>) carrying <c>-u</c>'s password: only when the mask holds Basic.</summary>
    Simple,

    /// <summary><c>LDAP_AUTH_NEGOTIATE</c> with <c>-u</c>'s credentials: <c>GSS-SPNEGO</c> or the Sicily NTLM bind.</summary>
    Negotiate,

    /// <summary><c>LDAP_AUTH_NTLM</c> with <c>-u</c>'s credentials: the Sicily bind named <c>NTLM</c> (tag <c>0x8a</c>) with an NTLMSSP payload.</summary>
    Ntlm,

    /// <summary><c>LDAP_AUTH_DIGEST</c> with <c>-u</c>'s credentials: the SASL bind (tag <c>0xa3</c>) with the mechanism <c>DIGEST-MD5</c>.</summary>
    Digest,

    /// <summary><c>LDAP_AUTH_NEGOTIATE</c> with no credentials: the bind as the logged-on user.</summary>
    LogonUser,
}
