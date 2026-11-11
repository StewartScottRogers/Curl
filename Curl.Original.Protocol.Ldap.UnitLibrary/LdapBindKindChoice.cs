using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Chooses the WinLDAP dialect's <see cref="LdapBindKind" /> from the auth mask and whether
/// <c>-u</c> gave credentials, as curl 8.22.0's Schannel build does (ADR-1244): a simple bind
/// only with credentials and Basic in the mask, so <c>--ntlm</c>, <c>--digest</c> and
/// <c>--negotiate</c> never send the password in clear; otherwise the first of Negotiate, NTLM
/// and Digest the mask holds, or the logged-on user when there are no credentials or the mask
/// holds none of them.
/// </summary>
public static class LdapBindKindChoice
{
    /// <summary>The bind kind for <paramref name="schemes" /> when <paramref name="hasCredentials" /> says whether <c>-u</c> was given.</summary>
    /// <param name="schemes">The auth mask: <c>--basic</c>, <c>--digest</c>, <c>--ntlm</c>, <c>--negotiate</c> or <c>--anyauth</c>; Basic by default.</param>
    /// <param name="hasCredentials">Whether <c>-u</c> gave a user and password.</param>
    /// <returns>How the bind is made.</returns>
    public static LdapBindKind Choose(HttpAuthSchemes schemes, bool hasCredentials) =>
        !hasCredentials ? LdapBindKind.LogonUser
        : schemes.HasFlag(HttpAuthSchemes.Basic) ? LdapBindKind.Simple
        : ChooseAuthenticatedBind(schemes);

    /// <summary><c>ldap_win_bind_auth</c>'s order: Negotiate, then NTLM, then Digest, else the logged-on user.</summary>
    private static LdapBindKind ChooseAuthenticatedBind(HttpAuthSchemes schemes) =>
        schemes.HasFlag(HttpAuthSchemes.Negotiate) ? LdapBindKind.Negotiate
        : schemes.HasFlag(HttpAuthSchemes.Ntlm) ? LdapBindKind.Ntlm
        : schemes.HasFlag(HttpAuthSchemes.Digest) ? LdapBindKind.Digest
        : LdapBindKind.LogonUser;
}
