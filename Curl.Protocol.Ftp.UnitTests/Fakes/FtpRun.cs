using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp.Fakes;

/// <summary>
/// One transfer run against a scripted control and data connection, and what it left
/// behind.
/// </summary>
public sealed record FtpRun(
    TransferResult Result,
    ScriptedConnection Control,
    ScriptedConnection Data,
    QueuedConnector Connector,
    Stream Output)
{
    public byte[] SentBytes => Control.Sent;

    public string Sent => Encoding.Latin1.GetString(Control.Sent);

    public string OutputText => Encoding.Latin1.GetString(((MemoryStream)Output).ToArray());

    public static Task<FtpRun> ExecuteAsync(
        string url,
        string replies,
        string data = "",
        Func<TransferContext, TransferContext>? adjust = null)
    {
        byte[] dataBytes = Encoding.Latin1.GetBytes(data);
        return ExecuteAsync(
            url,
            new ScriptedConnection(Encoding.Latin1.GetBytes(replies)),
            dataBytes.Length == 0 ? new ScriptedConnection() : new ScriptedConnection(dataBytes),
            adjust);
    }

    public static async Task<FtpRun> ExecuteAsync(
        string url,
        ScriptedConnection control,
        ScriptedConnection data,
        Func<TransferContext, TransferContext>? adjust = null)
    {
        var connector = new QueuedConnector(ConnectResult.Connected(control), ConnectResult.Connected(data));
        var context = new TransferContext { Url = CurlUrl.Parse(url), Output = new MemoryStream() };
        context = adjust?.Invoke(context) ?? context;

        TransferResult result = await new FtpProtocolHandler(connector).ExecuteAsync(context);

        return new FtpRun(result, control, data, connector, context.Output);
    }
}
