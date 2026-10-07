using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Telnet.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Telnet;

/// <summary>
/// Pins what a <c>telnet://</c> session reports to <see cref="ITransferEvents" /> after
/// connecting, against curl 8.21.0's <c>--trace-ascii -</c>, measured on 2026-09-29 against a
/// loopback listener (captures in BL-935's Notes): each negotiation and subnegotiation
/// received and sent, each run of output data, and the line the connection ends with.
/// </summary>
[TestClass]
public sealed class TelnetProtocolHandlerTransferEventsTests
{
    /// <summary>curl's own offers, once the server has negotiated.</summary>
    private static readonly string[] Offers =
    [
        "* SENT WILL BINARY",
        "* SENT DO BINARY",
        "* SENT WILL SUPPRESS GO AHEAD",
        "* SENT DO SUPPRESS GO AHEAD",
    ];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_ServerAsksTerminalTypeWithoutOption_ReportsNegotiationThenDataThenShuttingDown()
    {
        // Measured: the server sent IAC DO TERM-TYPE, IAC WILL ECHO, then "hello\r\n", and closed.
        Session session = await RunAsync([], null, Hex("FF FD 18 FF FB 01"), Latin1("hello\r\n"));

        string[] expected = new[] { "* RCVD DO TERM TYPE", "* SENT WONT TERM TYPE", "* RCVD WILL ECHO", "* SENT DO ECHO" }
            .Concat(Offers)
            .Concat(["<= hello\r\n", "* shutting down connection #0"])
            .ToArray();
        Diagnostics.AssertExitCode(CurlExitCode.Ok, session.Result.ExitCode);
        Diagnostics.AssertLines("transcript", expected, session.Transcript);
        Assert.AreEqual(CurlExitCode.Ok, session.Result.ExitCode);
        CollectionAssert.AreEqual(expected, session.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_TerminalTypeOptionAndServerAsksForIt_ReportsBothSubnegotiationsInPieces()
    {
        // Measured with -t TTYPE=vt100: the server then sent IAC SB TTYPE SEND IAC SE, and "hi\r\n".
        Session session = await RunAsync(
            ["TTYPE=vt100"],
            null,
            Hex("FF FD 18 FF FB 01"),
            Hex("FF FA 18 01 FF F0"),
            Latin1("hi\r\n"));

        string[] expected = new[] { "* RCVD DO TERM TYPE", "* SENT WILL TERM TYPE", "* RCVD WILL ECHO", "* SENT DO ECHO" }
            .Concat(Offers)
            .Concat(
            [
                "* RCVD IAC SB ", "* TERM TYPE", "*  SEND", "*  \"\"",
                "* SENT IAC SB ", "* TERM TYPE", "*  IS", "*  \"vt100\"",
                "<= hi\r\n",
                "* shutting down connection #0",
            ])
            .ToArray();
        Diagnostics.AssertLines("transcript", expected, session.Transcript);
        CollectionAssert.AreEqual(expected, session.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_DataBetweenCommands_ReportsEachRunAsItsOwnBlock()
    {
        // Measured: "ab" IAC WILL ECHO "cd\r\0e" IAC IAC "z\r\n" in one read gave four blocks.
        Session session = await RunAsync([], null, Hex("61 62 FF FB 01 63 64 0D 00 65 FF FF 7A 0D 0A"));

        string[] expected = new[] { "<= ab", "* RCVD WILL ECHO", "* SENT DO ECHO", "<= cd\r", "<= e", "<= ÿz\r\n" }
            .Concat(Offers)
            .Append("* shutting down connection #0")
            .ToArray();
        Diagnostics.AssertLines("transcript", expected, session.Transcript);
        CollectionAssert.AreEqual(expected, session.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_WindowSizeEnvironmentAndDisplayAskedFor_ReportsEverySubnegotiation()
    {
        // Measured with -t WS=300x24 -t NEW_ENV=A -t XDISPLOC=d:0.
        Session session = await RunAsync(
            ["WS=300x24", "NEW_ENV=A", "XDISPLOC=d:0"],
            null,
            Hex("FF FD 1F FF FA 27 01 FF F0 FF FA 23 01 FF F0 FF FD C8 FF F1 FF FA 05 01 02 FF F0"),
            Hex("FF FA FF F0 78"));

        string[] expected = new[]
            {
                "* RCVD DO NAWS", "* SENT WILL NAWS", "* SENT IAC SB ", "* NAWS", "* Width: 300 ; Height: 24",
                "* RCVD IAC SB ", "* NEW-ENVIRON", "*  SEND",
                "* SENT IAC SB ", "* NEW-ENVIRON", "*  IS", "*  ", "* A",
                "* RCVD IAC SB ", "* XDISPLOC", "*  SEND", "*  \"\"",
                "* SENT IAC SB ", "* XDISPLOC", "*  IS", "*  \"d:0\"",
                "* RCVD DO 200", "* SENT WONT 200",
                "* RCVD IAC NOP",
                "* RCVD IAC SB ", "* STATUS (unsupported)", "*  SEND", "*  02",
            }
            .Concat(Offers)
            .Concat(["* SENT WILL XDISPLOC", "* SENT WILL NEW-ENVIRON", "<= x", "* shutting down connection #0"])
            .ToArray();
        Diagnostics.AssertLines("transcript", expected, session.Transcript);
        CollectionAssert.AreEqual(expected, session.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_UserAndEnvironmentVariables_ReportsTheEnvironmentByteByByte()
    {
        // Measured with -u bob:x -t NEW_ENV=AB,CD -t NEW_ENV=E; the TTYPE IS at the end,
        // with no -t TTYPE, ended the session with exit 43.
        Session session = await RunAsync(
            ["NEW_ENV=AB,CD", "NEW_ENV=E"],
            new NetworkCredential("bob", "x"),
            Hex("FF FA 27 01 FF F0 FF FA C8 01 FF F0 FF FA FF F0 FF FA 1F 00 05 FF F0 FF FA 18 00 61 62 FF F0"));

        string[] expected =
        [
            "* RCVD IAC SB ", "* NEW-ENVIRON", "*  SEND",
            "* SENT IAC SB ", "* NEW-ENVIRON", "*  IS", "*  ",
            "* U", "* S", "* E", "* R", "*  = ", "* b", "* o", "* b",
            "* , ", "* A", "* B", "*  = ", "* C", "* D", "* , ", "* E",
            "* RCVD IAC SB ", "* 200 (unknown)", "*  SEND",
            "* RCVD IAC SB ", "* NAWS",
            "* RCVD IAC SB ", "* TERM TYPE", "*  IS", "*  \"ab\"",
            "* shutting down connection #0",
        ];
        Diagnostics.AssertExitCode(CurlExitCode.BadFunctionArgument, session.Result.ExitCode);
        Diagnostics.AssertLines("transcript", expected, session.Transcript);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, session.Result.ExitCode);
        CollectionAssert.AreEqual(expected, session.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_OddCommandsAndSubnegotiations_ReportsThemAsCurlDoes()
    {
        // Measured with -t TTYPE=vt.
        Session session = await RunAsync(
            ["TTYPE=vt"],
            null,
            Hex("61 FF 07 62 FF F1 FF FB FF FF FB 28 FF FA 18 FF F0 FF FA 27 00 FF F0 FF FA 05 07 41 FF F0 FF FA 1F 00 05 00 03 FF F0"));

        string[] expected = new[]
            {
                "<= a", "* RCVD IAC 7", "<= b", "* RCVD IAC NOP",
                "* RCVD WILL EXOPL", "* SENT DONT EXOPL", "* RCVD WILL 40", "* SENT DONT 40",
                "* RCVD IAC SB ", "* (Empty suboption?)",
                "* SENT IAC SB ", "* TERM TYPE", "*  IS", "*  \"vt\"",
                "* RCVD IAC SB ", "* NEW-ENVIRON", "*  IS", "*  ",
                "* SENT IAC SB ", "* NEW-ENVIRON", "*  IS", "*  ",
                "* RCVD IAC SB ", "* STATUS (unsupported)", "*  41",
                "* RCVD IAC SB ", "* NAWS", "* Width: 5 ; Height: 3",
            }
            .Concat(Offers)
            .Concat(["* SENT WILL TERM TYPE", "* shutting down connection #0"])
            .ToArray();
        Diagnostics.AssertLines("transcript", expected, session.Transcript);
        CollectionAssert.AreEqual(expected, session.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerRefusesAndOffersToStop_ReportsTheDontAndWontExchange()
    {
        Session session = await RunAsync([], null, Hex("FF FB 01"), Hex("FF FE 00 FF FC 01"));

        string[] expected = new[] { "* RCVD WILL ECHO", "* SENT DO ECHO" }
            .Concat(Offers)
            .Concat(["* RCVD DONT BINARY", "* RCVD WONT ECHO", "* SENT DONT ECHO", "* shutting down connection #0"])
            .ToArray();
        Diagnostics.AssertLines("transcript", expected, session.Transcript);
        CollectionAssert.AreEqual(expected, session.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_DisplayLocationWithNul_QuotesUpToTheFirstNul()
    {
        Session session = await RunAsync(["XDISPLOC=d"], null, Hex("FF FA 23 00 61 00 62 FF F0"));

        string[] expected =
        [
            "* RCVD IAC SB ", "* XDISPLOC", "*  IS", "*  \"a\"",
            "* SENT IAC SB ", "* XDISPLOC", "*  IS", "*  \"d\"",
            "* shutting down connection #0",
        ];
        Diagnostics.AssertLines("transcript", expected, session.Transcript);
        CollectionAssert.AreEqual(expected, session.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_MalformedSubnegotiation_ReportsTheMessageThenShuttingDown()
    {
        // Measured: IAC SB TTYPE SEND IAC 0x01 with -t TTYPE=x, exit 56.
        Session session = await RunAsync(["TTYPE=x"], null, Hex("FF FA 18 01 FF 01"));

        Diagnostics.AssertExitCode(CurlExitCode.RecvError, session.Result.ExitCode);
        Diagnostics.AssertLines("transcript", ["* telnet: suboption error", "* shutting down connection #0"], session.Transcript);
        Assert.AreEqual(CurlExitCode.RecvError, session.Result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "* telnet: suboption error", "* shutting down connection #0" },
            session.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_OutputRefusesTheWrite_ReportsTheBlockTheMessageThenClosing()
    {
        // Measured: an output that could not be opened traced the block, curl's message and
        // "* closing connection #0", and exited 23.
        ScriptedConnection connection = new(new ScriptedRead(Latin1("hello\r\n")));
        TranscriptTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("telnet://h/"),
            Output = new FaultingOutputStream(),
            Upload = new MemoryStream(),
            Events = events,
        };
        Diagnostics.ArrangeContext(context);
        Diagnostics.ArrangeReads([Latin1("hello\r\n")]);
        Diagnostics.Arrange("output", "refuses every write");

        TransferResult result = await new TelnetProtocolHandler(Connector(connection, 2)).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Diagnostics.ActLines("transcript", events.Transcript);
        Diagnostics.AssertExitCode(CurlExitCode.WriteError, result.ExitCode);
        Diagnostics.AssertLines("transcript", ["<= hello\r\n", "* " + result.ErrorMessage, "* closing connection #2"], events.Transcript);
        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "<= hello\r\n", "* " + result.ErrorMessage, "* closing connection #2" },
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnknownOption_ReportsTheMessageThenShuttingDown()
    {
        // Measured: -t FOO=1 printed "* Unknown telnet option FOO=1", exit 48.
        Session session = await RunAsync(["FOO=1"], null);

        Diagnostics.AssertExitCode(CurlExitCode.UnknownOption, session.Result.ExitCode);
        Diagnostics.AssertLines("transcript", ["* Unknown telnet option FOO=1", "* shutting down connection #0"], session.Transcript);
        Assert.AreEqual(CurlExitCode.UnknownOption, session.Result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "* Unknown telnet option FOO=1", "* shutting down connection #0" },
            session.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnmatchedOptionOfAKnownLength_ReportsOnlyShuttingDown()
    {
        Session session = await RunAsync(["TTYPX=1"], null);

        Diagnostics.AssertExitCode(CurlExitCode.UnknownOption, session.Result.ExitCode);
        Diagnostics.AssertLines("transcript", ["* shutting down connection #0"], session.Transcript);
        Assert.AreEqual(CurlExitCode.UnknownOption, session.Result.ExitCode);
        CollectionAssert.AreEqual(new[] { "* shutting down connection #0" }, session.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_NonAsciiUserName_ReportsOnlyShuttingDown()
    {
        // Measured: -u with a non-ASCII user name printed no message, exit 43.
        Session session = await RunAsync([], new NetworkCredential("béb", "x"));

        Diagnostics.AssertExitCode(CurlExitCode.BadFunctionArgument, session.Result.ExitCode);
        Diagnostics.AssertLines("transcript", ["* shutting down connection #0"], session.Transcript);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, session.Result.ExitCode);
        CollectionAssert.AreEqual(new[] { "* shutting down connection #0" }, session.Transcript);
    }

    private static RecordingConnector Connector(IConnection connection, long connectionNumber) =>
        new(ConnectResult.Connected(connection, null, connectionNumber: connectionNumber));

    private async Task<Session> RunAsync(string[] telnetOptions, NetworkCredential? credentials, params byte[][] reads)
    {
        ScriptedConnection connection = new([.. reads.Select(read => new ScriptedRead(read))]);
        TranscriptTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("telnet://h/"),
            Output = new MemoryStream(),
            Upload = new MemoryStream(),
            TelnetOptions = telnetOptions,
            Credentials = credentials,
            Events = events,
        };
        Diagnostics.ArrangeContext(context);
        Diagnostics.Arrange("user name (-u)", credentials?.UserName ?? "(none)");
        Diagnostics.ArrangeReads(reads);

        TransferResult result = await new TelnetProtocolHandler(Connector(connection, 0)).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Diagnostics.ActSent(connection.Sent);
        Diagnostics.ActLines("transcript", events.Transcript);
        return new Session(result, events.Transcript);
    }

    private static byte[] Hex(string hex) => Convert.FromHexString(hex.Replace(" ", string.Empty, StringComparison.Ordinal));

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

    private sealed record Session(TransferResult Result, List<string> Transcript);
}
