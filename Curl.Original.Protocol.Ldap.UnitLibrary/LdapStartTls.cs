using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Upgrades a plaintext <c>ldap</c> session to TLS with the StartTLS extended operation, as
/// curl's OpenLDAP build does for <c>--ssl</c> and <c>--ssl-reqd</c> before it binds (BL-3542).
/// </summary>
/// <remarks>
/// When the server refuses StartTLS, <c>--ssl</c> goes on in plaintext and <c>--ssl-reqd</c>
/// fails with exit 1 <c>Unsupported protocol</c> after an UnbindRequest, as measured against
/// curl 8.18 (ADR-0892). When the server accepts it, the session continues through the TLS handshake of
/// <see cref="ITlsProvider" />, whose failure is returned as it reported it.
/// </remarks>
internal static class LdapStartTls
{
    private const string FailurePrefix = "LDAP local: ";

    /// <summary>The text for a server that closed or answered with something other than an ExtendedResponse.</summary>
    private const string CannotContactServer = "Can't contact LDAP server";

    /// <summary>Sends StartTLS on <paramref name="exchange" /> and, when the server accepts it, moves the session onto TLS.</summary>
    /// <param name="exchange">The plaintext session, nothing sent on it yet.</param>
    /// <param name="plaintext">The session's connection, handed to <paramref name="tlsProvider" /> for the handshake.</param>
    /// <param name="tlsProvider">Performs the handshake; <see langword="null" /> when the handler has none.</param>
    /// <param name="context">The transfer: its TLS level, host, events and cancellation.</param>
    /// <returns>The failure that ends the transfer, or <see langword="null" /> to go on and bind.</returns>
    public static async ValueTask<TransferResult?> UpgradeAsync(LdapExchange exchange, IConnection plaintext, ITlsProvider? tlsProvider, ITransferContext context)
    {
        LdapBindReply reply = await exchange.StartTlsAsync(context.CancellationToken).ConfigureAwait(false);
        if (!reply.IsSuccess)
        {
            return context.SslLevel == TransportSecurityLevel.Try ? null : await RefusedAsync(exchange, reply, context.CancellationToken).ConfigureAwait(false);
        }

        if (tlsProvider is null)
        {
            return TransferResult.Failure(CurlExitCode.UseSslFailed, FailurePrefix + "no TLS provider to start TLS with");
        }

        ConnectResult secured = await tlsProvider.AuthenticateAsClientAsync(plaintext, context.Url.IdnHost, context.Events, context.CancellationToken).ConfigureAwait(false);
        if (secured.Connection is not { } connection)
        {
            // OpenLDAP reports any StartTLS failure, a certificate verification one included, as
            // exit 64 with no sslcerts help (measured against curl 8.18, STD-172).
            CurlExitCode exitCode = secured.ExitCode == CurlExitCode.PeerFailedVerification ? CurlExitCode.UseSslFailed : secured.ExitCode;
            return new TransferResult(exitCode, 0, secured.ErrorMessage);
        }

        exchange.UseSecuredConnection(connection);
        return null;
    }

    /// <summary>
    /// What <c>--ssl-reqd</c> fails with when the server refuses StartTLS: an answered refusal is exit 1
    /// <c>Unsupported protocol</c> after an UnbindRequest (measured, ADR-0892); a close or a reply that
    /// is not an ExtendedResponse is exit 64 and sends nothing.
    /// </summary>
    private static async ValueTask<TransferResult?> RefusedAsync(LdapExchange exchange, LdapBindReply reply, CancellationToken cancellationToken) =>
        reply.Status == LdapBindReplyStatus.Answered
            ? await LdapBind.UnbindAndFailAsync(exchange, TransferResult.Failure(CurlExitCode.UnsupportedProtocol, "Unsupported protocol"), cancellationToken).ConfigureAwait(false)
            : TransferResult.Failure(CurlExitCode.UseSslFailed, FailurePrefix + CannotContactServer);
}
