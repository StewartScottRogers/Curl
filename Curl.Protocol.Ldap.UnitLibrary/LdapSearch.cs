using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Sends the search an LDAP URL names, writes its entries and reads it to its
/// SearchResultDone, as each reference build does, and fails as it fails (ADR-0166, measured
/// by BL-587 and BL-588 against curl 8.21.0 with WinLDAP and curl 8.18.0 with OpenLDAP 2.6.10).
/// </summary>
/// <remarks>
/// <para>
/// A filter the build refuses sends no SearchRequest but uses up its messageID; the
/// UnbindRequest follows and the transfer fails with exit 39: <c>LDAP remote: Filter Error</c>
/// from WinLDAP, <c>LDAP local: ldap_search_ext Bad search filter</c> from <c>libldap</c>.
/// </para>
/// <para>
/// Each entry is written as <see cref="LdapEntryFormatter" /> formats it, when
/// <see cref="LdapEntryWriter" /> says: the OpenLDAP build as it arrives, the Windows build
/// only once the search has succeeded, so a failed search writes nothing there. The OpenLDAP
/// build fails on an entry whose attributes cannot be read, writing nothing for it: it
/// abandons the search and fails with exit 56 without an UnbindRequest; WinLDAP writes such
/// an entry as its DN line.
/// </para>
/// <para>
/// A SearchResultDone of <c>success</c> or <c>sizeLimitExceeded</c> succeeds; any other fails
/// with exit 39, WinLDAP's <c>LDAP remote: &lt;text&gt;</c> or <c>libldap</c>'s
/// <c>LDAP remote: search failed &lt;text&gt; &lt;diagnosticMessage&gt;</c>, and either way
/// the UnbindRequest is sent. Messages for other messageIDs are read past. WinLDAP reads past
/// any other reply too, a SearchResultReference included, and writes nothing for it; the
/// OpenLDAP build takes one as the end of the transfer, abandons the search, unbinds and
/// succeeds with the entries written so far. A server that closes, or sends bytes that are not
/// an LDAPMessage, fails without an UnbindRequest: WinLDAP with exit 39
/// <c>LDAP remote: Server Down</c> (after a 30-second wait this does not make), <c>libldap</c>
/// with exit 56. A connection reset, on a send or a receive, is a server that closed (BL-845).
/// </para>
/// <para>
/// An output that stops accepting bytes ends the transfer with exit 23 as
/// <see cref="LdapEntryWriter" /> words it (BL-845): the OpenLDAP build then abandons the search
/// and unbinds, the Windows build, whose entries are written after the search, unbinds.
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

    /// <summary>curl's text for exit 56, which the OpenLDAP build reports for an entry it cannot read.</summary>
    private const string ReceiveFailed = "Failure when receiving data from the peer";

    /// <summary>Runs <paramref name="search" /> on a bound session and leaves as the build leaves.</summary>
    /// <param name="dialect">The build to answer as.</param>
    /// <param name="exchange">The bound session.</param>
    /// <param name="search">The search the URL names.</param>
    /// <param name="context">The transfer, whose output the entries are written to.</param>
    /// <returns>The transfer's result.</returns>
    public static async ValueTask<TransferResult> RunAsync(
        LdapDialect dialect,
        LdapExchange exchange,
        LdapSearchParameters search,
        ITransferContext context)
    {
        CancellationToken cancellationToken = context.CancellationToken;
        var encoder = new LdapFilterEncoder(dialect, exchange.Writer);
        byte[]? filter = search.Filter is null ? encoder.EncodeDefault() : encoder.Encode(search.Filter);
        int messageId = exchange.TakeMessageId();
        exchange.Log.Searching(search);
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
        exchange.Log.Sent("SearchRequest", messageId);
        var entries = new LdapEntryWriter(dialect, context);
        LdapSearchReply reply = await ReadToTheEndAsync(dialect, exchange, messageId, entries, cancellationToken).ConfigureAwait(false);
        exchange.Log.EntriesReturned(entries.EntryCount);
        TransferResult result = entries.WriteFailure is { } writeFailure
            ? await AbandonAndUnbindAsync(exchange, messageId, writeFailure, cancellationToken).ConfigureAwait(false)
            : await FinishAsync(dialect, exchange, messageId, reply, entries, cancellationToken).ConfigureAwait(false);

        // Entries the OpenLDAP build wrote before a failure count too, as curl's size_download does.
        return result with { BytesTransferred = entries.BytesWritten };
    }

    /// <summary>Leaves the search as <paramref name="dialect" />'s build leaves after <paramref name="reply" /> ended it.</summary>
    private static async ValueTask<TransferResult> FinishAsync(
        LdapDialect dialect,
        LdapExchange exchange,
        int messageId,
        LdapSearchReply reply,
        LdapEntryWriter entries,
        CancellationToken cancellationToken) =>
        reply.Kind switch
        {
            LdapSearchReplyKind.Lost => Lost(dialect),
            LdapSearchReplyKind.Entry => await AbandonAsync(exchange, messageId, TransferResult.Failure(CurlExitCode.RecvError, ReceiveFailed), cancellationToken).ConfigureAwait(false),
            LdapSearchReplyKind.OtherResponse => await AbandonAndUnbindAsync(exchange, messageId, TransferResult.Success(0), cancellationToken).ConfigureAwait(false),
            _ => await UnbindAndReturnAsync(exchange, await OutcomeAsync(dialect, reply, entries).ConfigureAwait(false), cancellationToken).ConfigureAwait(false),
        };

    /// <summary>
    /// Reads replies, handing each entry to <paramref name="entries" />, until the one that
    /// ends the search for <paramref name="dialect" />'s build; an entry is returned only when
    /// the OpenLDAP build cannot read its attributes.
    /// </summary>
    private static async ValueTask<LdapSearchReply> ReadToTheEndAsync(
        LdapDialect dialect,
        LdapExchange exchange,
        int messageId,
        LdapEntryWriter entries,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            LdapSearchReply reply = await exchange.ReadSearchReplyAsync(messageId, cancellationToken).ConfigureAwait(false);
            if (reply.Entry is { } entry && IsWritten(dialect, entry))
            {
                if (!await entries.AddAsync(entry).ConfigureAwait(false))
                {
                    return reply;
                }
            }
            else if (!IsReadPast(dialect, reply))
            {
                return reply;
            }
        }
    }

    /// <summary>Gets a value indicating whether <paramref name="dialect" />'s build writes <paramref name="entry" />: WinLDAP any, OpenLDAP one whose attributes it can read.</summary>
    private static bool IsWritten(LdapDialect dialect, LdapSearchEntry entry) =>
        entry.Attributes is not null || dialect == LdapDialect.WinLdap;

    /// <summary>Gets a value indicating whether <paramref name="dialect" />'s build reads past <paramref name="reply" /> to the next.</summary>
    private static bool IsReadPast(LdapDialect dialect, LdapSearchReply reply) =>
        reply.Kind == LdapSearchReplyKind.OtherMessage
        || (reply.Kind == LdapSearchReplyKind.OtherResponse && dialect == LdapDialect.WinLdap);

    private static TransferResult FilterRefused(LdapDialect dialect) =>
        dialect == LdapDialect.WinLdap
            ? TransferResult.Failure(CurlExitCode.LdapSearchFailed, WinLdapRemotePrefix + WinLdapResultText.Of(WinLdapFilterError))
            : TransferResult.Failure(CurlExitCode.LdapSearchFailed, OpenLdapBadFilter);

    private static TransferResult Lost(LdapDialect dialect) =>
        dialect == LdapDialect.WinLdap
            ? TransferResult.Failure(CurlExitCode.LdapSearchFailed, WinLdapRemotePrefix + WinLdapResultText.Of(WinLdapServerDown))
            : TransferResult.Failure(CurlExitCode.RecvError, OpenLdapCannotContactServer);

    private static async ValueTask<TransferResult> OutcomeAsync(LdapDialect dialect, LdapSearchReply done, LdapEntryWriter entries)
    {
        if (done.IsSuccess)
        {
            return await entries.WriteHeldAsync().ConfigureAwait(false) ? TransferResult.Success(0) : entries.WriteFailure!;
        }

        string message = dialect == LdapDialect.WinLdap
            ? WinLdapRemotePrefix + WinLdapResultText.Of(done.ResultCode)
            : OpenLdapSearchFailedPrefix + LibLdapResultText.Of(done.ResultCode) + " " + done.DiagnosticMessage;
        return TransferResult.Failure(CurlExitCode.LdapSearchFailed, message);
    }

    private static async ValueTask<TransferResult> AbandonAsync(LdapExchange exchange, int messageId, TransferResult result, CancellationToken cancellationToken)
    {
        await exchange.AbandonAsync(messageId, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static async ValueTask<TransferResult> AbandonAndUnbindAsync(LdapExchange exchange, int messageId, TransferResult result, CancellationToken cancellationToken) =>
        await UnbindAndReturnAsync(exchange, await AbandonAsync(exchange, messageId, result, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

    private static async ValueTask<TransferResult> UnbindAndReturnAsync(LdapExchange exchange, TransferResult result, CancellationToken cancellationToken)
    {
        await exchange.UnbindAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }
}
