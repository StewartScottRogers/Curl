namespace Curl.Protocol.Smb;

/// <summary>
/// The user and domain curl 8.21.0 sends in the session setup, split from the user name
/// as <c>smb_connect</c> splits it.
/// </summary>
/// <param name="User">The user name after the separator, or the whole name when there is none.</param>
/// <param name="Domain">The domain before the separator, or the URL's host name when there is none.</param>
internal sealed record SmbIdentity(string User, string Domain)
{
    /// <summary>
    /// Splits <paramref name="userName" /> at its first <c>/</c>, or its first <c>\</c>
    /// when it has no <c>/</c>. With neither, the host name stands in for the domain.
    /// </summary>
    /// <remarks>
    /// curl looks for <c>/</c> first, unlike its HTTP NTLM, which looks for <c>\</c>
    /// first (<see cref="Curl.Ntlm.NtlmUserName" />), so <c>a/b\c</c> is user <c>b\c</c>
    /// in domain <c>a</c> here.
    /// </remarks>
    /// <param name="userName">The user name from <c>-u</c> or the URL.</param>
    /// <param name="hostName">The host name the transfer connects to.</param>
    /// <returns>The identity to send.</returns>
    public static SmbIdentity Split(string userName, string hostName)
    {
        int separator = userName.IndexOf('/', StringComparison.Ordinal);
        if (separator < 0)
        {
            separator = userName.IndexOf('\\', StringComparison.Ordinal);
        }

        return separator < 0
            ? new SmbIdentity(userName, hostName)
            : new SmbIdentity(userName[(separator + 1)..], userName[..separator]);
    }
}
