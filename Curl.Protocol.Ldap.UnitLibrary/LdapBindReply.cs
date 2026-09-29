namespace Curl.Protocol.Ldap;

/// <summary>The server's answer to one BindRequest.</summary>
/// <param name="Status">Whether a BindResponse arrived, and if not, why.</param>
/// <param name="ResultCode">The BindResponse's resultCode (RFC 4511 section 4.1.9); 0 unless <see cref="LdapBindReplyStatus.Answered" />.</param>
internal readonly record struct LdapBindReply(LdapBindReplyStatus Status, int ResultCode)
{
    /// <summary>Gets a value indicating whether the bind succeeded: a BindResponse with resultCode <c>success</c> (0).</summary>
    public bool IsSuccess => Status == LdapBindReplyStatus.Answered && ResultCode == 0;
}
