using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Pop3.Fakes;

/// <summary>
/// One POP3 transfer run against a scripted connection, and what it left behind.
/// </summary>
public sealed record Pop3Run(
    TransferResult Result,
    ScriptedConnection Connection,
    QueuedConnector Connector,
    QueuedTlsProvider Tls,
    byte[] Output,
    RecordingProgress Progress)
{
    /// <summary>Gets every byte written to the plaintext connection, as Latin-1 text.</summary>
    public string Sent => Encoding.Latin1.GetString(Connection.Sent);

    public static async Task<Pop3Run> ExecuteAsync(
        string url,
        ScriptedConnection connection,
        TransportSecurityLevel sslLevel = TransportSecurityLevel.None,
        params ConnectResult[] handshakes)
    {
        using var output = new MemoryStream();
        var progress = new RecordingProgress();
        var context = new TransferContext { Url = CurlUrl.Parse(url), Output = output, SslLevel = sslLevel, Progress = progress };
        var connector = new QueuedConnector(ConnectResult.Connected(connection));
        var tls = new QueuedTlsProvider(handshakes);

        TransferResult result = await new Pop3ProtocolHandler(connector, tls).ExecuteAsync(context);

        return new Pop3Run(result, connection, connector, tls, output.ToArray(), progress);
    }
}
