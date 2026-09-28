using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smtp.Fakes;

/// <summary>
/// One SMTP transfer run against a scripted connection, and what it left behind.
/// </summary>
public sealed record SmtpRun(
    TransferResult Result,
    ScriptedConnection Connection,
    QueuedConnector Connector,
    QueuedTlsProvider Tls)
{
    /// <summary>The host name the handler is told the local machine has.</summary>
    public const string LocalHostName = "local-machine";

    /// <summary>Gets every byte written to the plaintext connection, as Latin-1 text.</summary>
    public string Sent => Encoding.Latin1.GetString(Connection.Sent);

    public static Task<SmtpRun> ExecuteAsync(
        string url,
        ScriptedConnection connection,
        TransportSecurityLevel sslLevel = TransportSecurityLevel.None,
        params ConnectResult[] handshakes) =>
        ExecuteAsync(new TransferContext { Url = CurlUrl.Parse(url), Output = Stream.Null, SslLevel = sslLevel }, connection, handshakes);

    public static async Task<SmtpRun> ExecuteAsync(
        TransferContext context,
        ScriptedConnection connection,
        params ConnectResult[] handshakes)
    {
        var connector = new QueuedConnector(ConnectResult.Connected(connection));
        var tls = new QueuedTlsProvider(handshakes);

        TransferResult result = await new SmtpProtocolHandler(connector, tls, () => LocalHostName).ExecuteAsync(context);

        return new SmtpRun(result, connection, connector, tls);
    }
}
