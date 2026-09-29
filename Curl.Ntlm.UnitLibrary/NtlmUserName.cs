namespace Curl.Ntlm;

/// <summary>
/// Splits curl's <c>-u</c> user into the domain and user an AUTHENTICATE message carries,
/// as <c>Curl_auth_create_ntlm_type3_message</c> does: the part before the first
/// backslash, or failing that the first slash, is the domain.
/// </summary>
public static class NtlmUserName
{
    /// <summary>
    /// Splits <paramref name="userName" /> (<c>user</c>, <c>DOMAIN\user</c> or
    /// <c>DOMAIN/user</c>) into its domain, empty when there is none, and its user.
    /// </summary>
    public static (string Domain, string User) SplitDomain(string userName)
    {
        ArgumentNullException.ThrowIfNull(userName);
        int separator = userName.IndexOf('\\', StringComparison.Ordinal);
        if (separator < 0)
        {
            separator = userName.IndexOf('/', StringComparison.Ordinal);
        }

        return separator < 0 ? (string.Empty, userName) : (userName[..separator], userName[(separator + 1)..]);
    }
}
