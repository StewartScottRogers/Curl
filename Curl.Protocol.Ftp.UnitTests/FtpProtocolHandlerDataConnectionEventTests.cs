using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins the data-connection lines <c>-v</c> and <c>--trace</c> see for FTP against curl
/// 8.21.0 (the Schannel build), measured on 2026-09-29 with <c>Record-CurlExchange.ps1 -Ftp
/// -FtpData 'hello ftp\r\n'</c> (BL-931, whose Notes hold the recordings): the <c>* </c> info
/// lines in order among the command and reply lines, and the bytes of each <c>Recv data</c>
/// and <c>Send data</c> block, the zero-byte read that ends a download included. The
/// <c>Trying</c> and <c>Established</c> lines of a passive connect are the connector's and are
/// not reported here.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerDataConnectionEventTests
{
    private const string Host = "127.0.0.1";

    private const int ControlPort = 47931;

    private const string File = "hello ftp\r\n";

    private const string LoggedIn = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private const string Epsv = "229 Entering Extended Passive Mode (|||55801|)\r\n";

    private const string Pasv = "227 Entering Passive Mode (127,0,0,1,253,48)\r\n";

    private const string Opened = "150 Opening BINARY mode data connection\r\n";

    private const string Complete = "226 Transfer complete\r\n";

    private const string Bye = "221 Bye\r\n";

    private const string Retrieved = "200 Type set\r\n213 11\r\n" + Opened + Complete + Bye;

    private static readonly string[] LoggedInTranscript =
    [
        "< 220 Recorder ready\r\n",
        "> USER anonymous\r\n",
        "< 331 Password required\r\n",
        "> PASS ftp@example.com\r\n",
        "< 230 Logged in\r\n",
        "> PWD\r\n",
        "< 257 \"/\" is current directory\r\n",
        "* Entry path is '/'",
    ];

    private const string SamePath = "* Request has same path as previous transfer";

    private static readonly string[] EpsvTranscript =
    [
        "> EPSV\r\n",
        "* Connect data stream passively",
        "< " + Epsv,
        "* Connecting to 127.0.0.1 port 55801",
    ];

    [TestMethod]
    public async Task ExecuteAsync_PassiveRetr_ReportsCurlsLinesAndTheDataInOrder()
    {
        // curl -v ftp://127.0.0.1:47931/dir/file.txt; --trace-ascii - for the data blocks.
        DataRun run = await RunAsync("/dir/file.txt", LoggedIn + "250 OK\r\n" + Epsv + Retrieved, _ => { });

        string[] expected =
        [
            .. LoggedInTranscript,
            "> CWD dir\r\n",
            "< 250 OK\r\n",
            .. EpsvTranscript,
            "> TYPE I\r\n",
            "< 200 Type set\r\n",
            "> SIZE file.txt\r\n",
            "< 213 11\r\n",
            "> RETR file.txt\r\n",
            "< " + Opened,
            "* Maxdownload = -1",
            "* Getting file with size: 11",
            "<= " + File,
            "<= ",
            "* Remembering we are in directory \"dir/\"",
            "< " + Complete,
            "* Connection #0 to host 127.0.0.1:47931 left intact",
        ];
        CollectionAssert.AreEqual(expected, run.Events.Transcript);
        CollectionAssert.AreEqual(new[] { File, string.Empty }, run.Events.DataReceived);
        Assert.AreEqual(TransferResult.Success(11), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ActiveRetr_ReportsTheAcceptedConnectionBeforeTheData()
    {
        // curl -v -P - ftp://127.0.0.1:47931/file.txt
        var accepted = new ScriptedConnection(Encoding.Latin1.GetBytes(File)) { RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 55823) };
        DataRun run = await RunAsync(
            "/file.txt",
            LoggedIn + "200 EPRT command successful\r\n" + Retrieved,
            context => context.FtpPort = "-",
            pending: new ScriptedPendingConnection(new IPEndPoint(IPAddress.Loopback, 55822), ConnectResult.Connected(accepted)));

        string[] expected =
        [
            .. LoggedInTranscript,
            SamePath,
            "> EPRT |1|127.0.0.1|55822|\r\n",
            "< 200 EPRT command successful\r\n",
            "* Connect data stream actively",
            "> TYPE I\r\n",
            "< 200 Type set\r\n",
            "> SIZE file.txt\r\n",
            "< 213 11\r\n",
            "> RETR file.txt\r\n",
            "< " + Opened,
            "* Maxdownload = -1",
            "* Getting file with size: 11",
            "* Data conn was not available immediately",
            "* Ready to accept data connection from server",
            "* Connection accepted from server",
            "* Established 2nd connection to 127.0.0.1 (127.0.0.1 port 55823) from 127.0.0.1 port 55822 ",
            "<= " + File,
            "<= ",
            "* Remembering we are in directory \"\"",
            "< " + Complete,
            "* Connection #0 to host 127.0.0.1:47931 left intact",
        ];
        CollectionAssert.AreEqual(expected, run.Events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_ActiveRetrWithAnUnknownServerEnd_ReportsNoEstablishedLine()
    {
        DataRun run = await RunAsync(
            "/file.txt",
            LoggedIn + "200 EPRT command successful\r\n" + Retrieved,
            context => context.FtpPort = "-",
            pending: new ScriptedPendingConnection(new IPEndPoint(IPAddress.Loopback, 55822), ConnectResult.Connected(new ScriptedConnection(Encoding.Latin1.GetBytes(File)))));

        CollectionAssert.AreEqual(
            new[] { "Connection accepted from server", "Remembering we are in directory \"\"" },
            run.Events.Info.Skip(7).Take(2).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_ActiveWithPort_ReportsTheActiveLineAfterPort()
    {
        // curl -v -P - --disable-eprt ftp://127.0.0.1:47931/file.txt
        DataRun run = await RunAsync(
            "/file.txt",
            LoggedIn + "200 PORT command successful\r\n" + Retrieved,
            context =>
            {
                context.FtpPort = "-";
                context.FtpUseEprt = false;
            },
            pending: new ScriptedPendingConnection(new IPEndPoint(IPAddress.Loopback, 55822), ConnectResult.Connected(new ScriptedConnection(Encoding.Latin1.GetBytes(File)))));

        CollectionAssert.AreEqual(
            new[] { "> PORT 127,0,0,1,218,14\r\n", "< 200 PORT command successful\r\n", "* Connect data stream actively" },
            run.Events.Transcript.Skip(9).Take(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_ReportsTheSentDataAndCurlsUploadLine()
    {
        // curl -v -T - ftp://127.0.0.1:47931/up.txt, with "upload bytes\r\n" on standard input.
        DataRun run = await RunAsync(
            "/up.txt",
            LoggedIn + Epsv + "200 Type set\r\n" + Opened + Complete + Bye,
            context => context.Upload = new MemoryStream(Encoding.Latin1.GetBytes("upload bytes\r\n")),
            data: new ScriptedConnection());

        string[] expected =
        [
            .. LoggedInTranscript,
            SamePath,
            .. EpsvTranscript,
            "> TYPE I\r\n",
            "< 200 Type set\r\n",
            "> STOR up.txt\r\n",
            "< " + Opened,
            "=> upload bytes\r\n",
            "* upload completely sent off: 14 bytes",
            "* Remembering we are in directory \"\"",
            "< " + Complete,
            "* Connection #0 to host 127.0.0.1:47931 left intact",
        ];
        CollectionAssert.AreEqual(expected, run.Events.Transcript);
        CollectionAssert.AreEqual(new[] { "upload bytes\r\n" }, run.Events.DataSent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ListOnly_ReportsMaxdownloadButNoFileSize()
    {
        // curl -v -l ftp://127.0.0.1:47931/dir/
        DataRun run = await RunAsync(
            "/dir/",
            LoggedIn + "250 OK\r\n" + Epsv + "200 Type set\r\n" + Opened + Complete + Bye,
            context => context.ListOnly = true);

        string[] expected =
        [
            "> NLST\r\n",
            "< " + Opened,
            "* Maxdownload = -1",
            "<= " + File,
            "<= ",
            "* Remembering we are in directory \"dir/\"",
            "< " + Complete,
            "* Connection #0 to host 127.0.0.1:47931 left intact",
        ];
        CollectionAssert.AreEqual(expected, run.Events.Transcript.Skip(16).ToArray());
        CollectionAssert.AreEqual(new[] { File, string.Empty }, run.Events.DataReceived);
    }

    [TestMethod]
    [DataRow(FtpFileMethod.NoCwd, 0, "", DisplayName = "nocwd")]
    [DataRow(FtpFileMethod.SingleCwd, 1, "a/b/", DisplayName = "singlecwd")]
    [DataRow(FtpFileMethod.MultiCwd, 2, "a/b/", DisplayName = "multicwd")]
    public async Task ExecuteAsync_FtpMethod_RemembersThePathsDirectories(FtpFileMethod method, int changes, string remembered)
    {
        // curl -v --ftp-method <method> ftp://127.0.0.1:47931/a/b/file.txt
        DataRun run = await RunAsync(
            "/a/b/file.txt",
            LoggedIn + string.Concat(Enumerable.Repeat("250 OK\r\n", changes)) + Epsv + Retrieved,
            context => context.FtpFileMethod = method);

        Assert.AreEqual("Remembering we are in directory \"" + remembered + "\"", run.Events.Info[^2]);
        Assert.AreEqual(TransferResult.Success(11), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_DisableEpsv_ReportsThePassiveLineAfterPasvAndTheSkippedAddress()
    {
        // curl -v --disable-epsv ftp://127.0.0.1:47931/file.txt
        DataRun run = await RunAsync("/file.txt", LoggedIn + Pasv + Retrieved, context => context.FtpDisableEpsv = true);

        string[] expected =
        [
            "> PASV\r\n",
            "* Connect data stream passively",
            "< " + Pasv,
            "* Skip 127.0.0.1 for data connection, reuse 127.0.0.1 instead",
            "* Connecting to 127.0.0.1 port 64816",
            "> TYPE I\r\n",
        ];
        CollectionAssert.AreEqual(expected, run.Events.Transcript.Skip(9).Take(6).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_EpsvRefused_ReportsCurlsFallbackLineAndNoSecondPassiveLine()
    {
        // curl -v ftp://127.0.0.1:47931/file.txt, EPSV answered 500 no
        DataRun run = await RunAsync("/file.txt", LoggedIn + "500 no\r\n" + Pasv + Retrieved, _ => { });

        string[] expected =
        [
            "> EPSV\r\n",
            "* Connect data stream passively",
            "< 500 no\r\n",
            "* Failed EPSV attempt. Disabling EPSV",
            "> PASV\r\n",
            "< " + Pasv,
            "* Skip 127.0.0.1 for data connection, reuse 127.0.0.1 instead",
            "* Connecting to 127.0.0.1 port 64816",
        ];
        CollectionAssert.AreEqual(expected, run.Events.Transcript.Skip(9).Take(8).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_NoFtpSkipPasvIp_ConnectsToThe227AddressWithNoSkipLine()
    {
        // curl -v --disable-epsv --no-ftp-skip-pasv-ip ftp://127.0.0.1:47931/file.txt
        DataRun run = await RunAsync(
            "/file.txt",
            LoggedIn + Pasv + Retrieved,
            context =>
            {
                context.FtpDisableEpsv = true;
                context.FtpSkipPasvIp = false;
            });

        CollectionAssert.AreEqual(
            new[] { "< " + Pasv, "* Connecting to 127.0.0.1 port 64816", "> TYPE I\r\n" },
            run.Events.Transcript.Skip(11).Take(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_LocalhostUrl_ReusesTheUrlHostButConnectsToTheControlPeer()
    {
        // curl -v --disable-epsv ftp://localhost:47934/file.txt: the control connection went to 127.0.0.1.
        DataRun run = await RunAsync("/file.txt", LoggedIn + Pasv + Retrieved, context => context.FtpDisableEpsv = true, host: "localhost", controlPeer: new IPEndPoint(IPAddress.Loopback, ControlPort));

        Assert.AreEqual("Skip 127.0.0.1 for data connection, reuse localhost instead", run.Events.Info[3]);
        Assert.AreEqual("Connecting to 127.0.0.1 port 64816", run.Events.Info[4]);
        Assert.AreEqual("Connection #0 to host localhost:47931 left intact", run.Events.Info[^1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_ControlPeerUnknown_NamesTheUrlHostAsWhereTheDataIsDialled()
    {
        DataRun run = await RunAsync("/file.txt", LoggedIn + Epsv + Retrieved, _ => { }, host: "localhost");

        Assert.AreEqual("Connecting to localhost port 55801", run.Events.Info[3]);
    }

    [TestMethod]
    public async Task ExecuteAsync_IPv4MappedControlPeer_NamesItAsPlainIPv4()
    {
        DataRun run = await RunAsync(
            "/file.txt",
            LoggedIn + Epsv + Retrieved,
            _ => { },
            controlPeer: new IPEndPoint(IPAddress.Loopback.MapToIPv6(), ControlPort));

        Assert.AreEqual("Connecting to 127.0.0.1 port 55801", run.Events.Info[3]);
    }

    [TestMethod]
    public async Task ExecuteAsync_Range_ReportsTheRangeThenClosesTheConnection()
    {
        // curl -v -r 0-4 ftp://127.0.0.1:47931/file.txt: ABOR, then the connection is shut down.
        DataRun run = await RunAsync(
            "/file.txt",
            LoggedIn + Epsv + "200 Type set\r\n213 11\r\n" + Opened + Complete + Bye,
            context => context.Range = ByteRange.Bounded(0, 4),
            connectionNumber: 3);

        string[] expected =
        [
            "* Maxdownload = 5",
            "* Getting file with size: 5",
            "<= hello",
            "* Remembering we are in directory \"\"",
            "> ABOR\r\n",
            "< " + Complete,
            "* partial download completed, closing connection",
            "* shutting down connection #3",
        ];
        CollectionAssert.AreEqual(expected, run.Events.Transcript.Skip(19).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeFrom_ReportsTheOffsetBeforeRestAndTheBytesLeft()
    {
        // curl -v -C 3 ftp://127.0.0.1:47931/file.txt
        DataRun run = await RunAsync(
            "/file.txt",
            LoggedIn + Epsv + "200 Type set\r\n213 11\r\n350 Restarting at 3\r\n" + Opened + Complete + Bye,
            context => context.ResumeFrom = 3,
            data: new ScriptedConnection(Encoding.Latin1.GetBytes("lo ftp\r\n")));

        string[] expected =
        [
            "< 213 11\r\n",
            "* Instructs server to resume from offset 3",
            "> REST 3\r\n",
            "< 350 Restarting at 3\r\n",
            "> RETR file.txt\r\n",
            "< " + Opened,
            "* Maxdownload = -1",
            "* Getting file with size: 8",
        ];
        CollectionAssert.AreEqual(expected, run.Events.Transcript.Skip(16).Take(8).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_SizeRefused_ReportsAnUnknownFileSize()
    {
        // curl -v ftp://127.0.0.1:47931/file.txt, SIZE answered 500 no
        DataRun run = await RunAsync("/file.txt", LoggedIn + Epsv + "200 Type set\r\n500 no\r\n" + Opened + Complete + Bye, _ => { });

        Assert.AreEqual("Getting file with size: -1", run.Events.Info[5]);
    }

    [TestMethod]
    public async Task ExecuteAsync_PostTransferQuoteRefused_ReportsNoLeftIntactLine()
    {
        DataRun run = await RunAsync(
            "/file.txt",
            LoggedIn + Epsv + "200 Type set\r\n213 11\r\n" + Opened + Complete + "500 no\r\n" + Bye,
            context => context.QuoteCommands.Add("-NOOP"));

        Assert.AreEqual("< 500 no\r\n", run.Events.Transcript[^1]);
        Assert.AreEqual(CurlExitCode.QuoteError, run.Result.ExitCode);
    }

    private static async Task<DataRun> RunAsync(
        string path,
        string replies,
        Action<MutableContext> adjust,
        ScriptedConnection? data = null,
        ScriptedPendingConnection? pending = null,
        string host = Host,
        IPEndPoint? controlPeer = null,
        long connectionNumber = 0)
    {
        var events = new RecordingTransferEvents();
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(replies))
        {
            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 55800),
            RemoteEndPoint = controlPeer ?? (host == Host ? new IPEndPoint(IPAddress.Loopback, ControlPort) : null),
        };
        var connector = new QueuedConnector(
            ConnectResult.Connected(control, timings: null, connectionNumber: connectionNumber),
            ConnectResult.Connected(data ?? new ScriptedConnection(Encoding.Latin1.GetBytes(File))));
        var listener = pending is null ? new QueuedListener() : new QueuedListener(ListenResult.Listening(pending));
        var context = MutableContext.Build(
            new TransferContext { Url = CurlUrl.Parse($"ftp://{host}:{ControlPort}{path}"), Output = new MemoryStream() },
            mutable =>
            {
                mutable.Events = events;
                adjust(mutable);
            });

        TransferResult result = await new FtpProtocolHandler(connector, listener, new QueuedTlsProvider()).ExecuteAsync(context);
        return new DataRun(result with { Report = null }, events);
    }

    /// <summary>One transfer and the events it reported.</summary>
    private sealed record DataRun(TransferResult Result, RecordingTransferEvents Events);
}
