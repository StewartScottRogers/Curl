using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Writes an LDAP transfer's steps to Curl's diagnostic log under the <c>ldap</c> component
/// (ADR-0222): each LDAPMessage sent and each search reply received, by operation and
/// messageID, at <c>verbose</c>; the bind, the search's base, scope and filter, the entries it
/// returned and the transfer's end at <c>info</c>; and the failure that ends the transfer,
/// with the build's text for the LDAP result code, at <c>error</c>.
/// </summary>
/// <remarks>
/// Every line tests <see cref="IDiagnosticLog.IsEnabled" /> before it is formatted. A simple
/// bind's password and a logon bind's tokens are never written: a bind is named by its DN only.
/// </remarks>
/// <param name="diagnosticLog">Where the lines are written.</param>
internal sealed class LdapTransferLog(IDiagnosticLog diagnosticLog)
{
    /// <summary>RFC 4511's names for the scopes 0 to 3.</summary>
    private static readonly string[] ScopeNames = ["baseObject", "singleLevel", "wholeSubtree", "subordinateSubtree"];

    /// <summary>Writes that an LDAPMessage was sent, by operation and messageID, at <c>verbose</c>.</summary>
    /// <param name="operation">The operation, such as <c>BindRequest</c>.</param>
    /// <param name="messageId">The message's messageID.</param>
    public void Sent(string operation, int messageId)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, $"sent {operation} with message ID {messageId.ToString(CultureInfo.InvariantCulture)}");
        }
    }

    /// <summary>Writes what arrived for the search with <paramref name="messageId" />, at <c>verbose</c>.</summary>
    /// <param name="reply">The reply.</param>
    /// <param name="messageId">The SearchRequest's messageID.</param>
    public void SearchReplyReceived(LdapSearchReply reply, int messageId)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, $"received {Describe(reply.Kind)} for message ID {messageId.ToString(CultureInfo.InvariantCulture)}");
        }
    }

    /// <summary>Writes the bind that succeeded, at <c>info</c>.</summary>
    /// <param name="how">How the session bound: its DN, anonymously, or as the logged-on user.</param>
    public void Bound(string how)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, $"bound {how}");
        }
    }

    /// <summary>Writes the search's base, scope and filter, at <c>info</c>.</summary>
    /// <param name="search">The search the URL names.</param>
    public void Searching(LdapSearchParameters search)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, $"search base \"{search.BaseObject}\", scope {ScopeNames[search.Scope]}, filter {search.Filter ?? "(objectClass=*)"}");
        }
    }

    /// <summary>Writes how many entries the search returned, at <c>info</c>.</summary>
    /// <param name="entries">The entries that arrived and were written or held.</param>
    public void EntriesReturned(int entries)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, $"search returned {entries.ToString(CultureInfo.InvariantCulture)} entries");
        }
    }

    /// <summary>
    /// Writes the transfer's end: its bytes and elapsed milliseconds at <c>info</c> when it
    /// succeeded, otherwise its <see cref="CurlExitCode" /> and message at <c>error</c>.
    /// </summary>
    /// <param name="result">The transfer's outcome.</param>
    /// <param name="elapsed">How long the transfer took.</param>
    public void Ended(TransferResult result, TimeSpan elapsed)
    {
        if (result.IsSuccess)
        {
            Done(result.BytesTransferred, elapsed);
        }
        else
        {
            Failed(result);
        }
    }

    private static string Describe(LdapSearchReplyKind kind) => kind switch
    {
        LdapSearchReplyKind.Done => "SearchResultDone",
        LdapSearchReplyKind.Entry => "SearchResultEntry",
        LdapSearchReplyKind.OtherResponse => "another response",
        LdapSearchReplyKind.OtherMessage => "a message for another request",
        _ => "nothing: the server closed or sent no LDAPMessage",
    };

    private void Done(long bytes, TimeSpan elapsed)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, $"transfer done: {bytes.ToString(CultureInfo.InvariantCulture)} bytes in {((long)elapsed.TotalMilliseconds).ToString(CultureInfo.InvariantCulture)} ms");
        }
    }

    private void Failed(TransferResult result)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Error))
        {
            Write(DiagnosticLogLevel.Error, $"transfer failed with {result.ExitCode} (exit {((int)result.ExitCode).ToString(CultureInfo.InvariantCulture)}): {result.ErrorMessage}");
        }
    }

    private void Write(DiagnosticLogLevel level, string message) =>
        diagnosticLog.Write(level, DiagnosticLogComponents.Ldap, message);
}
