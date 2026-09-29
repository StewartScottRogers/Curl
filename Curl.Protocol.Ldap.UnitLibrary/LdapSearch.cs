using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Sends the search an LDAP URL names and reads it to its SearchResultDone, as each reference
/// build does, and fails as it fails (ADR-0166, measured by BL-587 against curl 8.21.0 with
/// WinLDAP and curl 8.18.0 with OpenLDAP 2.6.10).
/// </summary>
/// <remarks>
/// <para>
/// A filter the build refuses sends no SearchRequest but uses up its messageID; the
/// UnbindRequest follows and the transfer fails with exit 39: <c>LDAP remote: Filter Error</c>
/// from WinLDAP, <c>LDAP local: ldap_search_ext Bad search filter</c> from <c>libldap</c>.
/// </para>
/// <para>
/// A SearchResultDone of <c>success</c> or <c>sizeLimitExceeded</c> succeeds; any other fails
/// with exit 39, WinLDAP's <c>LDAP remote: &lt;text&gt;</c> or <c>libldap</c>'s
/// <c>LDAP remote: search failed &lt;text&gt; &lt;diagnosticMessage&gt;</c>, and either way
/// the UnbindRequest is sent. Entries are read past (BL-588 writes them), and so are messages
/// for other messageIDs. WinLDAP reads past any other reply too; the OpenLDAP build takes one
/// (a SearchResultReference, say) as the end of the transfer, abandons the search, unbinds and
/// succeeds. A server that closes, or sends bytes that are not an LDAPMessage, fails without
/// an UnbindRequest: WinLDAP with exit 39 <c>LDAP remote: Server Down</c> (after a 30-second
/// wait this does not make), <c>libldap</c> with exit 56.
/// </para>
/// </remarks>
internal static class LdapSearch
{
    /// <summary>WinLDAP's <c>LDAP_SERVER_DOWN</c>.</summary>
    private const int WinLdapServerDown = 81;

    /// <summary>WinLDAP's <c>LDAP_FILTER_ERROR</c>.</summary>
    private const int WinLdapFilterError = 87;

    private const string WinLdapRemotePrefix = "LDAP remote: ";

    private const string OpenLdapSearchFailedPrefix = "LDAP remote: search failed ";

    private const string OpenLdapBadFilter = "LDAP local: ldap_search_ext Bad search filter";

    private const string OpenLdapCannotContactServer = "LDAP local: search ldap_result Can't contact LDAP server";

    /// <summary>Runs <paramref name="search" /> on a bound session and leaves as the build leaves.</summary>
    /// <param name="dialect">The build to answer as.</param>
    /// <param name="exchange">The bound session.</param>
    /// <param name="search">The search the URL names.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The transfer's result.</returns>
    public static async ValueTask<TransferResult> RunAsync(
        LdapDialect dialect,
        LdapExchange exchange,
        LdapSearchParameters search,
        CancellationToken cancellationToken)
    {
        var encoder = new LdapFilterEncoder(dialect, exchange.Writer);
        byte[]? filter = search.Filter is null ? encoder.EncodeDefault() : encoder.Encode(search.Filter);
        int messageId = exchange.TakeMessageId();
        if (filter is null)
        {
            return await UnbindAndReturnAsync(exchange, FilterRefused(dialect), cancellationToken).ConfigureAwait(false);
        }

        byte[] request = LdapRequests.Search(
            exchange.Writer,
            messageId,
            LdapWireText.Encode(dialect, search.BaseObject),
            search.Scope,
            filter,
            search.Attributes.Select(attribute => LdapWireText.Encode(dialect, attribute)));
        await exchange.SendAsync(request, cancellationToken).ConfigureAwait(false);
        LdapSearchReply reply = await ReadToTheEndAsync(dialect, exchange, messageId, cancellationToken).ConfigureAwait(false);
        return reply.Kind switch
        {
            LdapSearchReplyKind.Lost => Lost(dialect),
            LdapSearchReplyKind.OtherResponse => await AbandonAndUnbindAsync(exchange, messageId, cancellationToken).ConfigureAwait(false),
            _ => await UnbindAndReturnAsync(exchange, Outcome(dialect, reply), cancellationToken).ConfigureAwait(false),
        };
    }

    /// <summary>Reads replies until the one that ends the search for <paramref name="dialect" />'s build.</summary>
    private static async ValueTask<LdapSearchReply> ReadToTheEndAsync(
        LdapDialect dialect,
        LdapExchange exchange,
        int messageId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            LdapSearchReply reply = await exchange.ReadSearchReplyAsync(messageId, cancellationToken).ConfigureAwait(false);
            bool readPast = reply.Kind is LdapSearchReplyKind.Entry or LdapSearchReplyKind.OtherMessage
                || (reply.Kind == LdapSearchReplyKind.OtherResponse && dialect == LdapDialect.WinLdap);
            if (!readPast)
            {
                return reply;
            }
        }
    }

    private static TransferResult FilterRefused(LdapDialect dialect) =>
        dialect == LdapDialect.WinLdap
            ? TransferResult.Failure(CurlExitCode.LdapSearchFailed, WinLdapRemotePrefix + WinLdapResultText.Of(WinLdapFilterError))
            : TransferResult.Failure(CurlExitCode.LdapSearchFailed, OpenLdapBadFilter);

    private static TransferResult Lost(LdapDialect dialect) =>
        dialect == LdapDialect.WinLdap
            ? TransferResult.Failure(CurlExitCode.LdapSearchFailed, WinLdapRemotePrefix + WinLdapResultText.Of(WinLdapServerDown))
            : TransferResult.Failure(CurlExitCode.RecvError, OpenLdapCannotContactServer);

    private static TransferResult Outcome(LdapDialect dialect, LdapSearchReply done)
    {
        if (done.IsSuccess)
        {
            return TransferResult.Success(0);
        }

        string message = dialect == LdapDialect.WinLdap
            ? WinLdapRemotePrefix + WinLdapResultText.Of(done.ResultCode)
            : OpenLdapSearchFailedPrefix + LibLdapResultText.Of(done.ResultCode) + " " + done.DiagnosticMessage;
        return TransferResult.Failure(CurlExitCode.LdapSearchFailed, message);
    }

    private static async ValueTask<TransferResult> AbandonAndUnbindAsync(LdapExchange exchange, int messageId, CancellationToken cancellationToken)
    {
        await exchange.AbandonAsync(messageId, cancellationToken).ConfigureAwait(false);
        return await UnbindAndReturnAsync(exchange, TransferResult.Success(0), cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<TransferResult> UnbindAndReturnAsync(LdapExchange exchange, TransferResult result, CancellationToken cancellationToken)
    {
        await exchange.UnbindAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }
}
