using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Upgrades a plaintext <c>ldap</c> session to TLS with the StartTLS extended operation, as
/// curl's OpenLDAP build does for <c>--ssl</c> and <c>--ssl-reqd</c> before it binds (BL-3542).
/// </summary>
/// <remarks>
/// As measured against curl 8.21.0 with OpenLDAP 2.6.15 (BL-5908 and BL-5931, ADR-1366): when the
/// server refuses StartTLS, <c>--ssl</c> goes on to bind in plaintext, as LDAPv2 after
/// <c>protocolError</c>, and <c>--ssl-reqd</c> fails with what <c>openldap.c</c> maps the result
/// code to after an UnbindRequest. A server that closes instead of answering fails either with
/// exit 7. When the server accepts, the session continues through the TLS handshake of
/// <see cref="ITlsProvider" />, whose every failure is exit 64 with the provider's message, after
/// an UnbindRequest in plaintext. A BindResponse or other result in place of the ExtendedResponse
/// counts by its result code; a search entry or reference counts as a refusal, which
/// <c>--ssl-reqd</c> abandons before it unbinds.
/// </remarks>
internal static class LdapStartTls
{
    private const string FailurePrefix = "LDAP local: ";

    /// <summary>curl's text for exit 64, which a refused <c>--ssl-reqd</c> reports.</summary>
    private const string UseSslFailedText = "Requested SSL level failed";

    /// <summary>The messageID of the StartTLS request, the session's first.</summary>
    private const int StartTlsMessageId = 1;

    /// <summary>Sends StartTLS on <paramref name="exchange" /> and, when the server accepts it, moves the session onto TLS.</summary>
    /// <param name="exchange">The plaintext session, nothing sent on it yet.</param>
    /// <param name="plaintext">The session's connection, which <paramref name="tlsProvider" /> performs the handshake over.</param>
    /// <param name="tlsProvider">Performs the handshake; <see langword="null" /> when the handler has none.</param>
    /// <param name="context">The transfer: its TLS level, host, events and cancellation.</param>
    /// <returns>
    /// The failure that ends the transfer, or <see langword="null" /> to go on; the protocol
    /// version a simple bind starts with; and whether the session moved onto TLS, after which
    /// curl sends no simple bind without <c>-u</c>.
    /// </returns>
    public static async ValueTask<(TransferResult? Failure, int BindVersion, bool Secured)> UpgradeAsync(LdapExchange exchange, IConnection plaintext, ITlsProvider? tlsProvider, ITransferContext context)
    {
        LdapBindReply reply = await exchange.StartTlsAsync(context.CancellationToken).ConfigureAwait(false);
        if (reply.Status == LdapBindReplyStatus.Closed)
        {
            return (LdapBind.OpenLdapCannotContact(), LdapBind.LdapVersion3, false);
        }

        if (reply.IsSuccess)
        {
            TransferResult? handshakeFailure = await HandshakeAsync(exchange, plaintext, tlsProvider, context).ConfigureAwait(false);
            return (handshakeFailure, LdapBind.LdapVersion3, handshakeFailure is null);
        }

        return context.SslLevel == TransportSecurityLevel.Try
            ? (null, BindVersionAfterRefusal(reply), false)
            : (await RefusedAsync(exchange, reply, context.CancellationToken).ConfigureAwait(false), LdapBind.LdapVersion3, false);
    }

    /// <summary>
    /// Moves the session onto TLS after the server accepted StartTLS. <c>openldap.c</c> reports any
    /// handshake failure, a certificate verification one included, as exit 64 with no sslcerts help,
    /// and then unbinds in plaintext, so the provider is given a connection its disposal leaves open.
    /// </summary>
    private static async ValueTask<TransferResult?> HandshakeAsync(LdapExchange exchange, IConnection plaintext, ITlsProvider? tlsProvider, ITransferContext context)
    {
        if (tlsProvider is null)
        {
            return TransferResult.Failure(CurlExitCode.UseSslFailed, FailurePrefix + "no TLS provider to start TLS with");
        }

        ConnectResult secured = await tlsProvider.AuthenticateAsClientAsync(new KeptOpenConnection(plaintext), context.Url.IdnHost, context.Events, context.CancellationToken).ConfigureAwait(false);
        if (secured.Connection is not { } connection)
        {
            var failure = new TransferResult(CurlExitCode.UseSslFailed, 0, secured.ErrorMessage);
            return await LdapBind.UnbindAndFailAsync(exchange, failure, context.CancellationToken).ConfigureAwait(false);
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
    /// result code's mapping, else exit 64 <c>Requested SSL level failed</c>, after an UnbindRequest,
    /// with an AbandonRequest before it for a search entry or reference; a malformed reply is exit 64
    /// and sends nothing.
    /// </summary>
    private static async ValueTask<TransferResult?> RefusedAsync(LdapExchange exchange, LdapBindReply reply, CancellationToken cancellationToken)
    {
        if (reply.Status == LdapBindReplyStatus.Malformed)
        {
            return TransferResult.Failure(CurlExitCode.UseSslFailed, FailurePrefix + "Can't contact LDAP server");
        }

        if (reply.Status == LdapBindReplyStatus.SearchResult)
        {
            await exchange.AbandonAsync(StartTlsMessageId, cancellationToken).ConfigureAwait(false);
        }

        TransferResult failure = LdapBind.OpenLdapMappedFailure(reply.ResultCode) ?? TransferResult.Failure(CurlExitCode.UseSslFailed, UseSslFailedText);
        return await LdapBind.UnbindAndFailAsync(exchange, failure, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The plaintext connection as the TLS provider sees it: disposing it leaves the connection open.</summary>
    private sealed class KeptOpenConnection(IConnection plaintext) : IConnection
    {
        public bool IsSecure => plaintext.IsSecure;

        public EndPoint? RemoteEndPoint => plaintext.RemoteEndPoint;

        public EndPoint? LocalEndPoint => plaintext.LocalEndPoint;

        public nint? SocketHandle => plaintext.SocketHandle;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
            plaintext.ReadAsync(buffer, cancellationToken);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
            plaintext.WriteAsync(buffer, cancellationToken);

        public ValueTask FlushAsync(CancellationToken cancellationToken) => plaintext.FlushAsync(cancellationToken);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
