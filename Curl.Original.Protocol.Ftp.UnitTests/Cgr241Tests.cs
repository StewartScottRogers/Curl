using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp.Fakes;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// CGR-241 (BL-3770): in active mode a <c>425</c> that arrives in the same read as RETR's
/// <c>150</c> ends the wait with exit 10 and, for a download whose size is known, curl 8.22.0's
/// "Received only partial file: 0 bytes" rather than the accept-failed message.
/// </summary>
[TestClass]
public sealed class Cgr241Tests
{
    private const string Url = "ftp://127.0.0.1:21357/1211";

    private const string LoggedIn = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n"
        + "200 EPRT command successful\r\n200 Type set\r\n";

    private static readonly IPEndPoint Listening = new(IPAddress.Loopback, 52273);

    [TestMethod]
    public async Task ExecuteAsync_RefusalInTheSameReadAsTheOpeningReply_SizeKnown_ReportsPartialFile()
    {
        TransferResult result = await RunAsync("213 18\r\n150 Opening\r\n425 Cannot open data connection\r\n221 Bye\r\n");

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpAcceptFailed, "Received only partial file: 0 bytes"), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusalInTheSameReadAsTheOpeningReply_SizeInOpeningReply_ReportsPartialFile()
    {
        TransferResult result = await RunAsync("500 Unknown command\r\n150 Opening BINARY mode data connection for 1211 (18 bytes).\r\n425 Cannot open data connection\r\n221 Bye\r\n");

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpAcceptFailed, "Received only partial file: 0 bytes"), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusalInTheSameReadAsTheOpeningReply_EmptySizeAndHintInOpeningReply_ReportsPartialFile()
    {
        TransferResult result = await RunAsync("213 0\r\n150 Opening (18 bytes)\r\n425 Cannot open data connection\r\n221 Bye\r\n");

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpAcceptFailed, "Received only partial file: 0 bytes"), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusalInTheSameReadAsTheOpeningReply_NoSizeAnywhere_ReportsAcceptFailed()
    {
        TransferResult result = await RunAsync("500 Unknown command\r\n150 Opening\r\n425 Cannot open data connection\r\n221 Bye\r\n");

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpAcceptFailed, "FTP: The server failed to connect to data port"), result);
    }

    private static async Task<TransferResult> RunAsync(string replies)
    {
        var control = new ScriptedConnection([Encoding.Latin1.GetBytes(LoggedIn + replies)]) { LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 55129) };
        var context = MutableContext.Build(
            new TransferContext { Url = CurlUrl.Parse(Url), Output = new MemoryStream() },
            mutable =>
            {
                mutable.FtpPort = "-";
                mutable.FtpAcceptTimeout = TimeSpan.FromSeconds(5);
            });
        var handler = new FtpProtocolHandler(
            new QueuedConnector(ConnectResult.Connected(control)),
            new QueuedListener(ListenResult.Listening(new ScriptedPendingConnection(Listening, null))),
            new QueuedTlsProvider());

        TransferResult result = await handler.ExecuteAsync(context);

        return result with { Report = null };
    }
}
