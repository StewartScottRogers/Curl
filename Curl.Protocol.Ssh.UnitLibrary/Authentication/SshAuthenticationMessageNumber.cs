namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// The user-authentication message numbers of RFC 4252 section 6 and RFC 4256 section 5
/// that this library reads or writes.
/// </summary>
internal static class SshAuthenticationMessageNumber
{
    /// <summary><c>SSH_MSG_USERAUTH_REQUEST</c>: one attempt to authenticate the user with one method.</summary>
    internal const byte Request = 50;

    /// <summary><c>SSH_MSG_USERAUTH_FAILURE</c>: the attempt failed; carries the methods that may continue and the partial-success flag.</summary>
    internal const byte Failure = 51;

    /// <summary><c>SSH_MSG_USERAUTH_SUCCESS</c>: the user is authenticated.</summary>
    internal const byte Success = 52;

    /// <summary><c>SSH_MSG_USERAUTH_BANNER</c>: text the server wants shown, which curl never shows.</summary>
    internal const byte Banner = 53;

    /// <summary>
    /// <c>SSH_MSG_USERAUTH_PASSWD_CHANGEREQ</c> (RFC 4252 section 8): the password is
    /// expired. It shares its number with <see cref="InfoRequest" />.
    /// </summary>
    internal const byte PasswordChangeRequest = 60;

    /// <summary>
    /// <c>SSH_MSG_USERAUTH_INFO_REQUEST</c> (RFC 4256 section 3.2): the prompts of one
    /// <c>keyboard-interactive</c> round.
    /// </summary>
    internal const byte InfoRequest = 60;

    /// <summary><c>SSH_MSG_USERAUTH_INFO_RESPONSE</c> (RFC 4256 section 3.4): the answers to one round's prompts.</summary>
    internal const byte InfoResponse = 61;
}
