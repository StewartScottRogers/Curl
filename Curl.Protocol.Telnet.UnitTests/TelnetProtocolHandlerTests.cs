using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Telnet.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Telnet;

/// <summary>
/// Pins the <c>telnet://</c> session against curl 8.21.0, measured on 2026-09-26 against
/// a loopback listener: the bytes sent to the server, the bytes written to the output and
/// the exit code, for each captured exchange.
/// </summary>
[TestClass]
public sealed class TelnetProtocolHandlerTests
{
    /// <summary>
    /// curl's own offers, sent once after the first read in which the server negotiates:
    /// <c>IAC WILL BINARY</c>, <c>IAC DO BINARY</c>, <c>IAC WILL SGA</c>, <c>IAC DO SGA</c>.
    /// </summary>
    private const string Offers = "FF FB 00 FF FD 00 FF FB 03 FF FD 03";

    private static readonly CurlUrl TelnetUrl = CurlUrl.Parse("telnet://example.test/");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void SupportedSchemes_IsTelnetOnly()
    {
        Diagnostics.Arrange("handler", "TelnetProtocolHandler over a scripted connection");
        var handler = new TelnetProtocolHandler(new RecordingConnector(ConnectResult.Connected(new ScriptedConnection())));

        Diagnostics.Act("supported schemes", string.Join(", ", handler.SupportedSchemes));
        Diagnostics.Assert("supported schemes", "telnet", string.Join(", ", handler.SupportedSchemes));
        CollectionAssert.AreEqual(new[] { "telnet" }, handler.SupportedSchemes.ToArray());
    }

    [TestMethod]
    public void Constructor_NullConnector_Throws()
    {
        Diagnostics.Arrange("connector", "(null)");

        var thrown = Assert.ThrowsExactly<ArgumentNullException>(() => new TelnetProtocolHandler(null!));

        Diagnostics.Act("thrown", thrown.GetType().Name);
        Diagnostics.Assert("thrown", nameof(ArgumentNullException), thrown.GetType().Name);
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws()
    {
        Diagnostics.Arrange("context", "(null)");
        var handler = new TelnetProtocolHandler(new RecordingConnector(ConnectResult.Connected(new ScriptedConnection())));

        var thrown = await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await handler.ExecuteAsync(null!));

        Diagnostics.Act("thrown", thrown.GetType().Name);
        Diagnostics.Assert("thrown", nameof(ArgumentNullException), thrown.GetType().Name);
    }

    [TestMethod]
    public async Task ExecuteAsync_UrlWithoutPort_ConnectsToPort23WithoutTls()
    {
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection()));
        Diagnostics.Arrange("url", TelnetUrl.OriginalString);

        await new TelnetProtocolHandler(connector).ExecuteAsync(Context(TelnetUrl, new MemoryStream()));

        Diagnostics.Act("connect targets", string.Join(" | ", connector.Targets));
        Diagnostics.Assert("connect target", new ConnectTarget("example.test", 23, false), connector.Targets.Single());
        Assert.AreEqual(new ConnectTarget("example.test", 23, false), connector.Targets.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_UrlWithPort_ConnectsToThatPort()
    {
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection()));
        Diagnostics.Arrange("url", "telnet://[::1]:2323/");

        await new TelnetProtocolHandler(connector).ExecuteAsync(
            Context(CurlUrl.Parse("telnet://[::1]:2323/"), new MemoryStream()));

        Diagnostics.Act("connect targets", string.Join(" | ", connector.Targets));
        Diagnostics.Assert("connect target", new ConnectTarget("::1", 2323, false), connector.Targets.Single());
        Assert.AreEqual(new ConnectTarget("::1", 2323, false), connector.Targets.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_ContextWithProxy_TunnelsToTheOriginOnPort23ThroughThatProxy()
    {
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection()));
        var proxy = new ProxyEndpoint(ProxyKind.Http, "proxy.example", 3128, null);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("telnet://example.com/"),
            Output = new MemoryStream(),
            Upload = new MemoryStream(),
            Proxy = proxy,
        };
        Diagnostics.ArrangeContext(context);

        await new TelnetProtocolHandler(connector).ExecuteAsync(context);

        Diagnostics.Act("connect targets", string.Join(" | ", connector.Targets));
        Diagnostics.Assert("connect target", new ConnectTarget("example.com", 23, false) { Proxy = proxy }, connector.Targets.Single());
        Assert.AreEqual(new ConnectTarget("example.com", 23, false) { Proxy = proxy }, connector.Targets.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_ContextWithEvents_PassesThemToTheConnectTargetSoTheConnectLinesAreReported()
    {
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection()));
        var events = new IgnoringTransferEvents();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("telnet://example.com/"),
            Output = new MemoryStream(),
            Upload = new MemoryStream(),
            Events = events,
        };
        Diagnostics.ArrangeContext(context);
        Diagnostics.Arrange("events", nameof(IgnoringTransferEvents));

        await new TelnetProtocolHandler(connector).ExecuteAsync(context);

        Diagnostics.Act("connect target events", connector.Targets.Single().Events?.GetType().Name ?? "(null)");
        Diagnostics.Assert("connect target events are the context's", true, ReferenceEquals(events, connector.Targets.Single().Events));
        Assert.AreSame(events, connector.Targets.Single().Events);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContextWithoutProxy_ConnectsDirectly()
    {
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection()));
        Diagnostics.Arrange("url", TelnetUrl.OriginalString);
        Diagnostics.Arrange("proxy", "(none)");

        await new TelnetProtocolHandler(connector).ExecuteAsync(Context(TelnetUrl, new MemoryStream()));

        Diagnostics.Act("connect targets", string.Join(" | ", connector.Targets));
        Diagnostics.Assert("connect target proxy", "(null)", connector.Targets.Single().Proxy?.ToString() ?? "(null)");
        Assert.IsNull(connector.Targets.Single().Proxy);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_ReturnsTheFailureUnchangedAndWritesNothing()
    {
        var connector = new RecordingConnector(
            ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect to example.test port 23"));
        var output = new MemoryStream();
        Diagnostics.Arrange("connect", "fails with CouldntConnect (7)");

        TransferResult result = await new TelnetProtocolHandler(connector).ExecuteAsync(Context(TelnetUrl, output));

        Diagnostics.ActResult(result);
        Diagnostics.ActOutput(output.ToArray());
        Diagnostics.AssertExitCode(CurlExitCode.CouldntConnect, result.ExitCode);
        Diagnostics.Diff("error", "Failed to connect to example.test port 23", result.ErrorMessage ?? string.Empty);
        Diagnostics.Assert("output length", 0L, output.Length);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to example.test port 23", result.ErrorMessage);
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.AreEqual(0L, output.Length);
        Assert.IsFalse(result.IsConnectionRefused);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectRefused_ReturnsExit7MarkedConnectionRefused()
    {
        var connector = new RecordingConnector(ConnectResult.Refused("Failed to connect to example.test port 23"));
        Diagnostics.Arrange("connect", "refused");

        TransferResult result = await new TelnetProtocolHandler(connector).ExecuteAsync(Context(TelnetUrl, new MemoryStream()));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.CouldntConnect, result.ExitCode);
        Diagnostics.Assert("connection refused", true, result.IsConnectionRefused);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    public async Task ExecuteAsync_StdinLinesAndServerLineThenClose_SendsStdinUnchangedAndWritesTheLine()
    {
        Exchange exchange = await RunAsync(
            Encoding.ASCII.GetBytes("a\nb\n"),
            new ScriptedRead(Encoding.ASCII.GetBytes("hi\r\n"), AfterBytesSent: 4));

        Diagnostics.Diff("sent", "61 0A 62 0A", exchange.Sent);
        Diagnostics.Diff("output", "68 69 0D 0A", exchange.Output);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, exchange.Result.ExitCode);
        Assert.AreEqual("61 0A 62 0A", exchange.Sent);
        Assert.AreEqual("68 69 0D 0A", exchange.Output);
        Assert.AreEqual(CurlExitCode.Ok, exchange.Result.ExitCode);
        Assert.AreEqual(4L, exchange.Result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyStdinAndServerLineThenClose_SendsNothingAndWritesTheLine()
    {
        Exchange exchange = await RunAsync([], Read("68 69 0D 0A"));

        Diagnostics.Diff("sent", string.Empty, exchange.Sent);
        Diagnostics.Diff("output", "68 69 0D 0A", exchange.Output);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, exchange.Result.ExitCode);
        Assert.AreEqual(string.Empty, exchange.Sent);
        Assert.AreEqual("68 69 0D 0A", exchange.Output);
        Assert.AreEqual(CurlExitCode.Ok, exchange.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoUpload_WritesWhatTheServerSends()
    {
        var connection = new ScriptedConnection(Read("68 69"));
        var output = new MemoryStream();
        Diagnostics.ArrangeReads([Read("68 69")]);
        Diagnostics.Arrange("upload", "(none)");

        TransferResult result = await new TelnetProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(new TransferContext { Url = TelnetUrl, Output = output });

        Diagnostics.ActResult(result);
        Diagnostics.ActSent(connection.Sent);
        Diagnostics.ActOutput(output.ToArray());
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Diff("output", "68 69", ToHex(output.ToArray()));
        Diagnostics.Assert("connection disposed", true, connection.IsDisposed);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("68 69", ToHex(output.ToArray()));
        Assert.IsEmpty(connection.Sent);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerWillEchoAndDoubledIac_AnswersDoEchoOffersAndWritesOneFF()
    {
        Exchange exchange = await RunAsync([], Read("FF FB 01 68 69 FF FF 78 0D 0A"));

        Diagnostics.Diff("sent", "FF FD 01 " + Offers, exchange.Sent);
        Diagnostics.Diff("output", "68 69 FF 78 0D 0A", exchange.Output);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, exchange.Result.ExitCode);
        Assert.AreEqual("FF FD 01 " + Offers, exchange.Sent);
        Assert.AreEqual("68 69 FF 78 0D 0A", exchange.Output);
        Assert.AreEqual(CurlExitCode.Ok, exchange.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_CommandSplitAcrossReads_IsRemovedAndAnswered()
    {
        Exchange exchange = await RunAsync([], Read("FF"), Read("FB 01 68 69"));

        Diagnostics.Diff("sent", "FF FD 01 " + Offers, exchange.Sent);
        Diagnostics.Diff("output", "68 69", exchange.Output);
        Assert.AreEqual("FF FD 01 " + Offers, exchange.Sent);
        Assert.AreEqual("68 69", exchange.Output);
    }

    [TestMethod]
    public async Task ExecuteAsync_StdinContainsFF_SendsItDoubled()
    {
        Exchange exchange = await RunAsync([0x61, 0xFF, 0x62, 0x0A], new ScriptedRead([0x68, 0x69], AfterBytesSent: 5));

        Diagnostics.Diff("sent", "61 FF FF 62 0A", exchange.Sent);
        Diagnostics.Diff("output", "68 69", exchange.Output);
        Assert.AreEqual("61 FF FF 62 0A", exchange.Sent);
        Assert.AreEqual("68 69", exchange.Output);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadStillWaitingWhenServerCloses_EndsWithExit0()
    {
        var connection = new ScriptedConnection(Read("68 69"));
        var output = new MemoryStream();
        Diagnostics.ArrangeReads([Read("68 69")]);
        Diagnostics.Arrange("upload", nameof(NeverEndingUploadStream));

        TransferResult result = await new TelnetProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(new TransferContext { Url = TelnetUrl, Output = output, Upload = new NeverEndingUploadStream() });

        Diagnostics.ActResult(result);
        Diagnostics.ActOutput(output.ToArray());
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("output length", 2L, output.Length);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(2, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadCannotBeRead_KeepsReceivingUntilTheServerCloses()
    {
        var connection = new ScriptedConnection(Read("68 69"));
        var output = new MemoryStream();
        Diagnostics.ArrangeReads([Read("68 69")]);
        Diagnostics.Arrange("upload", nameof(FaultingUploadStream));

        TransferResult result = await new TelnetProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(new TransferContext { Url = TelnetUrl, Output = output, Upload = new FaultingUploadStream() });

        Diagnostics.ActResult(result);
        Diagnostics.ActSent(connection.Sent);
        Diagnostics.ActOutput(output.ToArray());
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("output length", 2L, output.Length);
        Diagnostics.Assert("sent length", 0, connection.Sent.Length);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(2, output.Length);
        Assert.IsEmpty(connection.Sent);
    }

    [TestMethod]
    [DataRow("FF FD 18", "FF FC 18 " + Offers, DisplayName = "DO TTYPE is refused with WONT TTYPE")]
    [DataRow("FF FB 18", "FF FE 18 " + Offers, DisplayName = "WILL TTYPE is refused with DONT TTYPE")]
    [DataRow("FF FD 01", "FF FC 01 " + Offers, DisplayName = "DO ECHO is refused with WONT ECHO")]
    [DataRow("FF FC 01", Offers, DisplayName = "WONT ECHO needs no answer")]
    [DataRow("FF FE 00", Offers, DisplayName = "DONT BINARY needs no answer")]
    [DataRow("FF FB 03", "FF FD 03 FF FB 00 FF FD 00 FF FB 03", DisplayName = "WILL SGA is accepted and not offered again")]
    [DataRow("FF FD 03", "FF FB 03 FF FB 00 FF FD 00 FF FD 03", DisplayName = "DO SGA is accepted and not offered again")]
    [DataRow("FF FB 00", "FF FD 00 FF FB 00 FF FB 03 FF FD 03", DisplayName = "WILL BINARY is accepted and not offered again")]
    [DataRow("FF FD 00", "FF FB 00 FF FD 00 FF FB 03 FF FD 03", DisplayName = "DO BINARY is accepted and not offered again")]
    [DataRow("FF FB 01 FF FD 18 78", "FF FD 01 FF FC 18 " + Offers, DisplayName = "two commands in one read are both answered before the offers")]
    [DataRow("FF FB 01 FF FB 01", "FF FD 01 " + Offers, DisplayName = "WILL ECHO repeated is answered once")]
    [DataRow("FF FD 00 FF FE 00", "FF FB 00 FF FC 00 " + Offers, DisplayName = "DO then DONT BINARY is agreed then withdrawn")]
    [DataRow("FF FB 00 FF FC 00", "FF FD 00 FF FE 00 " + Offers, DisplayName = "WILL then WONT BINARY is agreed then withdrawn")]
    public async Task ExecuteAsync_ServerNegotiatesInOneRead_SendsCurlsAnswersAndOffers(string received, string expectedSent)
    {
        Exchange exchange = await RunAsync([], Read(received));

        Diagnostics.Diff("sent", expectedSent, exchange.Sent);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, exchange.Result.ExitCode);
        Assert.AreEqual(expectedSent, exchange.Sent);
        Assert.AreEqual(CurlExitCode.Ok, exchange.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerAnswersTheOffersThenRenegotiates_FollowsRfc1143()
    {
        Exchange exchange = await RunAsync(
            [],
            Read("FF FB 01"),
            new ScriptedRead(Hex("FF FD 00 FF FE 03 FF FB 00 FF FC 03"), AfterBytesSent: 15),
            Read("FF FE 00 FF FC 00"),
            new ScriptedRead(Hex("FF FB 01 FF FD 00 FF FB 03 FF FD 03"), AfterBytesSent: 21));

        string expectedSent = "FF FD 01 " + Offers
            + " FF FC 00 FF FE 00"
            + " FF FB 00 FF FD 03 FF FB 03";
        Diagnostics.Diff("sent", expectedSent, exchange.Sent);
        Assert.AreEqual(
            "FF FD 01 " + Offers
            + " FF FC 00 FF FE 00"
            + " FF FB 00 FF FD 03 FF FB 03",
            exchange.Sent);
    }

    [TestMethod]
    [DataRow("61 FF F1 62 0D 00 63 0D 0A", "61 62 0D 63 0D 0A", DisplayName = "IAC NOP is removed and NUL after CR dropped")]
    [DataRow("61 FF F2 62 FF F9 63", "61 62 63", DisplayName = "IAC DM and IAC GA are removed")]
    [DataRow("61 0D 62 0D 0A 63 0D", "61 0D 62 0D 0A 63 0D", DisplayName = "bare CR and CRLF are written unchanged")]
    [DataRow("61 0D FF FB 01 62", "61 0D FF FB 01 62", DisplayName = "IAC straight after CR is written as data")]
    [DataRow("61 0D FF FF 62", "61 0D FF", DisplayName = "CR IAC IAC writes one FF and starts a command")]
    [DataRow("61 0D 0D 00 62", "61 0D 0D 00 62", DisplayName = "NUL after a CR that followed a CR is written")]
    [DataRow("61 FF FA 05 01 FF F0 62", "61 62", DisplayName = "subnegotiation of an unhandled option is removed")]
    [DataRow("61 FF FA FF F0 62", "61 62", DisplayName = "empty subnegotiation is removed")]
    [DataRow("61 FF FA 05 FF FF 01 FF F0 62", "61 62", DisplayName = "IAC IAC inside a subnegotiation is removed")]
    public async Task ExecuteAsync_ServerSendsCommandsWithoutNegotiating_WritesCurlsBytesAndSendsNothing(
        string received,
        string expectedOutput)
    {
        Exchange exchange = await RunAsync([], Read(received));

        Diagnostics.Diff("output", expectedOutput, exchange.Output);
        Diagnostics.Diff("sent", string.Empty, exchange.Sent);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, exchange.Result.ExitCode);
        Assert.AreEqual(expectedOutput, exchange.Output);
        Assert.AreEqual(string.Empty, exchange.Sent);
        Assert.AreEqual(CurlExitCode.Ok, exchange.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_NewEnvironmentSubnegotiation_AnswersWithAnEmptyIsList()
    {
        Exchange exchange = await RunAsync([], Read("61 FF FA 27 01 FF F0 62"));

        Diagnostics.Diff("sent", "FF FA 27 00 FF F0", exchange.Sent);
        Diagnostics.Diff("output", "61 62", exchange.Output);
        Assert.AreEqual("FF FA 27 00 FF F0", exchange.Sent);
        Assert.AreEqual("61 62", exchange.Output);
    }

    [TestMethod]
    public async Task ExecuteAsync_NewEnvironmentSubnegotiationThenWillEcho_AnswersBothThenOffers()
    {
        Exchange exchange = await RunAsync([], Read("61 FF FA 27 01 FF F0 62 FF FB 01"));

        Diagnostics.Diff("sent", "FF FA 27 00 FF F0 FF FD 01 " + Offers, exchange.Sent);
        Diagnostics.Diff("output", "61 62", exchange.Output);
        Assert.AreEqual("FF FA 27 00 FF F0 FF FD 01 " + Offers, exchange.Sent);
        Assert.AreEqual("61 62", exchange.Output);
    }

    [TestMethod]
    [DataRow("61 FF FA 18 01 FF F0 62", DisplayName = "TTYPE SEND")]
    [DataRow("61 FF FA 23 01 FF F0 62", DisplayName = "XDISPLOC SEND")]
    public async Task ExecuteAsync_SubnegotiationForAValueNoOptionGave_ExitsWith43(string received)
    {
        Exchange exchange = await RunAsync([], Read(received), Read("63"));

        Diagnostics.AssertExitCode(CurlExitCode.BadFunctionArgument, exchange.Result.ExitCode);
        Diagnostics.Diff("error", "A libcurl function was given a bad argument", exchange.Result.ErrorMessage ?? string.Empty);
        Diagnostics.Diff("output", "61", exchange.Output);
        Diagnostics.Diff("sent", string.Empty, exchange.Sent);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, exchange.Result.ExitCode);
        Assert.AreEqual("A libcurl function was given a bad argument", exchange.Result.ErrorMessage);
        Assert.AreEqual("61", exchange.Output);
        Assert.AreEqual(1L, exchange.Result.BytesTransferred);
        Assert.AreEqual(string.Empty, exchange.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_SubnegotiationInterruptedByACommand_ExitsWith56()
    {
        Exchange exchange = await RunAsync([], Read("61 FF FA 05 01 FF FB 01 62"));

        Diagnostics.AssertExitCode(CurlExitCode.RecvError, exchange.Result.ExitCode);
        Diagnostics.Diff("error", "telnet: suboption error", exchange.Result.ErrorMessage ?? string.Empty);
        Diagnostics.Diff("output", "61", exchange.Output);
        Diagnostics.Diff("sent", string.Empty, exchange.Sent);
        Assert.AreEqual(CurlExitCode.RecvError, exchange.Result.ExitCode);
        Assert.AreEqual("telnet: suboption error", exchange.Result.ErrorMessage);
        Assert.AreEqual("61", exchange.Output);
        Assert.AreEqual(string.Empty, exchange.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectionReadIsReset_EndsWithExit0AndKeepsWhatWasWritten()
    {
        var connection = new FaultingConnection(Hex("68 69 0D 0A")) { ReadFailsAfterReads = true };
        var output = new MemoryStream();
        Diagnostics.Bytes("reply", Hex("68 69 0D 0A"));
        Diagnostics.Arrange("connection", "reads fail after the reply");

        TransferResult result = await new TelnetProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(new TransferContext { Url = TelnetUrl, Output = output });

        Diagnostics.ActResult(result);
        Diagnostics.ActOutput(output.ToArray());
        Diagnostics.AssertResult(TransferResult.Success(4), result);
        Diagnostics.Diff("output", "68 69 0D 0A", ToHex(output.ToArray()));
        Assert.AreEqual(TransferResult.Success(4), result);
        Assert.AreEqual("68 69 0D 0A", ToHex(output.ToArray()));
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadSendFailsWithoutSocketError_ExitsWith55FailedSendingDataToThePeer()
    {
        var connection = new FaultingConnection(Hex("68 69")) { WritesFail = true };
        var output = new MemoryStream();
        Diagnostics.Bytes("reply", Hex("68 69"));
        Diagnostics.Arrange("connection", "writes fail");
        Diagnostics.ArrangeUpload("a\n"u8.ToArray());

        TransferResult result = await new TelnetProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(new TransferContext { Url = TelnetUrl, Output = output, Upload = new MemoryStream("a\n"u8.ToArray()) });

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.SendError, result.ExitCode);
        Diagnostics.Diff("error", "Failed sending data to the peer", result.ErrorMessage ?? string.Empty);
        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("Failed sending data to the peer", result.ErrorMessage);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_NegotiationReplySendFailsWithoutSocketError_ExitsWith55FailedSendingDataToThePeer()
    {
        var connection = new FaultingConnection(Hex("68 69 FF FB 01")) { WritesFail = true };
        var output = new MemoryStream();
        Diagnostics.Bytes("reply", Hex("68 69 FF FB 01"));
        Diagnostics.Arrange("reply decoded", TelnetDiagnostics.Commands(Hex("68 69 FF FB 01")));
        Diagnostics.Arrange("connection", "writes fail");

        TransferResult result = await new TelnetProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(new TransferContext { Url = TelnetUrl, Output = output });

        Diagnostics.ActResult(result);
        Diagnostics.ActOutput(output.ToArray());
        Diagnostics.AssertResult(new TransferResult(CurlExitCode.SendError, 2, "Failed sending data to the peer"), result);
        Diagnostics.Diff("output", "68 69", ToHex(output.ToArray()));
        Assert.AreEqual(new TransferResult(CurlExitCode.SendError, 2, "Failed sending data to the peer"), result);
        Assert.AreEqual("68 69", ToHex(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_OutputWriteFails_ExitsWith23WriteFailure()
    {
        var connection = new ScriptedConnection(Read("68 69 0D 0A"), Read("78"));
        Diagnostics.ArrangeReads([Read("68 69 0D 0A"), Read("78")]);
        Diagnostics.Arrange("output", "refuses every write");

        TransferResult result = await new TelnetProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(new TransferContext { Url = TelnetUrl, Output = new FaultingOutputStream() });

        Diagnostics.ActResult(result);
        Diagnostics.AssertResult(
            new TransferResult(CurlExitCode.WriteError, 0, "Failure writing output to destination, passed 4 returned 0"),
            result);
        Assert.AreEqual(
            new TransferResult(CurlExitCode.WriteError, 0, "Failure writing output to destination, passed 4 returned 0"),
            result);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    [DataRow(100, 96)]
    [DataRow(300, 196)]
    [DataRow(1000, 96)]
    [DataRow(30, 16)]
    public async Task ExecuteAsync_OutputWriteFailsHavingAcceptedSome_ReportsTheBytesAccepted(int passed, int accepted)
    {
        var connection = new ScriptedConnection(new ScriptedRead(Line(passed)));
        Diagnostics.Arrange("line length", passed);
        Diagnostics.Arrange("output accepts", accepted);

        TransferResult result = await new TelnetProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(new TransferContext { Url = TelnetUrl, Output = new FaultingOutputStream(accepted) });

        Diagnostics.ActResult(result);
        var expected = new TransferResult(
            CurlExitCode.WriteError,
            0,
            $"Failure writing output to destination, passed {passed} returned {accepted}");
        Diagnostics.AssertResult(expected, result);
        Assert.AreEqual(
            new TransferResult(
                CurlExitCode.WriteError,
                0,
                $"Failure writing output to destination, passed {passed} returned {accepted}"),
            result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerOffersMoreThan4096Bytes_ReadsAndWritesAtMost4096()
    {
        var connection = new ScriptedConnection(new ScriptedRead([.. Line(5000), .. Line(5000)]));
        Diagnostics.Arrange("scripted read", "two 5000-byte lines in one read");
        Diagnostics.Arrange("output", "refuses every write");

        TransferResult result = await new TelnetProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(new TransferContext { Url = TelnetUrl, Output = new FaultingOutputStream() });

        Diagnostics.ActResult(result);
        Diagnostics.Act("read buffer lengths", string.Join(", ", connection.ReadBufferLengths));
        Diagnostics.Assert("first read buffer length", 4096, connection.ReadBufferLengths[0]);
        Diagnostics.AssertResult(
            new TransferResult(CurlExitCode.WriteError, 0, "Failure writing output to destination, passed 4096 returned 0"),
            result);
        Assert.AreEqual(4096, connection.ReadBufferLengths[0]);
        Assert.AreEqual(
            new TransferResult(CurlExitCode.WriteError, 0, "Failure writing output to destination, passed 4096 returned 0"),
            result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerOffersMoreThan4096Bytes_WritesEveryByte()
    {
        byte[] sent = [.. Line(5000), .. Line(5000)];
        var connection = new ScriptedConnection(new ScriptedRead(sent));
        var output = new MemoryStream();
        Diagnostics.Arrange("scripted read", "two 5000-byte lines in one read");

        TransferResult result = await new TelnetProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(new TransferContext { Url = TelnetUrl, Output = output });

        Diagnostics.ActResult(result);
        Diagnostics.ActOutput(output.ToArray());
        Diagnostics.AssertResult(TransferResult.Success(10000), result);
        Diagnostics.Diff("output", sent, output.ToArray());
        Assert.AreEqual(TransferResult.Success(10000), result);
        CollectionAssert.AreEqual(sent, output.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_Cancelled_Throws()
    {
        var connection = new ScriptedConnection(Read("68 69"));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var context = new TransferContext
        {
            Url = TelnetUrl,
            Output = new MemoryStream(),
            CancellationToken = cancellation.Token,
        };
        Diagnostics.ArrangeContext(context);
        Diagnostics.Arrange("cancellation", "cancelled before the transfer");

        var thrown = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await new TelnetProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection))).ExecuteAsync(context));

        Diagnostics.Act("thrown", thrown.GetType().Name);
        Diagnostics.Assert("connection disposed", true, connection.IsDisposed);
        Assert.IsTrue(connection.IsDisposed);
    }

    private static TransferContext Context(CurlUrl url, Stream output) =>
        new() { Url = url, Output = output, Upload = new MemoryStream() };

    private static ScriptedRead Read(string hex) => new(Hex(hex));

    /// <summary>A line of <paramref name="size" /> bytes, CRLF included, as the BL-099 listener sent.</summary>
    private static byte[] Line(int size) => [.. Enumerable.Repeat((byte)'x', size - 2), 0x0D, 0x0A];

    private static byte[] Hex(string hex) => Convert.FromHexString(hex.Replace(" ", string.Empty, StringComparison.Ordinal));

    private static string JoinHex(string joined, char[] pair) =>
        joined.Length == 0 ? new string(pair) : joined + " " + new string(pair);

    private static string ToHex(byte[] bytes) => Convert.ToHexString(bytes).Chunk(2).Aggregate(string.Empty, JoinHex);

    private async Task<Exchange> RunAsync(byte[] upload, params ScriptedRead[] reads)
    {
        var connection = new ScriptedConnection(reads);
        var output = new MemoryStream();
        var context = new TransferContext { Url = TelnetUrl, Output = output, Upload = new MemoryStream(upload) };
        Diagnostics.ArrangeContext(context);
        Diagnostics.ArrangeUpload(upload);
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

    private sealed record Exchange(TransferResult Result, string Sent, string Output);
}
