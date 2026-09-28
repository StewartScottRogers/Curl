using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp.Fakes;

/// <summary>
/// One transfer run against a scripted control and data connection, and what it left
/// behind. <see cref="Result" /> is the handler's result with its
/// <see cref="TransferResult.Report" /> set aside in <see cref="Report" />, so a test pins
/// the outcome and the report separately.
/// </summary>
public sealed record FtpRun(
    TransferResult Result,
    TransferReport? Report,
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

        return new FtpRun(result with { Report = null }, result.Report, control, data, connector, context.Output);
    }
}
