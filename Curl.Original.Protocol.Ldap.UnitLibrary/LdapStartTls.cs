using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Upgrades a plaintext <c>ldap</c> session to TLS with the StartTLS extended operation, as
/// curl's OpenLDAP build does for <c>--ssl</c> and <c>--ssl-reqd</c> before it binds (BL-3542).
/// </summary>
/// <remarks>
/// As measured against curl 8.21.0 with OpenLDAP 2.6.15 (BL-5908, ADR-1363): when the server
/// refuses StartTLS, <c>--ssl</c> goes on to bind in plaintext, as LDAPv2 after
/// <c>protocolError</c>, and <c>--ssl-reqd</c> fails with what <c>openldap.c</c> maps the result
/// code to after an UnbindRequest. A server that closes instead of answering fails either with
/// exit 7. When the server accepts, the session continues through the TLS handshake of
/// <see cref="ITlsProvider" />, whose every failure is exit 64 with the provider's message.
/// </remarks>
internal static class LdapStartTls
{
    private const string FailurePrefix = "LDAP local: ";

    /// <summary>curl's text for exit 64, which a refused <c>--ssl-reqd</c> reports.</summary>
    private const string UseSslFailedText = "Requested SSL level failed";

    /// <summary>Sends StartTLS on <paramref name="exchange" /> and, when the server accepts it, moves the session onto TLS.</summary>
    /// <param name="exchange">The plaintext session, nothing sent on it yet.</param>
    /// <param name="plaintext">The session's connection, handed to <paramref name="tlsProvider" /> for the handshake.</param>
    /// <param name="tlsProvider">Performs the handshake; <see langword="null" /> when the handler has none.</param>
    /// <param name="context">The transfer: its TLS level, host, events and cancellation.</param>
    /// <returns>
    /// The failure that ends the transfer, or <see langword="null" /> to go on and bind; and the
    /// protocol version a simple bind starts with.
    /// </returns>
    public static async ValueTask<(TransferResult? Failure, int BindVersion)> UpgradeAsync(LdapExchange exchange, IConnection plaintext, ITlsProvider? tlsProvider, ITransferContext context)
    {
        LdapBindReply reply = await exchange.StartTlsAsync(context.CancellationToken).ConfigureAwait(false);
        if (reply.Status == LdapBindReplyStatus.Closed)
        {
            return (LdapBind.OpenLdapCannotContact(), LdapBind.LdapVersion3);
        }

        if (reply.IsSuccess)
        {
            return (await HandshakeAsync(exchange, plaintext, tlsProvider, context).ConfigureAwait(false), LdapBind.LdapVersion3);
        }

        return context.SslLevel == TransportSecurityLevel.Try
            ? (null, BindVersionAfterRefusal(reply))
            : (await RefusedAsync(exchange, reply, context.CancellationToken).ConfigureAwait(false), LdapBind.LdapVersion3);
    }

    /// <summary>
    /// Moves the session onto TLS after the server accepted StartTLS. <c>openldap.c</c> reports any
    /// handshake failure, a certificate verification one included, as exit 64 with no sslcerts help.
    /// </summary>
    private static async ValueTask<TransferResult?> HandshakeAsync(LdapExchange exchange, IConnection plaintext, ITlsProvider? tlsProvider, ITransferContext context)
    {
        if (tlsProvider is null)
        {
            return TransferResult.Failure(CurlExitCode.UseSslFailed, FailurePrefix + "no TLS provider to start TLS with");
        }

        ConnectResult secured = await tlsProvider.AuthenticateAsClientAsync(plaintext, context.Url.IdnHost, context.Events, context.CancellationToken).ConfigureAwait(false);
        if (secured.Connection is not { } connection)
        {
            return new TransferResult(CurlExitCode.UseSslFailed, 0, secured.ErrorMessage);
        }

        exchange.UseSecuredConnection(connection);
        return null;
    }

    /// <summary>The version <c>--ssl</c> binds with after a refusal: LDAPv2 after <c>protocolError</c>, LDAPv3 otherwise.</summary>
    private static int BindVersionAfterRefusal(LdapBindReply reply) =>
        reply.Status == LdapBindReplyStatus.Answered && reply.ResultCode == LdapBind.ProtocolError
            ? LdapBind.LdapVersion2
            : LdapBind.LdapVersion3;

    /// <summary>
    /// What <c>--ssl-reqd</c> fails with when the server refuses StartTLS: an answered refusal is the
    /// result code's mapping, else exit 64 <c>Requested SSL level failed</c>, after an UnbindRequest;
    /// a reply that is not an ExtendedResponse is exit 64 and sends nothing.
    /// </summary>
    private static async ValueTask<TransferResult?> RefusedAsync(LdapExchange exchange, LdapBindReply reply, CancellationToken cancellationToken)
    {
        if (reply.Status != LdapBindReplyStatus.Answered)
        {
            return TransferResult.Failure(CurlExitCode.UseSslFailed, FailurePrefix + "Can't contact LDAP server");
        }

        TransferResult failure = LdapBind.OpenLdapMappedFailure(reply.ResultCode) ?? TransferResult.Failure(CurlExitCode.UseSslFailed, UseSslFailedText);
        return await LdapBind.UnbindAndFailAsync(exchange, failure, cancellationToken).ConfigureAwait(false);
    }
}
