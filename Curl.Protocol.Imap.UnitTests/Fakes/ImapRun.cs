using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Imap.Fakes;

/// <summary>
/// One IMAP transfer run against a scripted connection, and what it left behind.
/// </summary>
public sealed record ImapRun(
    TransferResult Result,
    ScriptedConnection Connection,
    QueuedConnector Connector,
    QueuedTlsProvider Tls)
{
    /// <summary>Gets every byte written to the plaintext connection, as Latin-1 text.</summary>
    public string Sent => Encoding.Latin1.GetString(Connection.Sent);

    public static Task<ImapRun> ExecuteAsync(
        string url,
        ScriptedConnection connection,
        TransportSecurityLevel sslLevel = TransportSecurityLevel.None,
        params ConnectResult[] handshakes) =>
        ExecuteAsync(new TransferContext { Url = CurlUrl.Parse(url), Output = Stream.Null, SslLevel = sslLevel }, connection, handshakes);

    public static Task<ImapRun> ExecuteAsync(
        TransferContext context,
        ScriptedConnection connection,
        params ConnectResult[] handshakes) =>
        ExecuteAsync(context, connection, null, handshakes);

    /// <summary>
    /// Runs <paramref name="context" /> through a handler built with
    /// <paramref name="saslAuthenticator" />, or without one when it is <see langword="null" />.
    /// </summary>
    public static async Task<ImapRun> ExecuteAsync(
        TransferContext context,
        ScriptedConnection connection,
        ISaslAuthenticator? saslAuthenticator,
        params ConnectResult[] handshakes)
    {
        var connector = new QueuedConnector(ConnectResult.Connected(connection));
        var tls = new QueuedTlsProvider(handshakes);
        ImapProtocolHandler handler = saslAuthenticator is null
            ? new ImapProtocolHandler(connector, tls)
            : new ImapProtocolHandler(connector, tls, saslAuthenticator);

        TransferResult result = await handler.ExecuteAsync(context);

        return new ImapRun(result, connection, connector, tls);
    }
}
