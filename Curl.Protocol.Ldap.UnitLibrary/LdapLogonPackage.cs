namespace Curl.Protocol.Ldap;

/// <summary>The security package WinLDAP's logon bind asks for the logged-on user's tokens from.</summary>
public enum LdapLogonPackage
{
    /// <summary>SPNEGO, for the SASL <c>GSS-SPNEGO</c> bind: Kerberos where a domain offers it, raw NTLM otherwise.</summary>
    Negotiate,

    /// <summary>NTLM, for the Sicily bind a server that offers no <c>GSS-SPNEGO</c> is sent.</summary>
    Ntlm,
}
