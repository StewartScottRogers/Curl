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

    private const string Pasv = "227 Entering Passive Mode (127,0,0,1,210,230)\r\n";

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

    private static readonly string[] ActiveAccept =
    [
        "* Ready to accept data connection from server",
        "* Connection accepted from server",
        "* Established 2nd connection to 127.0.0.1 (127.0.0.1 port 60356) from 127.0.0.1 port 60355 ",
        "* [FTP] ftp_initiate_transfer()",
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
    public async Task ExecuteAsync_TracedCwd_EntersTheCwdStateOnceAndAwaitsTheDataAfterTheFirst()
    {
        // curl -sS --trace-config ftp -v ftp://127.0.0.1:P/d/e/a.txt (BL-1197)
        TraceRecordingEvents events = await RunAsync(
            "/d/e/a.txt",
            LoggedIn + "250 OK\r\n250 OK\r\n" + Epsv + "200 Type set\r\n213 6\r\n" + Opened + Complete + Bye,
            _ => { });

        string[] expected =
        [
            "* [FTP] [STOP] DO phase starts",
            "> CWD d",
            "* [FTP] [STOP] -> [CWD]",
            "* [FTP] [CWD] perform, awaiting DATA connect",
            "< 250 OK",
            "> CWD e",
            "< 250 OK",
            "> EPSV",
            "* [FTP] [CWD] -> [PASV]",
            "* Connect data stream passively",
            "< 229 Entering Extended Passive Mode (|||53990|)",
            "* Connecting to 127.0.0.1 port 53990",
            "* [FTP] [PASV] -> [STOP]",
            "* [FTP] [STOP] DO phase is complete2",
        ];
        CollectionAssert.AreEqual(expected, Slice(events, "* [FTP] [STOP] DO phase starts", expected.Length));
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
        // curl -sS --trace-config ftp -v ftp://127.0.0.1:P/a.txt, GREETING=500 go away: exit 8 (BL-1197)
        TraceRecordingEvents events = await RunAsync("/a.txt", "500 Go away\r\n", _ => { });

        string[] expected =
        [
            "* [FTP] [STOP] setup connection -> 0",
            "* [FTP] [STOP] -> [WAIT220]",
            "< 500 Go away",
            "* [FTP] [WAIT220] done, result=8",
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

    [TestMethod]
    public async Task ExecuteAsync_TracedActiveDownloadWithEprt_WritesThePortStates()
    {
        // curl -sS --trace-config ftp -v -P 127.0.0.1 ftp://127.0.0.1:47196/a.txt
        TraceRecordingEvents events = await RunActiveAsync(
            "/a.txt",
            LoggedIn + "200 EPRT command successful\r\n200 Type set\r\n213 6\r\n" + Opened + Complete + Bye,
            _ => { });

        string[] expected =
        [
            .. ActiveDoPhase("EPRT |1|127.0.0.1|60355|", "200 EPRT command successful"),
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
            "* Data conn was not available immediately",
            "* [FTP] [RETR] -> [STOP]",
            "* [FTP] [STOP] ftp_domore_pollset()",
            .. ActiveAccept,
            .. TransferEnd,
        ];
        CollectionAssert.AreEqual(expected, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedActiveDownloadWithPort_WritesThePortStates()
    {
        // curl -sS --trace-config ftp -v --disable-eprt -P 127.0.0.1 ftp://127.0.0.1:47196/a.txt
        TraceRecordingEvents events = await RunActiveAsync(
            "/a.txt",
            LoggedIn + "200 PORT command successful\r\n200 Type set\r\n213 6\r\n" + Opened + Complete + Bye,
            context => context.FtpUseEprt = false);

        string[] expected = ActiveDoPhase("PORT 127,0,0,1,235,195", "200 PORT command successful");
        CollectionAssert.AreEqual(expected, events.Transcript.Take(expected.Length).ToArray());
        int notAvailable = events.Transcript.IndexOf("* Data conn was not available immediately");
        string[] accept = ["* [FTP] [RETR] -> [STOP]", "* [FTP] [STOP] ftp_domore_pollset()", .. ActiveAccept];
        CollectionAssert.AreEqual(
            accept,
            events.Transcript.Skip(notAvailable + 1).Take(2 + ActiveAccept.Length).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedActiveUpload_LeavesTheStorStateBeforeTheAccept()
    {
        // curl -sS --trace-config ftp -v -P 127.0.0.1 -T up.txt ftp://127.0.0.1:47197/u.txt
        TraceRecordingEvents events = await RunActiveAsync(
            "/u.txt",
            LoggedIn + "200 EPRT command successful\r\n200 Type set\r\n" + Opened + Complete + Bye,
            context => context.Upload = new MemoryStream("hi"u8.ToArray()));

        string[] expected =
        [
            .. ActiveDoPhase("EPRT |1|127.0.0.1|60355|", "200 EPRT command successful"),
            "> TYPE I",
            "* [FTP] [STOP] -> [STOR_TYPE]",
            "* [FTP] [STOR_TYPE] ftp_domore_pollset()",
            "< 200 Type set",
            "> STOR u.txt",
            "* [FTP] [STOR_TYPE] -> [STOR]",
            "* [FTP] [STOR] ftp_domore_pollset()",
            "< 150 Opening BINARY mode data connection",
            "* [FTP] [STOR] -> [STOP]",
            "* Data conn was not available immediately",
            "* [FTP] [STOP] ftp_domore_pollset()",
            .. ActiveAccept,
            "* upload completely sent off: 2 bytes",
            .. TransferEnd,
        ];
        CollectionAssert.AreEqual(expected, events.Transcript);
    }

    /// <summary>The connect phase and an active-mode DO phase, as curl 8.21.0 wrote them (BL-1196).</summary>
    private static string[] ActiveDoPhase(string announce, string accepted) =>
    [
        .. ConnectPhase.Take(17),
        "* [FTP] [STOP] ftp_state_use_port(), opened socket",
        "* [FTP] ftp_port_bind_socket(), socket bound to port 0",
        "* [FTP] ftp_port_listen(), listening on port",
        "> " + announce,
        "* [FTP] [STOP] -> [PORT]",
        "* [FTP] [PORT] perform, awaiting DATA connect",
        "< " + accepted,
        "* Connect data stream actively",
        "* [FTP] [PORT] -> [STOP]",
        "* [FTP] [STOP] DO phase is complete2",
    ];

    [TestMethod]
    public async Task ExecuteAsync_TracedRefusedRetr_ClosesTheDataConnectionAndKeepsResultZero()
    {
        // curl -sS --trace-config ftp -v ftp://127.0.0.1:P/a.txt, RETR=550 No such file: exit 78 (BL-1197)
        TraceRecordingEvents events = await RunAsync("/a.txt", LoggedIn + Epsv + "200 Type set\r\n213 6\r\n550 No such file\r\n" + Bye, _ => { });

        string[] expected =
        [
            "> RETR a.txt",
            "* [FTP] [RETR_SIZE] -> [RETR]",
            "* [FTP] [RETR] ftp_domore_pollset()",
            "< 550 No such file",
            "* [FTP] [RETR] closing DATA connection",
            "* [FTP] [RETR] done, result=0",
        ];
        CollectionAssert.AreEqual(expected, FtpLinesFrom(events, "> RETR a.txt"));
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedRemoteTime_EntersTheMdtmStateFirstInTheDoPhase()
    {
        // curl -sS --trace-config ftp -v -R ftp://127.0.0.1:P/a.txt (BL-1197)
        TraceRecordingEvents events = await RunAsync(
            "/a.txt",
            LoggedIn + "213 20260927123456\r\n" + Epsv + "200 Type set\r\n213 6\r\n" + Opened + Complete + Bye,
            context => context.RemoteTime = true);

        string[] expected =
        [
            "* [FTP] [STOP] DO phase starts",
            "> MDTM a.txt",
            "* [FTP] [STOP] -> [MDTM]",
            "* [FTP] [MDTM] perform, awaiting DATA connect",
            "< 213 20260927123456",
            "> EPSV",
            "* [FTP] [MDTM] -> [PASV]",
            "< 229 Entering Extended Passive Mode (|||53990|)",
            "* [FTP] [PASV] -> [STOP]",
        ];
        CollectionAssert.AreEqual(expected, FtpLinesFrom(events, "* [FTP] [STOP] DO phase starts").Take(expected.Length).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedResumedDownload_EntersTheRetrRestStateAfterFtpStateRetr()
    {
        // curl -sS --trace-config ftp -v -C 2 ftp://127.0.0.1:P/a.txt (BL-1197)
        TraceRecordingEvents events = await RunAsync(
            "/a.txt",
            LoggedIn + Epsv + "200 Type set\r\n213 6\r\n350 Restarting at 2\r\n" + Opened + Complete + Bye,
            context => context.ResumeFrom = 2);

        string[] expected =
        [
            "< 213 6",
            "* [FTP] [RETR_SIZE] ftp_state_retr()",
            "* Instructs server to resume from offset 2",
            "> REST 2",
            "* [FTP] [RETR_SIZE] -> [RETR_REST]",
            "* [FTP] [RETR_REST] ftp_domore_pollset()",
            "< 350 Restarting at 2",
            "> RETR a.txt",
            "* [FTP] [RETR_REST] -> [RETR]",
            "* [FTP] [RETR] ftp_domore_pollset()",
        ];
        CollectionAssert.AreEqual(expected, Slice(events, "< 213 6", expected.Length));
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedDisableEpsv_EntersThePasvStateOnPasv()
    {
        // curl -sS --trace-config ftp -v --disable-epsv ftp://127.0.0.1:P/a.txt (BL-1197)
        TraceRecordingEvents events = await RunAsync(
            "/a.txt",
            LoggedIn + Pasv + "200 Type set\r\n213 6\r\n" + Opened + Complete + Bye,
            context => context.FtpDisableEpsv = true);

        string[] expected =
        [
            "* [FTP] [STOP] DO phase starts",
            "> PASV",
            "* [FTP] [STOP] -> [PASV]",
            "* Connect data stream passively",
            "* [FTP] [PASV] perform, awaiting DATA connect",
            "< 227 Entering Passive Mode (127,0,0,1,210,230)",
        ];
        CollectionAssert.AreEqual(expected, Slice(events, "* [FTP] [STOP] DO phase starts", expected.Length));
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedRefusedEpsv_ClosesTheDataConnectionAndStaysInPasvForPasv()
    {
        // curl -sS --trace-config ftp -v ftp://127.0.0.1:P/a.txt, EPSV=500 no (BL-1197)
        TraceRecordingEvents events = await RunAsync(
            "/a.txt",
            LoggedIn + "500 no\r\n" + Pasv + "200 Type set\r\n213 6\r\n" + Opened + Complete + Bye,
            _ => { });

        string[] expected =
        [
            "> EPSV",
            "* [FTP] [STOP] -> [PASV]",
            "* Connect data stream passively",
            "* [FTP] [PASV] perform, awaiting DATA connect",
            "< 500 no",
            "* Failed EPSV attempt. Disabling EPSV",
            "* [FTP] [PASV] closing DATA connection",
            "> PASV",
            "< 227 Entering Passive Mode (127,0,0,1,210,230)",
        ];
        CollectionAssert.AreEqual(expected, Slice(events, "> EPSV", expected.Length));
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedSsl_WritesTheAuthPbszAndProtStates()
    {
        // curl -sS --trace-config ftp -v --ssl -k ftp://127.0.0.1:P/a.txt (BL-1197)
        var securedControl = new ScriptedConnection(Encoding.Latin1.GetBytes(
            "331 Password required\r\n230 Logged in\r\n200 PBSZ=0\r\n200 Protection level set to P\r\n257 \"/\" is current directory\r\n"
            + Epsv + "200 Type set\r\n213 6\r\n" + Opened + Complete + Bye))
        {
            RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, ControlPort),
        };
        var tls = new QueuedTlsProvider(ConnectResult.Connected(securedControl), ConnectResult.Connected(new ScriptedConnection("hello\n"u8.ToArray())));

        TraceRecordingEvents events = await RunAsync(
            "/a.txt",
            "220 Recorder ready\r\n234 AUTH accepted\r\n",
            context => context.SslLevel = TransportSecurityLevel.Try,
            tls: tls);

        string[] expected =
        [
            "* [FTP] [STOP] -> [WAIT220]",
            "< 220 Recorder ready",
            "> AUTH SSL",
            "* [FTP] [WAIT220] -> [AUTH]",
            "< 234 AUTH accepted",
            "> USER anonymous",
            "* [FTP] [AUTH] -> [USER]",
            "< 331 Password required",
            "> PASS ftp@example.com",
            "* [FTP] [USER] -> [PASS]",
            "< 230 Logged in",
            "> PBSZ 0",
            "* [FTP] [PASS] -> [PBSZ]",
            "< 200 PBSZ=0",
            "> PROT P",
            "* [FTP] [PBSZ] -> [PROT]",
            "< 200 Protection level set to P",
            "> PWD",
            "* [FTP] [PROT] -> [PWD]",
            "< 257 \"/\" is current directory",
            "* [FTP] [PWD] -> [STOP]",
            "* [FTP] [STOP] protocol connect phase DONE",
        ];
        CollectionAssert.AreEqual(expected, FtpLinesFrom(events, "* [FTP] [STOP] -> [WAIT220]").Take(expected.Length).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedQuotes_WritesTheQuoteAndPrequoteStatesAndThePostQuoteReply()
    {
        // curl -sS --trace-config ftp -v -Q NOOP -Q '-SITE x' -Q +HELP ftp://127.0.0.1:P/a.txt (BL-1197)
        TraceRecordingEvents events = await RunAsync(
            "/a.txt",
            LoggedIn + "200 ok\r\n" + Epsv + "200 Type set\r\n214 ok\r\n213 6\r\n" + Opened + Complete + "200 ok\r\n" + Bye,
            context => context.QuoteCommands.AddRange(["NOOP", "-SITE x", "+HELP"]));

        string[] expected =
        [
            "* [FTP] [STOP] DO phase starts",
            "> NOOP",
            "* [FTP] [STOP] -> [QUOTE]",
            "* [FTP] [QUOTE] perform, awaiting DATA connect",
            "< 200 ok",
            "> EPSV",
            "* [FTP] [QUOTE] -> [PASV]",
            "< 229 Entering Extended Passive Mode (|||53990|)",
            "* [FTP] [PASV] -> [STOP]",
            "* [FTP] [STOP] DO phase is complete2",
            "* [FTP] [STOP] ftp_domore_pollset()",
            "> TYPE I",
            "* [FTP] [STOP] -> [RETR_TYPE]",
            "* [FTP] [RETR_TYPE] ftp_domore_pollset()",
            "< 200 Type set",
            "> HELP",
            "* [FTP] [RETR_TYPE] -> [RETR_PREQUOTE]",
            "* [FTP] [RETR_PREQUOTE] ftp_domore_pollset()",
            "< 214 ok",
            "> SIZE a.txt",
            "* [FTP] [RETR_PREQUOTE] -> [RETR_SIZE]",
            "* [FTP] [RETR_SIZE] ftp_domore_pollset()",
            "< 213 6",
            "* [FTP] [RETR_SIZE] ftp_state_retr()",
            "> RETR a.txt",
            "* [FTP] [RETR_SIZE] -> [RETR]",
            "* [FTP] [RETR] ftp_domore_pollset()",
            "< 150 Opening BINARY mode data connection",
            "* [FTP] ftp_initiate_transfer()",
            "* [FTP] [RETR] -> [STOP]",
            "* [FTP] [STOP] closing DATA connection",
            "* [FTP] getftpresponse start",
            "< 226 Transfer complete",
            "* [FTP] getftpresponse -> result=0, nread=23, ftpcode=226",
            "> SITE x",
            "* [FTP] getftpresponse start",
            "< 200 ok",
            "* [FTP] getftpresponse -> result=0, nread=8, ftpcode=200",
            "* [FTP] [STOP] done, result=0",
        ];
        CollectionAssert.AreEqual(expected, FtpLinesFrom(events, "* [FTP] [STOP] DO phase starts"));
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedRelativeEntryPath_EntersTheSystState()
    {
        // curl -sS --trace-config ftp -v ftp://127.0.0.1:P/a.txt, PWD=257 "x" is cwd (BL-1197)
        TraceRecordingEvents events = await RunAsync(
            "/a.txt",
            "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"x\" is cwd\r\n502 Command not implemented\r\n" + Epsv + "200 Type set\r\n213 6\r\n" + Opened + Complete + Bye,
            _ => { });

        string[] expected =
        [
            "> PWD",
            "* [FTP] [PASS] -> [PWD]",
            "< 257 \"x\" is cwd",
            "> SYST",
            "* [FTP] [PWD] -> [SYST]",
            "< 502 Command not implemented",
            "* [FTP] [SYST] -> [STOP]",
            "* [FTP] [STOP] protocol connect phase DONE",
        ];
        CollectionAssert.AreEqual(expected, FtpLinesFrom(events, "> PWD").Take(expected.Length).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedOs400_EntersTheNamefmtStateAndThePwdStateAgain()
    {
        // curl -sS --trace-config ftp -v ftp://127.0.0.1:P/a.txt, PWD=257 "QSYS.LIB" then "/QSYS.LIB",
        // SYST=215 OS/400 V7R4, SITE=250 OK (BL-1199)
        TraceRecordingEvents events = await RunAsync(
            "/a.txt",
            "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"QSYS.LIB\"\r\n215 OS/400 V7R4\r\n250 OK\r\n257 \"/QSYS.LIB\"\r\n"
                + Epsv + "200 Type set\r\n213 6\r\n" + Opened + Complete + Bye,
            _ => { });

        string[] expected =
        [
            "> SYST",
            "* [FTP] [PWD] -> [SYST]",
            "< 215 OS/400 V7R4",
            "> SITE NAMEFMT 1",
            "* [FTP] [SYST] -> [NAMEFMT]",
            "< 250 OK",
            "> PWD",
            "* [FTP] [NAMEFMT] -> [PWD]",
            "< 257 \"/QSYS.LIB\"",
            "* [FTP] [PWD] -> [STOP]",
            "* [FTP] [STOP] protocol connect phase DONE",
        ];
        CollectionAssert.AreEqual(expected, FtpLinesFrom(events, "> SYST").Take(expected.Length).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedPret_EntersThePretStateAndAwaitsTheDataAfterIt()
    {
        // curl -sS --trace-config ftp -v --ftp-pret ftp://127.0.0.1:P/a.txt, PRET=200 OK (BL-1199)
        TraceRecordingEvents events = await RunAsync(
            "/a.txt",
            LoggedIn + "200 OK\r\n" + Epsv + "200 Type set\r\n213 6\r\n" + Opened + Complete + Bye,
            context => context.FtpSendPret = true);

        string[] expected =
        [
            "* [FTP] [STOP] DO phase starts",
            "> PRET RETR a.txt",
            "* [FTP] [STOP] -> [PRET]",
            "* [FTP] [PRET] perform, awaiting DATA connect",
            "< 200 OK",
            "> EPSV",
            "* [FTP] [PRET] -> [PASV]",
            "* Connect data stream passively",
            "< 229 Entering Extended Passive Mode (|||53990|)",
        ];
        CollectionAssert.AreEqual(expected, Slice(events, "* [FTP] [STOP] DO phase starts", expected.Length));
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedCreateDirs_EntersTheMkdStateAndTheCwdStateBeforeTheSecondCwd()
    {
        // curl -sS --trace-config ftp -v --ftp-create-dirs ftp://127.0.0.1:P/d/a.txt,
        // CWD=550 no then 250 OK, MKD=257 created (BL-1199)
        TraceRecordingEvents events = await RunAsync(
            "/d/a.txt",
            LoggedIn + "550 no\r\n257 created\r\n250 OK\r\n" + Epsv + "200 Type set\r\n213 6\r\n" + Opened + Complete + Bye,
            context => context.FtpCreateDirectories = true);

        string[] expected =
        [
            "* [FTP] [STOP] DO phase starts",
            "> CWD d",
            "* [FTP] [STOP] -> [CWD]",
            "* [FTP] [CWD] perform, awaiting DATA connect",
            "< 550 no",
            "> MKD d",
            "* [FTP] [CWD] -> [MKD]",
            "< 257 created",
            "* [FTP] [MKD] -> [CWD]",
            "> CWD d",
            "< 250 OK",
            "> EPSV",
            "* [FTP] [CWD] -> [PASV]",
        ];
        CollectionAssert.AreEqual(expected, Slice(events, "* [FTP] [STOP] DO phase starts", expected.Length));
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedAccount_EntersTheAcctState()
    {
        // curl -sS --trace-config ftp -v --ftp-account bob ftp://127.0.0.1:P/a.txt, PASS=332 (BL-1197)
        TraceRecordingEvents events = await RunAsync(
            "/a.txt",
            "220 Recorder ready\r\n331 Password required\r\n332 need acct\r\n230 ok\r\n257 \"/\" is current directory\r\n" + Epsv + "200 Type set\r\n213 6\r\n" + Opened + Complete + Bye,
            context => context.FtpAccount = "bob");

        string[] expected =
        [
            "> PASS ftp@example.com",
            "* [FTP] [USER] -> [PASS]",
            "< 332 need acct",
            "> ACCT bob",
            "* [FTP] [PASS] -> [ACCT]",
            "< 230 ok",
            "> PWD",
            "* [FTP] [ACCT] -> [PWD]",
        ];
        CollectionAssert.AreEqual(expected, FtpLinesFrom(events, "> PASS ftp@example.com").Take(expected.Length).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedRefusedPass_WritesDoneWithTheExitCode()
    {
        // curl -sS --trace-config ftp -v ftp://127.0.0.1:P/a.txt, PASS=530 no: exit 67 (BL-1197)
        TraceRecordingEvents events = await RunAsync("/a.txt", "220 Recorder ready\r\n331 Password required\r\n530 no\r\n", _ => { });

        CollectionAssert.AreEqual(new[] { "< 530 no", "* [FTP] [PASS] done, result=67" }, FtpLinesFrom(events, "< 530 no"));
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedRefusedCwd_WritesTheDoPhaseFailedAndResultZero()
    {
        // curl -sS --trace-config ftp -v ftp://127.0.0.1:P/d/a.txt, CWD=550 no: exit 9 (BL-1197)
        TraceRecordingEvents events = await RunAsync("/d/a.txt", LoggedIn + "550 no\r\n" + Bye, _ => { });

        string[] expected = ["< 550 no", "* [FTP] [CWD] DO phase failed", "* [FTP] [CWD] done, result=0"];
        CollectionAssert.AreEqual(expected, FtpLinesFrom(events, "< 550 no"));
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedRefusedQuote_WritesTheDoPhaseFailedAndExit21()
    {
        // curl -sS --trace-config ftp -v -Q NOOP ftp://127.0.0.1:P/a.txt, NOOP=502 (BL-1197)
        TraceRecordingEvents events = await RunAsync(
            "/a.txt",
            LoggedIn + "502 Command not implemented\r\n",
            context => context.QuoteCommands.Add("NOOP"));

        string[] expected = ["< 502 Command not implemented", "* [FTP] [QUOTE] DO phase failed", "* [FTP] [QUOTE] done, result=21"];
        CollectionAssert.AreEqual(expected, FtpLinesFrom(events, "< 502 Command not implemented"));
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedRange_ReadsAborsReplyAfterClosingTheDataConnection()
    {
        // curl -sS --trace-config ftp -v -r 0-1 ftp://127.0.0.1:P/a.txt (BL-1197)
        TraceRecordingEvents events = await RunAsync(
            "/a.txt",
            LoggedIn + Epsv + "200 Type set\r\n213 6\r\n" + Opened + Complete + Bye,
            context => context.Range = ByteRange.Bounded(0, 1));

        string[] expected =
        [
            "* Remembering we are in directory \"\"",
            "> ABOR",
            "* [FTP] [STOP] closing DATA connection",
            "* [FTP] getftpresponse start",
            "< 226 Transfer complete",
            "* [FTP] getftpresponse -> result=0, nread=23, ftpcode=226",
            "* partial download completed, closing connection",
            "* [FTP] [STOP] done, result=0",
            "* shutting down connection #0",
        ];
        CollectionAssert.AreEqual(expected, Slice(events, "* Remembering we are in directory \"\"", expected.Length));
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedUploadResumedFromTheRemoteSize_EntersTheStorSizeState()
    {
        // curl -sS --trace-config ftp -v -T up.txt -C - ftp://127.0.0.1:P/a.txt (BL-1197)
        TraceRecordingEvents events = await RunAsync(
            "/a.txt",
            LoggedIn + Epsv + "200 Type set\r\n213 6\r\n" + Opened + Complete + Bye,
            context =>
            {
                context.Upload = new MemoryStream("0123456789"u8.ToArray());
                context.ResumeUploadFromUnknownOffset = true;
            });

        string[] expected =
        [
            "> SIZE a.txt",
            "* [FTP] [STOR_TYPE] -> [STOR_SIZE]",
            "* [FTP] [STOR_SIZE] ftp_domore_pollset()",
            "< 213 6",
            "> APPE a.txt",
            "* [FTP] [STOR_SIZE] -> [STOR]",
        ];
        CollectionAssert.AreEqual(expected, Slice(events, "> SIZE a.txt", expected.Length));
    }

    /// <summary>The lines from <paramref name="first" /> on, <paramref name="count" /> of them.</summary>
    private static string[] Slice(TraceRecordingEvents events, string first, int count) =>
        [.. events.Transcript.Skip(events.Transcript.IndexOf(first)).Take(count)];

    /// <summary>The <c>[FTP]</c>, command and reply lines from <paramref name="first" /> on, the other <c>-v</c> lines left out.</summary>
    private static string[] FtpLinesFrom(TraceRecordingEvents events, string first) =>
    [
        .. events.Transcript
            .Skip(events.Transcript.IndexOf(first))
            .Where(line => line.StartsWith("* [FTP]", StringComparison.Ordinal) || line.StartsWith("> ", StringComparison.Ordinal) || line.StartsWith("< ", StringComparison.Ordinal)),
    ];

    private static async Task<TraceRecordingEvents> RunActiveAsync(string path, string replies, Action<MutableContext> adjust)
    {
        var events = new TraceRecordingEvents();
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(replies))
        {
            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 53991),
            RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, ControlPort),
        };
        var data = new ScriptedConnection("hello\n"u8.ToArray()) { RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 60356) };
        var pending = new ScriptedPendingConnection(new IPEndPoint(IPAddress.Loopback, 60355), ConnectResult.Connected(data));
        var context = MutableContext.Build(
            new TransferContext { Url = CurlUrl.Parse($"ftp://127.0.0.1:{ControlPort}{path}"), Output = new MemoryStream() },
            mutable =>
            {
                mutable.Events = events;
                mutable.FtpPort = "127.0.0.1";
                adjust(mutable);
            });

        var handler = new FtpProtocolHandler(new QueuedConnector(ConnectResult.Connected(control)), new QueuedListener(ListenResult.Listening(pending)), new QueuedTlsProvider()) { TracesStateMachine = true };
        await handler.ExecuteAsync(context);
        return events;
    }

    private static async Task<TraceRecordingEvents> RunAsync(string path, string replies, Action<MutableContext> adjust, bool traced = true, QueuedTlsProvider? tls = null)
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

        var handler = new FtpProtocolHandler(connector, new QueuedListener(), tls ?? new QueuedTlsProvider()) { TracesStateMachine = traced };
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
