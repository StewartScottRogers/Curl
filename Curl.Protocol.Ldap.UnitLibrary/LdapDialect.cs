namespace Curl.Protocol.Ldap;

/// <summary>
/// Which reference build of curl the LDAP handler answers as (ADR-0166): the Windows build
/// over WinLDAP, or the Linux and macOS builds over OpenLDAP's <c>libldap</c>. The two differ
/// on the wire, in their messages and in their exit codes.
/// </summary>
public enum LdapDialect
{
    /// <summary>
    /// curl's <c>lib/ldap.c</c> over WinLDAP: every constructed BER element carries a
    /// five-byte <c>84 00 00 00 nn</c> length, and a failed bind is retried once as LDAPv2
    /// before it fails with exit 38 and WinLDAP's text for the result.
    /// </summary>
    WinLdap,

    /// <summary>
    /// curl's <c>lib/openldap.c</c> over <c>libldap</c>: every BER length is the shortest,
    /// and a failed bind is not retried; <c>invalidCredentials</c> fails with exit 67 and
    /// every other result with exit 38.
    /// </summary>
    OpenLdap,
}
