using Curl.Protocol.Abstractions;
using Curl.Protocol.Telnet.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Telnet;

/// <summary>
/// Adversarial black-box attacks on <see cref="TelnetProtocolHandler" /> through its public
/// surface and a scripted server (BL-1519, by the method in
/// <c>Documentation/Wiki/Adversarial-Testing.md</c>): command bytes at the edges of a read
/// and of the stream, commands cut short, replies sent in states that do not expect them,
/// and the same session delivered in every split and run many times at once. The oracle
/// is curl 8.21.0's <c>lib/telnet.c</c> receive state machine and the handler's documented
/// contract: data written byte for byte as the whole-read case writes it, never a throw
/// and never a hang.
/// </summary>
[TestClass]
public sealed class TelnetProtocolHandlerAdversarialTests
{
    /// <summary>
    /// curl's own offers, sent once after the first read in which the server negotiates:
    /// <c>IAC WILL BINARY</c>, <c>IAC DO BINARY</c>, <c>IAC WILL SGA</c>, <c>IAC DO SGA</c>.
    /// </summary>
    private const string Offers = "FF FB 00 FF FD 00 FF FB 03 FF FD 03";

    /// <summary>
    /// A server stream that never negotiates but holds every command shape that does not
    /// call for a reply: <c>IAC IAC</c>, CR NUL, <c>IAC NOP</c>, a subnegotiation with a
    /// doubled IAC inside, an IAC straight after a CR, and an unknown command byte.
    /// </summary>
    private const string MixedStream = "61 FF FF 62 0D 00 63 FF F1 64 FF FA 05 01 FF FF 02 FF F0 65 0D FF FB 01 66 FF 00 67";

    /// <summary>What curl writes for <see cref="MixedStream" />.</summary>
    private const string MixedStreamOutput = "61 FF 62 0D 63 64 65 0D FF FB 01 66 67";

    private static readonly CurlUrl TelnetUrl = CurlUrl.Parse("telnet://example.test/");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    // Boundaries: a command byte as the last byte of a read or of the stream.

    [TestMethod]
    public async Task ExecuteAsync_LoneIacAsTheLastByteBeforeClose_WritesTheDataBeforeItAndExitsOk()
    {
        Exchange exchange = await RunAsync([], Read("61 FF"));

        AssertExchange(exchange, CurlExitCode.Ok, "61", string.Empty);
    }

    [TestMethod]
    public async Task ExecuteAsync_IacIacSplitAcrossTwoReads_WritesOneFFBetweenTheData()
    {
        Exchange exchange = await RunAsync([], Read("61 FF"), Read("FF 62"));

        AssertExchange(exchange, CurlExitCode.Ok, "61 FF 62", string.Empty);
    }

    [TestMethod]
    public async Task ExecuteAsync_CarriageReturnEndsOneReadAndNulStartsTheNext_DropsTheNul()
    {
        Exchange exchange = await RunAsync([], Read("61 0D"), Read("00 62"));

        AssertExchange(exchange, CurlExitCode.Ok, "61 0D 62", string.Empty);
    }

    [TestMethod]
    [DataRow("FF FB", DisplayName = "IAC WILL with no option")]
    [DataRow("FF FD", DisplayName = "IAC DO with no option")]
    [DataRow("FF FA", DisplayName = "IAC SB with no option")]
    [DataRow("FF FA 18 01 FF", DisplayName = "TTYPE SEND cut before SE")]
    public async Task ExecuteAsync_CommandCutShortByClose_SendsNothingAndExitsOk(string received)
    {
        Exchange exchange = await RunAsync(["TTYPE=vt100"], Read("61 " + received));

        AssertExchange(exchange, CurlExitCode.Ok, "61", string.Empty);
    }

    [TestMethod]
    [DataRow("FF FD FF", "FF FC FF", DisplayName = "DO 255 is refused with WONT 255")]
    [DataRow("FF FB FF", "FF FE FF", DisplayName = "WILL 255 is refused with DONT 255")]
    [DataRow("FF FD C8", "FF FC C8", DisplayName = "DO 200 is refused with WONT 200")]
    [DataRow("FF FB C8", "FF FE C8", DisplayName = "WILL 200 is refused with DONT 200")]
    public async Task ExecuteAsync_NegotiationForAnOptionNumberCurlDoesNotKnow_IsRefusedThenOffersFollow(
        string received,
        string refusal)
    {
        Exchange exchange = await RunAsync([], Read(received));

        AssertExchange(exchange, CurlExitCode.Ok, string.Empty, refusal + " " + Offers);
    }

    [TestMethod]
    public async Task ExecuteAsync_TerminalTypeSubnegotiationLongerThanCurlsBuffer_IsAnsweredOnce()
    {
        string padding = string.Join(' ', Enumerable.Repeat("41", 600));

        Exchange exchange = await RunAsync(["TTYPE=vt"], Read("FF FA 18 01 " + padding + " FF F0 62"));

        AssertExchange(exchange, CurlExitCode.Ok, "62", "FF FA 18 00 76 74 FF F0");
    }

    [TestMethod]
    public async Task ExecuteAsync_SubnegotiationOfAMillionBytes_KeepsOnlyCurlsBufferAndWritesTheDataAfterIt()
    {
        // BL-1669: curl's CURL_SB_ACCUM drops every byte past its 512-byte subbuffer, and
        // the IAC SE it adds and takes back off leave 510 kept; the rest is read and dropped.
        byte[] stream = [0xFF, 0xFA, 0x05, .. Enumerable.Repeat((byte)0x41, 1_000_000), 0xFF, 0xF0, 0x62];
        ScriptedRead[] reads = [.. stream.Chunk(4096).Select(chunk => new ScriptedRead(chunk))];
        var connection = new ScriptedConnection(reads);
        var output = new MemoryStream();
        TranscriptTransferEvents events = new();
        var context = new TransferContext { Url = TelnetUrl, Output = output, Upload = new MemoryStream(), Events = events };
        Diagnostics.ArrangeContext(context);
        Diagnostics.Arrange("reads", $"IAC SB STATUS, 1,000,000 x 41, IAC SE, 62 in {reads.Length} reads of at most 4096 bytes");

        TransferResult result = await new TelnetProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(context);

        int parameterLines = events.Transcript.Count(line => line == "*  41");
        Diagnostics.ActResult(result);
        Diagnostics.ActOutput(output.ToArray());
        Diagnostics.Act("subnegotiation parameter lines traced", parameterLines.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("62", ToHex(output.ToArray()));
        Assert.AreEqual(string.Empty, ToHex(connection.Sent));
        Assert.AreEqual(1, events.Transcript.Count(line => line == "* RCVD IAC SB "));
        Assert.AreEqual(508, parameterLines, "the option and 509 bytes after it are kept, 508 of them traced as parameters");
    }

    // Malformed input: commands that are almost right.

    [TestMethod]
    public async Task ExecuteAsync_SubnegotiationWithoutSeBeforeClose_SwallowsTheRestAndExitsOk()
    {
        Exchange exchange = await RunAsync([], Read("61 FF FA 05 01 62 63"), Read("64 65"));

        AssertExchange(exchange, CurlExitCode.Ok, "61", string.Empty);
    }

    [TestMethod]
    [DataRow("FF F0", DisplayName = "SE outside a subnegotiation")]
    [DataRow("FF 00", DisplayName = "command byte 0")]
    [DataRow("FF 80", DisplayName = "command byte 128")]
    [DataRow("FF EF", DisplayName = "command byte just below SE")]
    [DataRow("FF F4", DisplayName = "IAC IP")]
    public async Task ExecuteAsync_CommandThatNeedsNoReply_IsRemovedFromTheData(string command)
    {
        Exchange exchange = await RunAsync([], Read("61 " + command + " 62"));

        AssertExchange(exchange, CurlExitCode.Ok, "61 62", string.Empty);
    }

    [TestMethod]
    [DataRow("FF FA 05 FF FA 05 FF F0", DisplayName = "SB inside a subnegotiation")]
    [DataRow("FF FA 05 FF 00 FF F0", DisplayName = "IAC NUL inside a subnegotiation")]
    [DataRow("FF FA 18 01 FF FD 01 FF F0", DisplayName = "DO inside a TTYPE subnegotiation")]
    public async Task ExecuteAsync_SubnegotiationBrokenByAnotherCommand_ExitsWith56AndAnswersNothing(string received)
    {
        Exchange exchange = await RunAsync(["TTYPE=vt"], Read("61 " + received + " 62"));

        Diagnostics.Diff("error", "telnet: suboption error", exchange.Result.ErrorMessage ?? string.Empty);
        AssertExchange(exchange, CurlExitCode.RecvError, "61", string.Empty);
        Assert.AreEqual("telnet: suboption error", exchange.Result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("FF FA 1F FF F0", DisplayName = "NAWS")]
    [DataRow("FF FA 00 01 FF F0", DisplayName = "BINARY")]
    [DataRow("FF FA FF FF 01 FF F0", DisplayName = "option 255 written as IAC IAC")]
    public async Task ExecuteAsync_SubnegotiationForAnOptionCurlNeverAnswers_IsIgnored(string received)
    {
        Exchange exchange = await RunAsync(["TTYPE=vt"], Read("61 " + received + " 62"));

        AssertExchange(exchange, CurlExitCode.Ok, "61 62", string.Empty);
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty option")]
    [DataRow("=", DisplayName = "only an equals sign")]
    [DataRow("==", DisplayName = "two equals signs")]
    public async Task ExecuteAsync_OptionWithAnEmptyName_ExitsBeforeSendingAByte(string option)
    {
        Exchange exchange = await RunAsync([option], Read("68 69"));

        Diagnostics.Arrange("option", option);
        Assert.AreNotEqual(CurlExitCode.Ok, exchange.Result.ExitCode);
        Assert.AreEqual(string.Empty, exchange.Sent);
        Assert.AreEqual(string.Empty, exchange.Output);
    }

    [TestMethod]
    public async Task ExecuteAsync_TerminalTypeValueHoldingEquals_SendsEverythingAfterTheFirstEquals()
    {
        Exchange exchange = await RunAsync(["TTYPE==a="], Read("FF FA 18 01 FF F0"));

        AssertExchange(exchange, CurlExitCode.Ok, string.Empty, "FF FA 18 00 3D 61 3D FF F0");
    }

    // Invalid partitions: replies in states that do not expect them.

    [TestMethod]
    public async Task ExecuteAsync_DoForAnUnknownOptionRepeated_IsRefusedEachTime()
    {
        Exchange exchange = await RunAsync([], Read("FF FD C8 FF FD C8 FF FD C8"));

        AssertExchange(exchange, CurlExitCode.Ok, string.Empty, "FF FC C8 FF FC C8 FF FC C8 " + Offers);
    }

    [TestMethod]
    [DataRow("FF FE C8", DisplayName = "DONT an option never enabled")]
    [DataRow("FF FC C8", DisplayName = "WONT an option never enabled")]
    [DataRow("FF FE 18", DisplayName = "DONT TTYPE never offered")]
    public async Task ExecuteAsync_DisableForAnOptionNeverEnabled_IsNotAnswered(string received)
    {
        Exchange exchange = await RunAsync([], Read(received));

        AssertExchange(exchange, CurlExitCode.Ok, string.Empty, Offers);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerRefusesEveryOffer_SendsNothingMore()
    {
        Exchange exchange = await RunAsync(
            [],
            Read("FF FB 01"),
            Read("FF FE 00 FF FC 00 FF FE 03 FF FC 03 61"));

        AssertExchange(exchange, CurlExitCode.Ok, "61", "FF FD 01 " + Offers);
    }

    [TestMethod]
    public async Task ExecuteAsync_TerminalTypeSubnegotiationBeforeAnyNegotiation_IsAnsweredWithoutOffers()
    {
        Exchange exchange = await RunAsync(["TTYPE=vt"], Read("FF FA 18 01 FF F0"));

        AssertExchange(exchange, CurlExitCode.Ok, string.Empty, "FF FA 18 00 76 74 FF F0");
    }

    // State: the same stream in every split, negotiation loops, many sessions at once.

    [TestMethod]
    public async Task ExecuteAsync_MixedStreamOneBytePerRead_WritesWhatTheWholeReadWrites()
    {
        ScriptedRead[] reads = [.. Hex(MixedStream).Select(value => new ScriptedRead([value]))];

        Exchange exchange = await RunAsync([], reads);

        AssertExchange(exchange, CurlExitCode.Ok, MixedStreamOutput, string.Empty);
    }

    [TestMethod]
    public async Task ExecuteAsync_MixedStreamSplitAtEveryOffset_WritesWhatTheWholeReadWrites()
    {
        byte[] stream = Hex(MixedStream);
        // An empty read is the server closing, so each piece holds at least one byte.
        for (int split = 1; split < stream.Length; split++)
        {
            Diagnostics.Arrange("split", split);
            Exchange exchange = await RunAsync([], new ScriptedRead(stream[..split]), new ScriptedRead(stream[split..]));

            Assert.AreEqual(MixedStreamOutput, exchange.Output, $"split at {split}");
            Assert.AreEqual(string.Empty, exchange.Sent, $"split at {split}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_MixedStreamInRandomPieces_WritesWhatTheWholeReadWrites()
    {
        const int Seed = 1519;
        var random = new Random(Seed);
        Diagnostics.Arrange("seed", Seed);
        byte[] stream = Hex(MixedStream);
        for (int round = 0; round < 50; round++)
        {
            var reads = new List<ScriptedRead>();
            int offset = 0;
            while (offset < stream.Length)
            {
                int length = random.Next(1, Math.Min(5, stream.Length - offset) + 1);
                reads.Add(new ScriptedRead(stream[offset..(offset + length)]));
                offset += length;
            }

            Exchange exchange = await RunAsync([], [.. reads]);

            Assert.AreEqual(MixedStreamOutput, exchange.Output, $"seed {Seed}, round {round}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_NegotiationSplitOneBytePerRead_IsAnsweredOnceThenOffers()
    {
        Exchange exchange = await RunAsync([], Read("FF"), Read("FB"), Read("01"), Read("61"));

        AssertExchange(exchange, CurlExitCode.Ok, "61", "FF FD 01 " + Offers);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerTogglesEchoAHundredTimes_AnswersEachToggleOnceAndOffersOnce()
    {
        string toggles = string.Join(' ', Enumerable.Repeat("FF FB 01 FF FC 01", 100));

        Exchange exchange = await RunAsync([], Read(toggles));

        string answers = string.Join(' ', Enumerable.Repeat("FF FD 01 FF FE 01", 100));
        AssertExchange(exchange, CurlExitCode.Ok, string.Empty, answers + " " + Offers);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerRepeatsDoBinaryAfterAgreeing_IsNotAnsweredAgain()
    {
        Exchange exchange = await RunAsync([], Read("FF FD 00"), Read("FF FD 00 FF FD 00 FF FB 00 FF FB 00"));

        AssertExchange(exchange, CurlExitCode.Ok, string.Empty, "FF FB 00 FF FD 00 FF FB 03 FF FD 03");
    }

    [TestMethod]
    public async Task ExecuteAsync_OneHandlerRunsManySessionsAtOnce_EachMatchesASessionRunAlone()
    {
        var connector = new ConnectorForEachSession();
        var handler = new TelnetProtocolHandler(connector);
        Diagnostics.Arrange("sessions", 32);

        (TransferResult Result, string Output)[] sessions = await Task.WhenAll(
            Enumerable.Range(0, 32).Select(_ => Task.Run(() => RunOnAsync(handler))));

        Assert.HasCount(32, connector.Connections);
        foreach ((TransferResult result, string output) in sessions)
        {
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
            Assert.AreEqual(MixedStreamOutput + " 68", output);
        }

        foreach (ScriptedConnection connection in connector.Connections)
        {
            Assert.AreEqual("FF FD 01 " + Offers, ToHex(connection.Sent));
            Assert.IsTrue(connection.IsDisposed);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_SameHandlerRunTwiceInARow_StartsTheSecondSessionAfresh()
    {
        var connector = new ConnectorForEachSession();
        var handler = new TelnetProtocolHandler(connector);

        (TransferResult Result, string Output) first = await RunOnAsync(handler);
        (TransferResult Result, string Output) second = await RunOnAsync(handler);

        Assert.AreEqual(first.Output, second.Output);
        Assert.AreEqual(first.Result, second.Result);
        Assert.HasCount(2, connector.Connections);
        Assert.IsTrue(connector.Connections.All(connection => ToHex(connection.Sent) == "FF FD 01 " + Offers));
    }

    private static ScriptedRead Read(string hex) => new(Hex(hex));

    private static byte[] Hex(string hex) => Convert.FromHexString(hex.Replace(" ", string.Empty, StringComparison.Ordinal));

    private static string ToHex(byte[] bytes) =>
        string.Join(' ', Convert.ToHexString(bytes).Chunk(2).Select(pair => new string(pair)));

    private void AssertExchange(Exchange exchange, CurlExitCode exitCode, string output, string sent)
    {
        Diagnostics.Diff("output", output, exchange.Output);
        Diagnostics.Diff("sent", sent, exchange.Sent);
        Diagnostics.AssertExitCode(exitCode, exchange.Result.ExitCode);
        Assert.AreEqual(output, exchange.Output);
        Assert.AreEqual(sent, exchange.Sent);
        Assert.AreEqual(exitCode, exchange.Result.ExitCode);
    }

    private static async Task<(TransferResult Result, string Output)> RunOnAsync(TelnetProtocolHandler handler)
    {
        var output = new MemoryStream();
        var context = new TransferContext { Url = TelnetUrl, Output = output, Upload = new MemoryStream() };
        TransferResult result = await handler.ExecuteAsync(context);
        return (result, ToHex(output.ToArray()));
    }

    private async Task<Exchange> RunAsync(string[] telnetOptions, params ScriptedRead[] reads)
    {
        var connection = new ScriptedConnection(reads);
        var output = new MemoryStream();
        var context = new TransferContext
        {
            Url = TelnetUrl,
            Output = output,
            Upload = new MemoryStream(),
            TelnetOptions = telnetOptions,
        };
        Diagnostics.ArrangeContext(context);
        Diagnostics.ArrangeReads(reads);

        TransferResult result;
        using (Diagnostics.Phase("session"))
        {
            result = await new TelnetProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
                .ExecuteAsync(context);
        }

        Diagnostics.ActResult(result);
        Diagnostics.ActSent(connection.Sent);
        Diagnostics.ActOutput(output.ToArray());
        Assert.IsTrue(connection.IsDisposed);
        return new Exchange(result, ToHex(connection.Sent), ToHex(output.ToArray()));
    }

    /// <summary>
    /// Hands every session its own <see cref="ScriptedConnection" /> playing
    /// <see cref="MixedStream" />, a <c>WILL ECHO</c> and one more byte, and keeps every
    /// connection it handed out.
    /// </summary>
    private sealed class ConnectorForEachSession : IConnector
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<ScriptedConnection> connections = new();

        public ScriptedConnection[] Connections => [.. connections];

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            var connection = new ScriptedConnection(new ScriptedRead(Hex(MixedStream)), Read("FF FB 01 68"));
            connections.Enqueue(connection);
            return ValueTask.FromResult(ConnectResult.Connected(connection));
        }
    }

    private sealed record Exchange(TransferResult Result, string Sent, string Output);
}
