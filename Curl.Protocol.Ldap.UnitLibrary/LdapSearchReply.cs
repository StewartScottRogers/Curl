namespace Curl.Protocol.Ldap;

/// <summary>One thing the server sent, or failed to send, while a search was outstanding.</summary>
/// <param name="Kind">What it was.</param>
/// <param name="ResultCode">The SearchResultDone's resultCode (RFC 4511 section 4.1.9); 0 unless <see cref="LdapSearchReplyKind.Done" />.</param>
/// <param name="DiagnosticMessage">The SearchResultDone's diagnosticMessage, as UTF-8; empty unless <see cref="LdapSearchReplyKind.Done" />.</param>
/// <param name="Entry">The SearchResultEntry; <see langword="null" /> unless <see cref="LdapSearchReplyKind.Entry" />.</param>
internal readonly record struct LdapSearchReply(LdapSearchReplyKind Kind, int ResultCode, string DiagnosticMessage, LdapSearchEntry? Entry = null)
{
    /// <summary>Gets a value indicating whether the search ended as both builds count success: <c>success</c> (0) or <c>sizeLimitExceeded</c> (4).</summary>
    public bool IsSuccess => Kind == LdapSearchReplyKind.Done && ResultCode is 0 or 4;

    /// <summary>A reply that carries nothing but its kind.</summary>
    /// <param name="kind">What it was.</param>
    /// <returns>The reply.</returns>
    public static LdapSearchReply Of(LdapSearchReplyKind kind) => new(kind, 0, string.Empty);

    /// <summary>A SearchResultEntry for the search.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The reply.</returns>
    public static LdapSearchReply OfEntry(LdapSearchEntry entry) => new(LdapSearchReplyKind.Entry, 0, string.Empty, entry);
}
