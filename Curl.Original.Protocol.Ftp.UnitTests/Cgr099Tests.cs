using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp.Fakes;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// CGR-099 (BL-3628): in active mode a 4xx or 5xx reply that arrives behind <c>RETR</c>'s
/// <c>150</c> while curl 8.22.0 waits for the server to connect ends the wait at once with
/// exit 10 after <c>QUIT</c>, measured against Record-CurlExchange.ps1 -Ftp with
/// <c>RETR=150 Opening\r\n{PAUSE}\r\n425 Cannot open data connection</c> and <c>-P localhost</c>.
/// </summary>
[TestClass]
public sealed class Cgr099Tests
{
    private const string Url = "ftp://127.0.0.1:20964/file.txt";

    private const string LoggedIn = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n"
        + "200 EPRT command successful\r\n200 Type set\r\n";

    private const string Opened = "150 Opening BINARY mode data connection\r\n";

    private const string ExpectedSent = "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\nEPRT |1|127.0.0.1|52273|\r\n"
        + "TYPE I\r\nSIZE file.txt\r\nRETR file.txt\r\nQUIT\r\n";

    private static readonly IPEndPoint Listening = new(IPAddress.Loopback, 52273);

    [TestMethod]
    public async Task ExecuteAsync_RefusalArrivesWhileAwaitingConnect_SizeKnown_QuitsWithExit10AndPartialFileMessage()
    {
        // curl -sS -P localhost ftp://127.0.0.1:20964/file.txt, SIZE 18: exit 10 in 1.5 s,
        // "curl: (10) Received only partial file: 0 bytes", QUIT sent.
        var pending = new ScriptedPendingConnection(Listening, null);

        (TransferResult result, string sent) = await RunAsync("213 18\r\n" + Opened + ScriptedConnection.NextRead + "425 Cannot open data connection\r\n221 Bye\r\n", pending);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpAcceptFailed, "Received only partial file: 0 bytes"), result);
        Assert.AreEqual(ExpectedSent, sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusalArrivesWhileAwaitingConnect_SizeUnknown_QuitsWithExit10AndAcceptFailedMessage()
    {
        // The same with SIZE=500 Unknown command: "curl: (10) FTP: The server failed to connect to data port".
        var pending = new ScriptedPendingConnection(Listening, null);

        (TransferResult result, string sent) = await RunAsync("500 Unknown command\r\n" + Opened + ScriptedConnection.NextRead + "421 Timeout\r\n221 Bye\r\n", pending);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpAcceptFailed, "FTP: The server failed to connect to data port"), result);
        Assert.AreEqual(ExpectedSent, sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_PositiveReplyArrivesBeforeTheServerConnects_KeepsWaitingAndDownloads()
    {
        var data = new ScriptedConnection(Encoding.Latin1.GetBytes("hello"));
        var pending = new LateConnectingPendingConnection(ConnectResult.Connected(data));

        (TransferResult result, string sent) = await RunAsync("213 5\r\n" + Opened + ScriptedConnection.NextRead + "226 Transfer complete\r\n221 Bye\r\n", pending);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(ExpectedSent, sent);
    }

    private static async Task<(TransferResult Result, string Sent)> RunAsync(string replies, IPendingConnection pending)
    {
        var control = new ScriptedConnection([.. SplitReads(LoggedIn + replies)]) { LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 55129) };
        var context = MutableContext.Build(
            new TransferContext { Url = CurlUrl.Parse(Url), Output = new MemoryStream() },
            mutable =>
            {
                mutable.FtpPort = "-";
                mutable.FtpAcceptTimeout = TimeSpan.FromSeconds(5);
            });
        var handler = new FtpProtocolHandler(
            new QueuedConnector(ConnectResult.Connected(control)),
            new QueuedListener(ListenResult.Listening(pending)),
            new QueuedTlsProvider());

        TransferResult result = await handler.ExecuteAsync(context);

        return (result with { Report = null }, Encoding.Latin1.GetString(control.Sent));
    }

    private static IEnumerable<byte[]> SplitReads(string replies) =>
        replies.Split(ScriptedConnection.NextRead).Select(Encoding.Latin1.GetBytes);

    /// <summary>A pending connection the server connects to a little after the accept starts.</summary>
    private sealed class LateConnectingPendingConnection(ConnectResult accepted) : IPendingConnection
    {
        public EndPoint LocalEndPoint => Listening;

        public bool HasConnectionWaiting => false;

        public async ValueTask<ConnectResult> AcceptAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
            return accepted;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
