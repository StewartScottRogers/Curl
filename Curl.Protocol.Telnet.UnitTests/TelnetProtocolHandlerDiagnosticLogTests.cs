using System.Text;
using System.Text.RegularExpressions;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Telnet.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Telnet;

/// <summary>
/// Pins what a TELNET session writes to Curl's own diagnostic log, component
/// <c>telnet</c> (ADR-0222, BL-928): the failure that ends it as <c>error</c>, an option
/// refused as <c>warning</c>, the session's start and end as <c>info</c>, and each option
/// negotiation received and sent as <c>verbose</c>.
/// </summary>
[TestClass]
public sealed class TelnetProtocolHandlerDiagnosticLogTests
{
    private static readonly CurlUrl TelnetUrl = CurlUrl.Parse("telnet://example.test/");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_SessionAtInfo_LogsTheSessionStartAndEndWithBytesAndMilliseconds()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        TransferResult result = await RunAsync(log, [], new ScriptedRead(Encoding.ASCII.GetBytes("hello")));

        string[] info = log.At(DiagnosticLogLevel.Info);
        Diagnostics.AssertResult(TransferResult.Success(5), result);
        Diagnostics.Assert("log lines", 2, log.Lines.Count);
        Diagnostics.Diff("info line 0", "session started to example.test:23", info[0]);
        Diagnostics.Assert("info line 1 pattern", "^session ended: 5 bytes in [0-9]+ ms$", info[1]);
        Assert.AreEqual(TransferResult.Success(5), result);
        Assert.HasCount(2, log.Lines);
        Assert.AreEqual("session started to example.test:23", info[0]);
        StringAssert.Matches(info[1], new Regex("^session ended: 5 bytes in [0-9]+ ms$"));
        Assert.IsTrue(log.Lines.All(line => line.Component == DiagnosticLogComponents.Telnet));
    }

    [TestMethod]
    public async Task ExecuteAsync_UrlWithPort_LogsThatPortInTheSessionStart()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        await RunAsync(log, [], CurlUrl.Parse("telnet://[::1]:2323/"));

        Diagnostics.Diff("info line 0", "session started to ::1:2323", log.At(DiagnosticLogLevel.Info)[0]);
        Assert.AreEqual("session started to ::1:2323", log.At(DiagnosticLogLevel.Info)[0]);
    }

    [TestMethod]
    public async Task ExecuteAsync_TerminalTypeAskedForWithoutOne_LogsAnErrorNamingExit43()
    {
        // The server asks for a terminal type that no -t option supplied.
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);

        TransferResult result = await RunAsync(log, [], Read("FF FA 18 01 FF F0"));

        Diagnostics.AssertExitCode(CurlExitCode.BadFunctionArgument, result.ExitCode);
        Diagnostics.AssertLines(
            "error lines",
            ["failed with BadFunctionArgument (43): A libcurl function was given a bad argument"],
            log.At(DiagnosticLogLevel.Error));
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, result.ExitCode);
        Assert.AreEqual(
            "failed with BadFunctionArgument (43): A libcurl function was given a bad argument",
            log.At(DiagnosticLogLevel.Error).Single());
        Assert.IsFalse(log.At(DiagnosticLogLevel.Info).Any(line => line.StartsWith("session ended", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_LogsAnErrorNamingExit7AndNoSessionStart()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);
        var connector = new RecordingConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect to example.test port 23"));
        Diagnostics.Arrange("connect", "fails with CouldntConnect (7)");
        Diagnostics.Arrange("log level", DiagnosticLogLevel.Verbose);

        TransferResult result = await new TelnetProtocolHandler(connector).ExecuteAsync(Context(log, [], TelnetUrl));

        Diagnostics.ActResult(result);
        Diagnostics.ActLines("log", log.Lines.Select(line => $"{line.Level} {line.Message}"));
        Diagnostics.AssertExitCode(CurlExitCode.CouldntConnect, result.ExitCode);
        Diagnostics.Diff("log line", "failed with CouldntConnect (7): Failed to connect to example.test port 23", log.Lines.Single().Message);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("failed with CouldntConnect (7): Failed to connect to example.test port 23", log.Lines.Single().Message);
        Assert.AreEqual(DiagnosticLogLevel.Error, log.Lines.Single().Level);
    }

    [TestMethod]
    public async Task ExecuteAsync_BadTelnetOption_LogsAnErrorNamingItsExitCode()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);

        TransferResult result = await RunAsync(log, ["BOGUS=1"]);

        string expected = "failed with " + result.ExitCode + " (" + (int)result.ExitCode + "): " + result.ErrorMessage;
        Diagnostics.Diff("log line", expected, log.Lines.Single().Message);
        Diagnostics.Assert("success", false, result.IsSuccess);
        Assert.AreEqual(
            "failed with " + result.ExitCode + " (" + (int)result.ExitCode + "): " + result.ErrorMessage,
            log.Lines.Single().Message);
        Assert.IsFalse(result.IsSuccess);
    }

    [TestMethod]
    public async Task ExecuteAsync_DoNawsAtVerbose_LogsTheNegotiationReceivedAndEachSent()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);

        await RunAsync(log, ["TTYPE=vt100"], Read("FF FD 1F"));

        string[] expected =
        [
            "recv DO NAWS",
            "sent WILL NAWS",
            "sent WILL BINARY",
            "sent DO BINARY",
            "sent WILL SGA",
            "sent DO SGA",
            "sent WILL TTYPE",
        ];
        Diagnostics.AssertLines("verbose lines", expected, log.At(DiagnosticLogLevel.Verbose));
        CollectionAssert.AreEqual(
            new[]
            {
                "recv DO NAWS",
                "sent WILL NAWS",
                "sent WILL BINARY",
                "sent DO BINARY",
                "sent WILL SGA",
                "sent DO SGA",
                "sent WILL TTYPE",
            },
            log.At(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public async Task ExecuteAsync_EveryCommandAndNamedOption_LogsEachByName()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);

        await RunAsync(
            log,
            ["XDISPLOC=host:0", "NEW_ENV=USER,bob"],
            Read("FF FB 01 FF FD 23 FF FD 27 FF FC 01 FF FE 23"));

        string[] verbose = log.At(DiagnosticLogLevel.Verbose);
        string[] expectedSubset =
        [
            "recv WILL ECHO",
            "sent DO ECHO",
            "recv DO XDISPLOC",
            "sent WILL XDISPLOC",
            "recv DO NEW-ENVIRON",
            "sent WILL NEW-ENVIRON",
            "recv WONT ECHO",
            "sent DONT ECHO",
            "recv DONT XDISPLOC",
            "sent WONT XDISPLOC",
        ];
        Diagnostics.Assert("verbose lines missing", "(none)", string.Join(" | ", expectedSubset.Except(verbose).DefaultIfEmpty("(none)")));
        CollectionAssert.IsSubsetOf(
            new[]
            {
                "recv WILL ECHO",
                "sent DO ECHO",
                "recv DO XDISPLOC",
                "sent WILL XDISPLOC",
                "recv DO NEW-ENVIRON",
                "sent WILL NEW-ENVIRON",
                "recv WONT ECHO",
                "sent DONT ECHO",
                "recv DONT XDISPLOC",
                "sent WONT XDISPLOC",
            },
            verbose);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnknownOptionAskedForAndOffered_LogsAWarningForEachRefusal()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);

        await RunAsync(log, [], Read("FF FD 63 FF FB 63"));

        string[] expectedVerbose = ["recv DO 99", "sent WONT 99", "recv WILL 99", "sent DONT 99"];
        Diagnostics.AssertLines(
            "warning lines",
            ["refused option 99 with WONT", "refused option 99 with DONT"],
            log.At(DiagnosticLogLevel.Warning));
        Diagnostics.Assert("verbose lines missing", "(none)", string.Join(" | ", expectedVerbose.Except(log.At(DiagnosticLogLevel.Verbose)).DefaultIfEmpty("(none)")));
        CollectionAssert.AreEqual(
            new[] { "refused option 99 with WONT", "refused option 99 with DONT" },
            log.At(DiagnosticLogLevel.Warning));
        CollectionAssert.IsSubsetOf(
            new[] { "recv DO 99", "sent WONT 99", "recv WILL 99", "sent DONT 99" },
            log.At(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectTarget_CarriesTheIncomingDiagnosticLog()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection()));
        Diagnostics.Arrange("log level", DiagnosticLogLevel.Verbose);

        await new TelnetProtocolHandler(connector).ExecuteAsync(Context(log, [], TelnetUrl));

        Diagnostics.Act("connect targets", string.Join(" | ", connector.Targets));
        Diagnostics.Assert("connect target log is the context's", true, ReferenceEquals(log, connector.Targets.Single().DiagnosticLog));
        Assert.AreSame(log, connector.Targets.Single().DiagnosticLog);
    }

    [TestMethod]
    public async Task ExecuteAsync_AtError_RecordsNoInfoWarningOrVerboseLine()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);

        TransferResult result = await RunAsync(log, [], Read("FF FD 1F FF FD 63"), new ScriptedRead(Encoding.ASCII.GetBytes("hi")));

        Diagnostics.Assert("success", true, result.IsSuccess);
        Diagnostics.Assert("log lines", 0, log.Lines.Count);
        Assert.IsTrue(result.IsSuccess);
        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_AtNone_RecordsNothingEvenForAFailure()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.None);

        TransferResult result = await RunAsync(log, [], Read("FF FA 18 01 FF F0"));

        Diagnostics.Assert("success", false, result.IsSuccess);
        Diagnostics.Assert("log lines", 0, log.Lines.Count);
        Assert.IsFalse(result.IsSuccess);
        Assert.IsEmpty(log.Lines);
    }

    private static ScriptedRead Read(string hex) => new(Convert.FromHexString(hex.Replace(" ", string.Empty, StringComparison.Ordinal)));

    private static TransferContext Context(IDiagnosticLog log, string[] telnetOptions, CurlUrl url) =>
        new()
        {
            Url = url,
            Output = new MemoryStream(),
            Upload = new MemoryStream(),
            TelnetOptions = telnetOptions,
            DiagnosticLog = log,
        };

    private Task<TransferResult> RunAsync(IDiagnosticLog log, string[] telnetOptions, params ScriptedRead[] reads) =>
        RunAsync(log, telnetOptions, TelnetUrl, reads);

    private async Task<TransferResult> RunAsync(
        IDiagnosticLog log,
        string[] telnetOptions,
        CurlUrl url,
        params ScriptedRead[] reads)
    {
        var connection = new ScriptedConnection(reads);
        var connector = new RecordingConnector(ConnectResult.Connected(connection));
        TransferContext context = Context(log, telnetOptions, url);
        Diagnostics.ArrangeContext(context);
        Diagnostics.ArrangeReads(reads);

        TransferResult result = await new TelnetProtocolHandler(connector).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Diagnostics.ActSent(connection.Sent);
        if (log is RecordingDiagnosticLog recording)
        {
            Diagnostics.ActLines("log", recording.Lines.Select(line => $"{line.Level} {line.Message}"));
        }

        return result;
    }
}
