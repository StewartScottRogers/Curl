using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Binds as the Windows build binds without <c>-u</c>: curl 8.21.0's <c>ldap_win_bind</c>
/// calls WinLDAP's <c>ldap_bind_s</c> with <c>LDAP_AUTH_NEGOTIATE</c> and no credentials, so
/// WinLDAP binds as the logged-on user (ADR-0166, measured by BL-830).
/// </summary>
/// <remarks>
/// <para>
/// Each attempt reads the rootDSE's <c>supportedCapabilities</c>, then its
/// <c>supportedSASLMechanisms</c>. When a server has offered <c>GSS-SPNEGO</c> (in this
/// attempt or an earlier one) it sends SASL <c>GSS-SPNEGO</c> binds with SPNEGO tokens,
/// answering each <c>saslBindInProgress</c> challenge until the server answers otherwise.
/// Otherwise it reads <c>supportedCapabilities</c> once more and sends the Sicily NTLM bind:
/// <c>sicilyNegotiate</c> named <c>NTLM</c>, whose answer carries the NTLM challenge in its
/// matchedDN, then <c>sicilyResponse</c>.
/// </para>
/// <para>
/// A failed attempt is made once more, as curl's LDAPv2 retry. When both fail, it sends an
/// UnbindRequest and fails with exit 38 and WinLDAP's text for the second attempt's result
/// code. A server that closes, or answers a bind with something that is not a BindResponse,
/// fails at once without an UnbindRequest: <c>Timeout</c>, except while the second attempt
/// reads the rootDSE, which is <c>Server Down</c>. A package that cannot produce a token,
/// or a <c>success</c> before the authentication is complete, fails the attempt with
/// <c>Local Error</c>.
/// </para>
/// </remarks>
internal static class WinLdapLogonBind
{
    private const string SupportedCapabilities = "supportedCapabilities";

    private const string SupportedSaslMechanisms = "supportedSASLMechanisms";

    private const string GssSpnego = "GSS-SPNEGO";

    /// <summary>The name WinLDAP's <c>sicilyNegotiate</c> bind carries.</summary>
    private const string SicilyNegotiateName = "NTLM";

    /// <summary>The <c>sicilyNegotiate</c> authentication choice's tag number.</summary>
    private const int SicilyNegotiate = 10;

    /// <summary>The <c>sicilyResponse</c> authentication choice's tag number.</summary>
    private const int SicilyResponse = 11;

    /// <summary>How many attempts are made: the first, and curl's LDAPv2 retry.</summary>
    private const int Attempts = 2;

    private static readonly LdapBindReply LocalError = new(LdapBindReplyStatus.Answered, WinLdapResultText.LocalError);

    /// <summary>Binds as the logged-on user as WinLDAP does.</summary>
    /// <param name="exchange">The session to bind on.</param>
    /// <param name="tokenSource">Starts the logged-on user's authentication.</param>
    /// <param name="host">The URL's host, for the service principal name <c>ldap/</c><paramref name="host" />.</param>
    /// <param name="cancellationToken">Cancels the bind.</param>
    /// <returns><see langword="null" /> once bound; otherwise the failed transfer's result.</returns>
    public static async ValueTask<TransferResult?> BindAsync(
        LdapExchange exchange,
        ILdapLogonTokenSource tokenSource,
        string host,
        CancellationToken cancellationToken)
    {
        bool offersSpnego = false;
        int resultCode = 0;
        for (int attempt = 1; attempt <= Attempts; attempt++)
        {
            if (await ReadOffersSpnegoAsync(exchange, offersSpnego, cancellationToken).ConfigureAwait(false) is not { } offers)
            {
                return LdapBind.WinLdapBindFailed(attempt == 1 ? WinLdapResultText.Timeout : WinLdapResultText.ServerDown);
            }

            offersSpnego = offers;
            LdapBindReply reply = await BindOnceAsync(exchange, tokenSource, offers, "ldap/" + host, cancellationToken).ConfigureAwait(false);
            if (reply.Status != LdapBindReplyStatus.Answered)
            {
                return LdapBind.WinLdapBindFailed(WinLdapResultText.Timeout);
            }

            if (reply.IsSuccess)
            {
                return null;
            }

            resultCode = reply.ResultCode;
        }

        return await LdapBind.UnbindAndFailAsync(exchange, LdapBind.WinLdapBindFailed(resultCode), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the rootDSE as one attempt does and says whether to bind with <c>GSS-SPNEGO</c>;
    /// <see langword="null" /> when the server closed or sent bytes that are not an LDAPMessage.
    /// </summary>
    private static async ValueTask<bool?> ReadOffersSpnegoAsync(LdapExchange exchange, bool offeredBefore, CancellationToken cancellationToken)
    {
        if (await ReadRootDseAsync(exchange, SupportedCapabilities, cancellationToken).ConfigureAwait(false) is null
            || await ReadRootDseAsync(exchange, SupportedSaslMechanisms, cancellationToken).ConfigureAwait(false) is not { } mechanisms)
        {
            return null;
        }

        bool offersSpnego = offeredBefore || mechanisms.Contains(GssSpnego, StringComparer.OrdinalIgnoreCase);
        return offersSpnego || await ReadRootDseAsync(exchange, SupportedCapabilities, cancellationToken).ConfigureAwait(false) is not null
            ? offersSpnego
            : null;
    }

    /// <summary>
    /// Searches the rootDSE for <paramref name="attribute" /> and collects every value its
    /// entries carry, whatever the SearchResultDone's resultCode; replies that are neither are
    /// skipped. <see langword="null" /> when the server closed or sent bytes that are not an
    /// LDAPMessage first.
    /// </summary>
    private static async ValueTask<List<string>?> ReadRootDseAsync(LdapExchange exchange, string attribute, CancellationToken cancellationToken)
    {
        int messageId = exchange.TakeMessageId();
        await exchange.SendAsync(LdapRequests.RootDseSearch(exchange.Writer, messageId, attribute), cancellationToken).ConfigureAwait(false);
        var values = new List<string>();
        while (true)
        {
            LdapSearchReply reply = await exchange.ReadSearchReplyAsync(messageId, cancellationToken).ConfigureAwait(false);
            if (reply.Kind == LdapSearchReplyKind.Done)
            {
                return values;
            }

            if (reply.Kind == LdapSearchReplyKind.Lost)
            {
                return null;
            }

            values.AddRange(ValuesOf(reply.Entry, attribute));
        }
    }

    /// <summary>
    /// Every value of <paramref name="name" /> (in any case) <paramref name="entry" /> carries;
    /// none for a reply that is not an entry or an entry whose attributes cannot be read.
    /// </summary>
    private static IEnumerable<string> ValuesOf(LdapSearchEntry? entry, string name) =>
        (entry?.Attributes ?? [])
            .Where(attribute => string.Equals(Encoding.UTF8.GetString(attribute.Name), name, StringComparison.OrdinalIgnoreCase))
            .SelectMany(attribute => attribute.Values)
            .Select(value => Encoding.UTF8.GetString(value));

    /// <summary>
    /// Makes one attempt's bind with a fresh authentication, SASL or Sicily. A bind that
    /// succeeds starts the session's security layer with the authentication's keys; any other
    /// disposes it.
    /// </summary>
    private static async ValueTask<LdapBindReply> BindOnceAsync(
        LdapExchange exchange,
        ILdapLogonTokenSource tokenSource,
        bool offersSpnego,
        string targetName,
        CancellationToken cancellationToken)
    {
        ILdapLogonAuthentication authentication = tokenSource.Start(offersSpnego ? LdapLogonPackage.Negotiate : LdapLogonPackage.Ntlm, targetName);
        LdapBindReply reply = LocalError;
        try
        {
            reply = offersSpnego
                ? await SaslBindAsync(exchange, authentication, cancellationToken).ConfigureAwait(false)
                : await SicilyBindAsync(exchange, authentication, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (reply.IsSuccess)
            {
                exchange.StartSecurityLayer(authentication);
            }
            else
            {
                authentication.Dispose();
            }
        }

        return reply;
    }

    /// <summary>Sends <c>GSS-SPNEGO</c> binds, one for each token, until the server answers anything but <c>saslBindInProgress</c>.</summary>
    private static async ValueTask<LdapBindReply> SaslBindAsync(LdapExchange exchange, ILdapLogonAuthentication authentication, CancellationToken cancellationToken)
    {
        byte[]? token = authentication.NextToken([]);
        while (token is not null)
        {
            LdapBindReply reply = await exchange.SaslBindAsync(GssSpnego, token, cancellationToken).ConfigureAwait(false);
            if (reply.Status != LdapBindReplyStatus.Answered || reply.ResultCode != LdapBindReply.SaslBindInProgress)
            {
                return reply.IsSuccess && !Completes(authentication, reply.ServerSaslCredentials) ? LocalError : reply;
            }

            token = authentication.NextToken(reply.ServerSaslCredentials);
        }

        return LocalError;
    }

    /// <summary>
    /// Says whether <paramref name="authentication" /> is complete once it has taken the
    /// server's last token, which a <c>success</c> may carry (a Kerberos AP-REP, SPNEGO's
    /// accept-completed); a package with nothing more to send is not asked for anything.
    /// </summary>
    private static bool Completes(ILdapLogonAuthentication authentication, byte[] lastToken)
    {
        if (!authentication.IsAuthenticated && lastToken.Length > 0)
        {
            authentication.NextToken(lastToken);
        }

        return authentication.IsAuthenticated;
    }

    /// <summary>Sends <c>sicilyNegotiate</c>, then <c>sicilyResponse</c> answering the challenge the first's answer carries.</summary>
    private static async ValueTask<LdapBindReply> SicilyBindAsync(LdapExchange exchange, ILdapLogonAuthentication authentication, CancellationToken cancellationToken)
    {
        if (authentication.NextToken([]) is not { } negotiate)
        {
            return LocalError;
        }

        LdapBindReply reply = await exchange.SicilyBindAsync(SicilyNegotiateName, SicilyNegotiate, negotiate, cancellationToken).ConfigureAwait(false);
        if (!reply.IsSuccess)
        {
            return reply;
        }

        return authentication.NextToken(reply.MatchedDn) is { } response
            ? await exchange.SicilyBindAsync(string.Empty, SicilyResponse, response, cancellationToken).ConfigureAwait(false)
            : LocalError;
    }
}
