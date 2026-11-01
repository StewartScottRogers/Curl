using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp.Fakes;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// BL-3906: the <c>-v</c> lines curl 8.22.0 writes after <c>Data conn was not available immediately</c>
/// when a 4xx reply behind <c>RETR</c>'s <c>150</c> ends the wait for the server's active-mode
/// connection, measured with Record-CurlExchange.ps1 -Ftp, <c>-FtpReply 'RETR=150 ...\r\n425 ...'</c>
/// (with <c>{PAUSE}</c> for the late reply), <c>SIZE=213 18</c> or <c>SIZE=500 Unknown command</c>,
/// and <c>-v -P localhost</c>.
/// </summary>
[TestClass]
public sealed class FtpActiveAcceptVerboseTests
{
    private const string LoggedIn = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n"
        + "200 EPRT command successful\r\n200 Type set\r\n";

    private const string Opened = "150 Opening BINARY mode data connection\r\n";

    private const string Refusal = "425 Cannot open data connection\r\n";

    private static readonly IPEndPoint Listening = new(IPAddress.Loopback, 52273);

    [TestMethod]
    public async Task ExecuteAsync_BufferedRefusalSizeKnown_ReportsCtrlConnHasDataAndThePartialFile()
    {
        string[] lines = await InfoAfterNotAvailableAsync("213 18\r\n" + Opened + Refusal + "221 Bye\r\n");

        CollectionAssert.AreEqual(
            new[]
            {
                "Ctrl conn has data while waiting for data conn",
                "Remembering we are in directory \"\"",
                "Received only partial file: 0 bytes",
            },
            lines[..3]);
    }

    [TestMethod]
    public async Task ExecuteAsync_BufferedRefusalSizeUnknown_ReportsCtrlConnHasDataAndNoFailureText()
    {
        string[] lines = await InfoAfterNotAvailableAsync("500 Unknown command\r\n" + Opened + Refusal + "221 Bye\r\n");

        CollectionAssert.AreEqual(
            new[] { "Ctrl conn has data while waiting for data conn", "Remembering we are in directory \"\"" },
            lines[..2]);
        Assert.IsFalse(lines.Any(line => line.Contains("failed to connect", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_LateRefusalSizeKnown_ReportsNoWaitingLinesBeforeThePartialFile()
    {
        string[] lines = await InfoAfterNotAvailableAsync("213 18\r\n" + Opened + ScriptedConnection.NextRead + Refusal + "221 Bye\r\n");

        CollectionAssert.AreEqual(
            new[] { "Remembering we are in directory \"\"", "Received only partial file: 0 bytes" },
            lines[..2]);
    }

    [TestMethod]
    public async Task ExecuteAsync_LateRefusalSizeUnknown_ReportsOnlyTheRememberedDirectory()
    {
        string[] lines = await InfoAfterNotAvailableAsync("500 Unknown command\r\n" + Opened + ScriptedConnection.NextRead + Refusal + "221 Bye\r\n");

        Assert.AreEqual("Remembering we are in directory \"\"", lines[0]);
        Assert.IsFalse(lines.Any(line => line.Contains("Ready to accept", StringComparison.Ordinal) || line.Contains("negative response", StringComparison.Ordinal)));
    }

    private static async Task<string[]> InfoAfterNotAvailableAsync(string replies)
    {
        var events = new RecordingTransferEvents();
        var control = new ScriptedConnection([.. (LoggedIn + replies).Split(ScriptedConnection.NextRead).Select(Encoding.Latin1.GetBytes)])
        {
            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 55129),
        };
        var context = MutableContext.Build(
            new TransferContext { Url = CurlUrl.Parse("ftp://127.0.0.1:20964/file.txt"), Output = new MemoryStream() },
            mutable =>
            {
                mutable.Events = events;
                mutable.FtpPort = "-";
                mutable.FtpAcceptTimeout = TimeSpan.FromSeconds(5);
            });
        var handler = new FtpProtocolHandler(
            new QueuedConnector(ConnectResult.Connected(control)),
            new QueuedListener(ListenResult.Listening(new ScriptedPendingConnection(Listening, null))),
            new QueuedTlsProvider());

        await handler.ExecuteAsync(context);

        return [.. events.Info.SkipWhile(line => line != "Data conn was not available immediately").Skip(1)];
    }
}
