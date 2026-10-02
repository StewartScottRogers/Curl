using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins curl 8.21.0's <c>--trace-config ftp</c> lines (the Schannel build), measured on
/// 2026-10-02 with <c>Record-CurlExchange.ps1 -Ftp -FtpData 'hello\n'</c> and
/// <c>-sS --trace-config ftp -v</c> (BL-1162, whose Notes hold the recordings): each
/// <c>[FTP]</c> line in order among the command, reply and <c>-v</c> lines of a passive
/// download, a passive upload and a listing. The data connection's <c>Trying</c> line and its
/// opening (curl's <c>Established 2nd connection</c>) are the connector's, scripted here.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerStateTraceTests
{
    private const int ControlPort = 47162;

    private const string LoggedIn = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private const string Epsv = "229 Entering Extended Passive Mode (|||53990|)\r\n";

    private const string Opened = "150 Opening BINARY mode data connection\r\n";

    private const string Complete = "226 Transfer complete\r\n";

    private const string Bye = "221 Bye\r\n";

    private static readonly string[] ConnectPhase =
    [
        "* [FTP] [STOP] setup connection -> 0",
        "* [FTP] [STOP] -> [WAIT220]",
        "< 220 Recorder ready",
        "> USER anonymous",
        "* [FTP] [WAIT220] -> [USER]",
        "< 331 Password required",
        "> PASS ftp@example.com",
        "* [FTP] [USER] -> [PASS]",
        "< 230 Logged in",
        "> PWD",
        "* [FTP] [PASS] -> [PWD]",
        "< 257 \"/\" is current directory",
        "* Entry path is '/'",
        "* [FTP] [PWD] -> [STOP]",
        "* [FTP] [STOP] protocol connect phase DONE",
        "* Request has same path as previous transfer",
        "* [FTP] [STOP] DO phase starts",
        "> EPSV",
        "* [FTP] [STOP] -> [PASV]",
        "* Connect data stream passively",
        "* [FTP] [PASV] perform, awaiting DATA connect",
        "< 229 Entering Extended Passive Mode (|||53990|)",
        "* Connecting to 127.0.0.1 port 53990",
        "* [FTP] [PASV] -> [STOP]",
        "* [FTP] [STOP] DO phase is complete2",
        "*   Trying 127.0.0.1:53990...",
        "* [FTP] [STOP] ftp_domore_pollset()",
        "[data connection opened]",
    ];

    private static readonly string[] TransferEnd =
    [
        "* Remembering we are in directory \"\"",
        "* [FTP] [STOP] closing DATA connection",
        "* [FTP] getftpresponse start",
        "< 226 Transfer complete",
        "* [FTP] getftpresponse -> result=0, nread=23, ftpcode=226",
        "* [FTP] [STOP] done, result=0",
        "* Connection #0 to host 127.0.0.1:47162 left intact",
    ];

    [TestMethod]
    public async Task ExecuteAsync_TracedPassiveDownload_WritesCurlsFtpLinesInOrder()
    {
        // curl -sS --trace-config ftp -v ftp://127.0.0.1:47162/a.txt
        TraceRecordingEvents events = await RunAsync("/a.txt", LoggedIn + Epsv + "200 Type set\r\n213 6\r\n" + Opened + Complete + Bye, _ => { });

        string[] expected =
        [
            .. ConnectPhase,
            "> TYPE I",
            "* [FTP] [STOP] -> [RETR_TYPE]",
            "* [FTP] [RETR_TYPE] ftp_domore_pollset()",
            "< 200 Type set",
            "> SIZE a.txt",
            "* [FTP] [RETR_TYPE] -> [RETR_SIZE]",
            "* [FTP] [RETR_SIZE] ftp_domore_pollset()",
            "< 213 6",
            "* [FTP] [RETR_SIZE] ftp_state_retr()",
            "> RETR a.txt",
            "* [FTP] [RETR_SIZE] -> [RETR]",
            "* [FTP] [RETR] ftp_domore_pollset()",
            "< 150 Opening BINARY mode data connection",
            "* Maxdownload = -1",
            "* Getting file with size: 6",
            "* [FTP] ftp_initiate_transfer()",
            "* [FTP] [RETR] -> [STOP]",
            .. TransferEnd,
        ];
        CollectionAssert.AreEqual(expected, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedPassiveUpload_WritesTheStorStates()
    {
        // curl -sS --trace-config ftp -v -T up.txt ftp://127.0.0.1:47163/b.txt
        TraceRecordingEvents events = await RunAsync(
            "/b.txt",
            LoggedIn + Epsv + "200 Type set\r\n" + Opened + Complete + Bye,
            context => context.Upload = new MemoryStream("hi"u8.ToArray()));

        string[] expected =
        [
            .. ConnectPhase,
            "> TYPE I",
            "* [FTP] [STOP] -> [STOR_TYPE]",
            "* [FTP] [STOR_TYPE] ftp_domore_pollset()",
            "< 200 Type set",
            "> STOR b.txt",
            "* [FTP] [STOR_TYPE] -> [STOR]",
            "* [FTP] [STOR] ftp_domore_pollset()",
            "< 150 Opening BINARY mode data connection",
            "* [FTP] ftp_initiate_transfer()",
            "* [FTP] [STOR] -> [STOP]",
            "* upload completely sent off: 2 bytes",
            .. TransferEnd,
        ];
        CollectionAssert.AreEqual(expected, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedListing_WritesTheListStates()
    {
        // curl -sS --trace-config ftp -v ftp://127.0.0.1:47163/
        TraceRecordingEvents events = await RunAsync("/", LoggedIn + Epsv + "200 Type set\r\n" + Opened + Complete + Bye, _ => { });

        string[] expected =
        [
            .. ConnectPhase,
            "> TYPE A",
            "* [FTP] [STOP] -> [LIST_TYPE]",
            "* [FTP] [LIST_TYPE] ftp_domore_pollset()",
            "< 200 Type set",
            "> LIST",
            "* [FTP] [LIST_TYPE] -> [LIST]",
            "* [FTP] [LIST] ftp_domore_pollset()",
            "< 150 Opening BINARY mode data connection",
            "* Maxdownload = -1",
            "* [FTP] ftp_initiate_transfer()",
            "* [FTP] [LIST] -> [STOP]",
            .. TransferEnd,
        ];
        CollectionAssert.AreEqual(expected, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedResumedUpload_EntersTheStorStateOnAppe()
    {
        TraceRecordingEvents events = await RunAsync(
            "/b.txt",
            LoggedIn + Epsv + "200 Type set\r\n" + Opened + Complete + Bye,
            context =>
            {
                context.Upload = new MemoryStream("hi"u8.ToArray());
                context.ResumeFrom = 1;
            });

        int appe = events.Transcript.IndexOf("> APPE b.txt");
        CollectionAssert.AreEqual(
            new[] { "* [FTP] [STOR_TYPE] -> [STOR]", "* [FTP] [STOR] ftp_domore_pollset()" },
            events.Transcript.Skip(appe + 1).Take(2).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedNameOnlyListing_EntersTheListStateOnNlst()
    {
        TraceRecordingEvents events = await RunAsync(
            "/",
            LoggedIn + Epsv + "200 Type set\r\n" + Opened + Complete + Bye,
            context => context.ListOnly = true);

        int nlst = events.Transcript.IndexOf("> NLST");
        Assert.AreEqual("* [FTP] [LIST_TYPE] -> [LIST]", events.Transcript[nlst + 1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedCwd_WritesNoStateChangeForIt()
    {
        TraceRecordingEvents events = await RunAsync(
            "/dir/a.txt",
            LoggedIn + "250 OK\r\n" + Epsv + "200 Type set\r\n213 6\r\n" + Opened + Complete + Bye,
            _ => { });

        int cwd = events.Transcript.IndexOf("> CWD dir");
        Assert.AreEqual("< 250 OK", events.Transcript[cwd + 1]);
        Assert.AreEqual("* [FTP] [STOP] -> [PASV]", events.Transcript[cwd + 3]);
    }

    [TestMethod]
    public async Task ExecuteAsync_MultiLineTransferReply_ReportsEveryLinesBytesAsNread()
    {
        TraceRecordingEvents events = await RunAsync(
            "/a.txt",
            LoggedIn + Epsv + "200 Type set\r\n213 6\r\n" + Opened + "226-Done\r\n" + Complete + Bye,
            _ => { });

        CollectionAssert.Contains(events.Transcript, "* [FTP] getftpresponse -> result=0, nread=33, ftpcode=226");
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusedGreeting_EndsTheTraceBeforeTheConnectPhaseIsDone()
    {
        TraceRecordingEvents events = await RunAsync("/a.txt", "500 Go away\r\n", _ => { });

        string[] expected =
        [
            "* [FTP] [STOP] setup connection -> 0",
            "* [FTP] [STOP] -> [WAIT220]",
            "< 500 Go away",
        ];
        CollectionAssert.AreEqual(expected, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_NotTraced_WritesNoFtpLine()
    {
        TraceRecordingEvents events = await RunAsync("/a.txt", LoggedIn + Epsv + "200 Type set\r\n213 6\r\n" + Opened + Complete + Bye, _ => { }, traced: false);

        Assert.IsFalse(events.Transcript.Any(line => line.StartsWith("* [FTP]", StringComparison.Ordinal)));
        CollectionAssert.Contains(events.Transcript, "* Getting file with size: 6");
    }

    private static async Task<TraceRecordingEvents> RunAsync(string path, string replies, Action<MutableContext> adjust, bool traced = true)
    {
        var events = new TraceRecordingEvents();
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(replies))
        {
            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 53991),
            RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, ControlPort),
        };
        var connector = new QueuedConnector(
            ConnectResult.Connected(control),
            ConnectResult.Connected(new ScriptedConnection("hello\n"u8.ToArray())))
        {
            DataConnectReports = dataEvents =>
            {
                dataEvents.ReportInfo("  Trying 127.0.0.1:53990...");
                dataEvents.ReportConnectionOpened(new ConnectionOpenedEvent
                {
                    HostName = "127.0.0.1",
                    RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 53990),
                    LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 53992),
                    ConnectionNumber = 1,
                });
            },
        };
        var context = MutableContext.Build(
            new TransferContext { Url = CurlUrl.Parse($"ftp://127.0.0.1:{ControlPort}{path}"), Output = new MemoryStream() },
            mutable =>
            {
                mutable.Events = events;
                adjust(mutable);
            });

        var handler = new FtpProtocolHandler(connector, new QueuedListener(), new QueuedTlsProvider()) { TracesStateMachine = traced };
        await handler.ExecuteAsync(context);
        return events;
    }

    /// <summary>
    /// Records the info lines (<c>* </c>), commands (<c>&gt; </c>) and replies (<c>&lt; </c>)
    /// without their line ends, and each connection opened, in order; data is left out.
    /// </summary>
    private sealed class TraceRecordingEvents : ITransferEvents
    {
        public List<string> Transcript { get; } = [];

        public void ReportInfo(string text) => Transcript.Add("* " + text);

        public void ReportConnectionOpened(ConnectionOpenedEvent opened) => Transcript.Add("[data connection opened]");

        public void ReportConnectionReused(ConnectionReusedEvent reused)
        {
        }

        public void ReportTlsHandshake(TlsHandshakeEvent handshake)
        {
        }

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent)
        {
        }

        public void ReportTlsMessage(TlsMessageEvent message)
        {
        }

        public void ReportTlsTrust(TlsTrustEvent trust)
        {
        }

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => Transcript.Add("> " + Encoding.Latin1.GetString(bytes).TrimEnd('\r', '\n'));

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => Transcript.Add("< " + Encoding.Latin1.GetString(bytes).TrimEnd('\r', '\n'));

        public void ReportDataSent(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportDataReceived(ReadOnlySpan<byte> bytes)
        {
        }
    }
}
