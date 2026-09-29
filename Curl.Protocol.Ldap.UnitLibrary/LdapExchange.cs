using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ldap;

/// <summary>
/// One LDAP session on a connection: numbers each request from messageID 1 on, as both
/// builds do, sends it, and reads the replies a bind and a search wait for; after WinLDAP's
/// logon bind, through the SASL security layer (<see cref="StartSecurityLayer" />).
/// </summary>
/// <param name="connection">The connection to the LDAP server, which the session does not dispose.</param>
/// <param name="writer">Writes the requests with the dialect's length form.</param>
internal sealed class LdapExchange(IConnection connection, LdapBerWriter writer) : IAsyncDisposable
{
    private IConnection connection = connection;

    private LdapMessageReader reader = new(connection);

    private LdapSaslSecurityLayer? securityLayer;

    private int nextMessageId = 1;

    /// <summary>
    /// Sends and reads every later message through the SASL security layer, sealed with the
    /// keys of <paramref name="authentication" />, which the session now owns.
    /// </summary>
    /// <param name="authentication">The logon bind's complete authentication.</param>
    public void StartSecurityLayer(ILdapLogonAuthentication authentication)
    {
        securityLayer = new LdapSaslSecurityLayer(connection, authentication);
        connection = securityLayer;
        reader = new LdapMessageReader(securityLayer);
    }

    /// <summary>Disposes the security layer's keys, if the session has one; the connection stays open.</summary>
    /// <returns>A task that completes when the keys have been disposed.</returns>
    public ValueTask DisposeAsync() => securityLayer?.DisposeAsync() ?? ValueTask.CompletedTask;

    /// <summary>Sends a simple BindRequest and reads the server's answer.</summary>
    /// <param name="version">The protocol version, 3, or 2 for WinLDAP's retry.</param>
    /// <param name="name">The DN to bind as; empty for the anonymous bind.</param>
    /// <param name="password">The password; empty for the anonymous bind.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The server's answer.</returns>
    public ValueTask<LdapBindReply> BindAsync(int version, string name, string password, CancellationToken cancellationToken) =>
        BindWithAsync(messageId => LdapRequests.Bind(writer, messageId, version, name, password), cancellationToken);

    /// <summary>Sends a BindRequest with SASL authentication and reads the server's answer.</summary>
    /// <param name="mechanism">The SASL mechanism's name.</param>
    /// <param name="credentials">The mechanism's token.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The server's answer.</returns>
    public ValueTask<LdapBindReply> SaslBindAsync(string mechanism, byte[] credentials, CancellationToken cancellationToken) =>
        BindWithAsync(messageId => LdapRequests.SaslBind(writer, messageId, mechanism, credentials), cancellationToken);

    /// <summary>Sends a BindRequest with a Sicily authentication choice and reads the server's answer.</summary>
    /// <param name="name">The BindRequest's name.</param>
    /// <param name="choice">The authentication choice's tag number, 10 or 11.</param>
    /// <param name="token">The NTLM message.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The server's answer.</returns>
    public ValueTask<LdapBindReply> SicilyBindAsync(string name, int choice, byte[] token, CancellationToken cancellationToken) =>
        BindWithAsync(messageId => LdapRequests.SicilyBind(writer, messageId, name, choice, token), cancellationToken);

    /// <summary>Sends the BindRequest <paramref name="encode" /> writes for the next messageID and reads the server's answer.</summary>
    private async ValueTask<LdapBindReply> BindWithAsync(Func<int, byte[]> encode, CancellationToken cancellationToken)
    {
        int messageId = nextMessageId++;
        await SendAsync(encode(messageId), cancellationToken).ConfigureAwait(false);
        (LdapReadStatus status, byte[] message) = await reader.ReadMessageAsync(cancellationToken).ConfigureAwait(false);
        return status switch
        {
            LdapReadStatus.Message => LdapBindResponse.Decode(message, messageId),
            LdapReadStatus.Closed => new LdapBindReply(LdapBindReplyStatus.Closed, 0),
            _ => new LdapBindReply(LdapBindReplyStatus.Malformed, 0),
        };
    }

    /// <summary>Gets the writer the requests are written with, in the dialect's length form.</summary>
    public LdapBerWriter Writer => writer;

    /// <summary>
    /// Takes the next messageID. A search takes one before its filter is known to be good, so
    /// a filter the build refuses still uses it up, as both builds do.
    /// </summary>
    /// <returns>The messageID.</returns>
    public int TakeMessageId() => nextMessageId++;

    /// <summary>Reads the next reply to the search with <paramref name="messageId" />.</summary>
    /// <param name="messageId">The SearchRequest's messageID.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>What arrived; <see cref="LdapSearchReplyKind.Lost" /> when the server closed or sent bytes that cannot start an LDAPMessage.</returns>
    public async ValueTask<LdapSearchReply> ReadSearchReplyAsync(int messageId, CancellationToken cancellationToken)
    {
        (LdapReadStatus status, byte[] message) = await reader.ReadMessageAsync(cancellationToken).ConfigureAwait(false);
        return status == LdapReadStatus.Message
            ? LdapSearchResponse.Decode(message, messageId)
            : LdapSearchReply.Of(LdapSearchReplyKind.Lost);
    }

    /// <summary>Sends an AbandonRequest for the request with <paramref name="abandoned" />, which the server does not answer.</summary>
    /// <param name="abandoned">The messageID of the request to abandon.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes when the request has been flushed.</returns>
    public ValueTask AbandonAsync(int abandoned, CancellationToken cancellationToken) =>
        SendAsync(LdapRequests.Abandon(writer, nextMessageId++, abandoned), cancellationToken);

    /// <summary>Sends an UnbindRequest, which the server does not answer.</summary>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes when the request has been flushed.</returns>
    public ValueTask UnbindAsync(CancellationToken cancellationToken) =>
        SendAsync(LdapRequests.Unbind(writer, nextMessageId++), cancellationToken);

    /// <summary>Sends <paramref name="message" /> and flushes it.</summary>
    /// <param name="message">One whole LDAPMessage.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes when the message has been flushed.</returns>
    public async ValueTask SendAsync(byte[] message, CancellationToken cancellationToken)
    {
        await connection.WriteAsync(message, cancellationToken).ConfigureAwait(false);
        await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
