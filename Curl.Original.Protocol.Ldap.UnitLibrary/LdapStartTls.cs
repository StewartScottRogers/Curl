using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Upgrades a plaintext <c>ldap</c> session to TLS with the StartTLS extended operation, as
/// curl's OpenLDAP build does for <c>--ssl</c> and <c>--ssl-reqd</c> before it binds (BL-3542).
/// </summary>
/// <remarks>
/// When the server refuses StartTLS, <c>--ssl</c> goes on in plaintext and <c>--ssl-reqd</c>
/// fails with exit 64 and libldap's text for the result code, as curl's <c>openldap.c</c>
/// does. When the server accepts it, the session continues through the TLS handshake of
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
            return context.SslLevel == TransportSecurityLevel.Try ? null : Refused(reply);
        }

        if (tlsProvider is null)
        {
            return TransferResult.Failure(CurlExitCode.UseSslFailed, FailurePrefix + "no TLS provider to start TLS with");
        }

        ConnectResult secured = await tlsProvider.AuthenticateAsClientAsync(plaintext, context.Url.IdnHost, context.Events, context.CancellationToken).ConfigureAwait(false);
        if (secured.Connection is not { } connection)
        {
            return new TransferResult(secured.ExitCode, 0, secured.ErrorMessage);
        }

        exchange.UseSecuredConnection(connection);
        return null;
    }

    /// <summary>The exit 64 <c>--ssl-reqd</c> fails with when the server refuses StartTLS.</summary>
    private static TransferResult Refused(LdapBindReply reply) =>
        TransferResult.Failure(
            CurlExitCode.UseSslFailed,
            FailurePrefix + (reply.Status == LdapBindReplyStatus.Answered ? LibLdapResultText.Of(reply.ResultCode) : CannotContactServer));
}
