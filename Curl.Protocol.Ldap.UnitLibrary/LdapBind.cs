using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Binds as each reference build of curl binds, and fails as it fails (ADR-0166, measured
/// by BL-586 against curl 8.21.0 with WinLDAP and curl 8.18.0 with OpenLDAP 2.6.10).
/// </summary>
internal static class LdapBind
{
    /// <summary>The protocol version both builds bind with first.</summary>
    private const int LdapVersion3 = 3;

    /// <summary>The protocol version WinLDAP retries a failed bind with.</summary>
    private const int LdapVersion2 = 2;

    /// <summary>The LDAP result code <c>invalidCredentials</c>.</summary>
    private const int InvalidCredentials = 49;

    private const string WinLdapBindFailedPrefix = "LDAP local: bind via ldap_win_bind ";

    private const string OpenLdapLoginDenied = "Login denied";

    private const string OpenLdapCannotBind = "LDAP: cannot bind";

    private const string OpenLdapCannotContactServer = "LDAP local: connecting ldap_result Can't contact LDAP server";

    /// <summary>Binds as <paramref name="dialect" />'s build does.</summary>
    /// <param name="dialect">The build to answer as.</param>
    /// <param name="exchange">The session to bind on.</param>
    /// <param name="name">The DN to bind as; empty for the anonymous bind.</param>
    /// <param name="password">The password; empty for the anonymous bind.</param>
    /// <param name="cancellationToken">Cancels the bind.</param>
    /// <returns><see langword="null" /> once bound; otherwise the failed transfer's result.</returns>
    public static ValueTask<TransferResult?> BindAsync(
        LdapDialect dialect,
        LdapExchange exchange,
        string name,
        string password,
        CancellationToken cancellationToken) =>
        dialect == LdapDialect.WinLdap
            ? BindAsWinLdapAsync(exchange, name, password, cancellationToken)
            : BindAsOpenLdapAsync(exchange, name, password, cancellationToken);

    /// <summary>
    /// Binds as <c>ldap_win_bind</c> does: LDAPv3, then once more as LDAPv2 when the first
    /// is answered with anything but <c>success</c>. A second failure sends an UnbindRequest
    /// and fails with exit 38 and WinLDAP's text for its result code. A first bind that gets
    /// no BindResponse fails with WinLDAP's <c>Timeout</c>, a retry the server closes on with
    /// <c>Unavailable</c>, and neither sends an UnbindRequest.
    /// </summary>
    /// <remarks>
    /// WinLDAP waits 30 seconds for a BindResponse before it reports <c>Timeout</c>; this
    /// reports it as soon as the server closes or sends a reply that is not one.
    /// </remarks>
    private static async ValueTask<TransferResult?> BindAsWinLdapAsync(
        LdapExchange exchange,
        string name,
        string password,
        CancellationToken cancellationToken)
    {
        LdapBindReply first = await exchange.BindAsync(LdapVersion3, name, password, cancellationToken).ConfigureAwait(false);
        if (first.IsSuccess)
        {
            return null;
        }

        if (first.Status != LdapBindReplyStatus.Answered)
        {
            return WinLdapBindFailed(WinLdapResultText.Timeout);
        }

        LdapBindReply retry = await exchange.BindAsync(LdapVersion2, name, password, cancellationToken).ConfigureAwait(false);
        return retry.Status switch
        {
            LdapBindReplyStatus.Answered when retry.IsSuccess => null,
            LdapBindReplyStatus.Answered => await UnbindAndFailAsync(exchange, WinLdapBindFailed(retry.ResultCode), cancellationToken).ConfigureAwait(false),
            LdapBindReplyStatus.Closed => WinLdapBindFailed(WinLdapResultText.Unavailable),
            _ => WinLdapBindFailed(WinLdapResultText.Timeout),
        };
    }

    /// <summary>
    /// Binds as curl's <c>openldap.c</c> does: LDAPv3 once. Any answer but <c>success</c>,
    /// a reply that is not a BindResponse included, sends an UnbindRequest and fails, with
    /// exit 67 <c>Login denied</c> for <c>invalidCredentials</c> and exit 38 otherwise. A
    /// server that closes first fails with exit 7, as <c>libldap</c>'s
    /// <c>Can't contact LDAP server</c> maps.
    /// </summary>
    private static async ValueTask<TransferResult?> BindAsOpenLdapAsync(
        LdapExchange exchange,
        string name,
        string password,
        CancellationToken cancellationToken)
    {
        LdapBindReply reply = await exchange.BindAsync(LdapVersion3, name, password, cancellationToken).ConfigureAwait(false);
        if (reply.IsSuccess)
        {
            return null;
        }

        if (reply.Status == LdapBindReplyStatus.Closed)
        {
            return TransferResult.Failure(CurlExitCode.CouldntConnect, OpenLdapCannotContactServer);
        }

        TransferResult failure = reply.Status == LdapBindReplyStatus.Answered && reply.ResultCode == InvalidCredentials
            ? TransferResult.Failure(CurlExitCode.LoginDenied, OpenLdapLoginDenied)
            : TransferResult.Failure(CurlExitCode.LdapCannotBind, OpenLdapCannotBind);
        return await UnbindAndFailAsync(exchange, failure, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The failure <c>ldap_win_bind</c> reports for <paramref name="resultCode" />: exit 38 and WinLDAP's text for it.</summary>
    /// <param name="resultCode">The LDAP or WinLDAP result code.</param>
    /// <returns>The failed transfer's result.</returns>
    internal static TransferResult WinLdapBindFailed(int resultCode) =>
        TransferResult.Failure(CurlExitCode.LdapCannotBind, WinLdapBindFailedPrefix + WinLdapResultText.Of(resultCode));

    /// <summary>Sends an UnbindRequest, then returns <paramref name="failure" />.</summary>
    /// <param name="exchange">The session to leave.</param>
    /// <param name="failure">The failed transfer's result.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns><paramref name="failure" />.</returns>
    internal static async ValueTask<TransferResult?> UnbindAndFailAsync(
        LdapExchange exchange,
        TransferResult failure,
        CancellationToken cancellationToken)
    {
        await exchange.UnbindAsync(cancellationToken).ConfigureAwait(false);
        return failure;
    }
}
