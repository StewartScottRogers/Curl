using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ldap;

/// <summary>
/// One LDAP session on a connection: numbers each request from messageID 1 on, as both
/// builds do, sends it, and reads the reply a bind waits for.
/// </summary>
/// <param name="connection">The connection to the LDAP server.</param>
/// <param name="writer">Writes the requests with the dialect's length form.</param>
internal sealed class LdapExchange(IConnection connection, LdapBerWriter writer)
{
    private readonly LdapMessageReader reader = new(connection);

    private int nextMessageId = 1;

    /// <summary>Sends a simple BindRequest and reads the server's answer.</summary>
    /// <param name="version">The protocol version, 3, or 2 for WinLDAP's retry.</param>
    /// <param name="name">The DN to bind as; empty for the anonymous bind.</param>
    /// <param name="password">The password; empty for the anonymous bind.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The server's answer.</returns>
    public async ValueTask<LdapBindReply> BindAsync(int version, string name, string password, CancellationToken cancellationToken)
    {
        int messageId = nextMessageId++;
        await SendAsync(LdapRequests.Bind(writer, messageId, version, name, password), cancellationToken).ConfigureAwait(false);
        (LdapReadStatus status, byte[] message) = await reader.ReadMessageAsync(cancellationToken).ConfigureAwait(false);
        return status switch
        {
            LdapReadStatus.Message => LdapBindResponse.Decode(message, messageId),
            LdapReadStatus.Closed => new LdapBindReply(LdapBindReplyStatus.Closed, 0),
            _ => new LdapBindReply(LdapBindReplyStatus.Malformed, 0),
        };
    }

    /// <summary>Sends an UnbindRequest, which the server does not answer.</summary>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes when the request has been flushed.</returns>
    public ValueTask UnbindAsync(CancellationToken cancellationToken) =>
        SendAsync(LdapRequests.Unbind(writer, nextMessageId++), cancellationToken);

    private async ValueTask SendAsync(byte[] message, CancellationToken cancellationToken)
    {
        await connection.WriteAsync(message, cancellationToken).ConfigureAwait(false);
        await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
