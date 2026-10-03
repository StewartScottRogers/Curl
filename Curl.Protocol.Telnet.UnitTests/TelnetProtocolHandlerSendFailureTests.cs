using System.Net.Sockets;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Telnet.Fakes;

namespace Curl.Protocol.Telnet;

/// <summary>
/// Pins how a <c>telnet://</c> session meets a negotiation reply the connection cannot send,
/// against curl 8.21.0 measured on 2026-10-02 (BL-1307's Context): each reply is its own
/// write, and one that fails is reported as <c>Sending data failed (N)</c>, the socket error
/// number, while the session goes on to exit 0. The window size inside a NAWS
/// subnegotiation is the exception: curl sends it through <c>send_telnet_data</c>, whose
/// socket filter writes <c>Send failure: &lt;text&gt;</c> instead (BL-1312, ADR-0403). A
/// failed upload write still ends the session with exit 55.
/// </summary>
[TestClass]
public sealed class TelnetProtocolHandlerSendFailureTests
{
    /// <summary>The server's greeting in the measured run: <c>DO TERM-TYPE</c>, <c>WILL ECHO</c>, <c>hello</c>.</summary>
    private static readonly byte[] Greeting = Hex("FF FD 18 FF FB 01 68 65 6C 6C 6F");

    [TestMethod]
    public async Task ExecuteAsync_ServerNegotiates_SendsEachReplyAndOfferAsItsOwnWrite()
    {
        var connection = new SocketFailingConnection(Greeting);

        Session session = await RunAsync(connection, []);

        Assert.AreEqual(CurlExitCode.Ok, session.Result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "FFFC18", "FFFD01", "FFFB00", "FFFD00", "FFFB03", "FFFD03" },
            connection.Writes.Select(Convert.ToHexString).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_WritesFailAfterTheFirst_ReportsSendingDataFailedBeforeEachSentLineAndExitsWith0()
    {
        var connection = new SocketFailingConnection(Greeting) { SuccessfulWrites = 1 };
        string failed = $"* Sending data failed ({connection.Failure.NativeErrorCode})";

        Session session = await RunAsync(connection, []);

        Assert.AreEqual(TransferResult.Success(5), session.Result);
        Assert.AreEqual("hello", session.Output);
        CollectionAssert.AreEqual(
            new[]
            {
                "* RCVD DO TERM TYPE", "* SENT WONT TERM TYPE",
                "* RCVD WILL ECHO", failed, "* SENT DO ECHO",
                "<= hello",
                failed, "* SENT WILL BINARY",
                failed, "* SENT DO BINARY",
                failed, "* SENT WILL SUPPRESS GO AHEAD",
                failed, "* SENT DO SUPPRESS GO AHEAD",
                "* shutting down connection #0",
            },
            session.Transcript);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_WindowSizeWriteFailsOnWindows_ReportsTheSchannelBuildsSendFailureBetweenTheSuboptionFailures()
    {
        Session session = await RunWindowSizeFailureAsync();

        Assert.AreEqual("* Send failure: Connection was aborted", session.Transcript[6]);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_WindowSizeWriteFailsOffWindows_ReportsStrerrorsSendFailureBetweenTheSuboptionFailures()
    {
        Session session = await RunWindowSizeFailureAsync();

        Assert.AreEqual("* Send failure: Software caused connection abort", session.Transcript[6]);
    }

    [TestMethod]
    public async Task ExecuteAsync_WindowSizeSubnegotiationWritesFail_ReportsFailuresAfterTheSuboptionAndGoesOn()
    {
        var connection = new SocketFailingConnection(Hex("FF FD 1F"), Latin1("hi")) { SuccessfulWrites = 1 };
        string failed = $"* Sending data failed ({connection.Failure.NativeErrorCode})";
        string sendFailure = "* Send failure: " + CurlSocketErrorText.Words(connection.Failure, OperatingSystem.IsWindows());

        Session session = await RunAsync(connection, ["WS=80x24"]);

        Assert.AreEqual(TransferResult.Success(2), session.Result);
        Assert.AreEqual("hi", session.Output);
        CollectionAssert.AreEqual(
            new[]
            {
                "* RCVD DO NAWS", "* SENT WILL NAWS",
                "* SENT IAC SB ", "* NAWS", "* Width: 80 ; Height: 24",
                failed, sendFailure, failed,
                failed, "* SENT WILL BINARY",
                failed, "* SENT DO BINARY",
                failed, "* SENT WILL SUPPRESS GO AHEAD",
                failed, "* SENT DO SUPPRESS GO AHEAD",
                "<= hi",
                "* shutting down connection #0",
            },
            session.Transcript);
        CollectionAssert.AreEqual(
            new[] { "FFFB1F", "FFFA1F", "00500018", "FFF0" },
            connection.Writes.Take(4).Select(Convert.ToHexString).ToArray());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_UploadWriteIsAbortedOnWindows_ExitsWith55WithTheSchannelBuildsSendFailure()
    {
        var connection = new SocketFailingConnection { SuccessfulWrites = 0, StaysOpen = true };

        Session session = await RunAsync(connection, [], "a\n"u8.ToArray());

        Assert.AreEqual(new TransferResult(CurlExitCode.SendError, 0, "Send failure: Connection was aborted"), session.Result);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_UploadWriteIsAbortedOffWindows_ExitsWith55WithStrerrorsSendFailure()
    {
        var connection = new SocketFailingConnection { SuccessfulWrites = 0, StaysOpen = true };

        Session session = await RunAsync(connection, [], "a\n"u8.ToArray());

        Assert.AreEqual(new TransferResult(CurlExitCode.SendError, 0, "Send failure: " + connection.Failure.Message), session.Result);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_WindowSizeWriteIsNetworkResetOnWindows_ReportsNetworkHasBeenResetBesideTheSendingDataFailedLine()
    {
        var connection = new SocketFailingConnection(Hex("FF FD 1F"), Latin1("hi"))
        {
            SuccessfulWrites = 1,
            Failure = new SocketException((int)SocketError.NetworkReset),
        };

        Session session = await RunAsync(connection, ["WS=80x24"]);

        Assert.AreEqual("* Sending data failed (10052)", session.Transcript[5]);
        Assert.AreEqual("* Send failure: Network has been reset", session.Transcript[6]);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_WindowSizeWriteIsNetworkResetOffWindows_ReportsStrerrorsWordsBesideTheSendingDataFailedLine()
    {
        var failure = new SocketException((int)SocketError.NetworkReset);
        var connection = new SocketFailingConnection(Hex("FF FD 1F"), Latin1("hi")) { SuccessfulWrites = 1, Failure = failure };

        Session session = await RunAsync(connection, ["WS=80x24"]);

        Assert.AreEqual($"* Sending data failed ({failure.NativeErrorCode})", session.Transcript[5]);
        Assert.AreEqual("* Send failure: " + failure.Message, session.Transcript[6]);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplyWriteFailsWithoutSocketError_ExitsWith55FailedSendingDataToThePeer()
    {
        var connection = new FaultingConnection(Greeting) { WritesFail = true };

        Session session = await RunAsync(connection, []);

        Assert.AreEqual(new TransferResult(CurlExitCode.SendError, 5, "Failed sending data to the peer"), session.Result);
        Assert.AreEqual("* SENT WONT TERM TYPE", session.Transcript[1]);
    }

    private static Task<Session> RunWindowSizeFailureAsync() =>
        RunAsync(new SocketFailingConnection(Hex("FF FD 1F"), Latin1("hi")) { SuccessfulWrites = 1 }, ["WS=80x24"]);

    private static async Task<Session> RunAsync(IConnection connection, string[] telnetOptions, byte[]? upload = null)
    {
        var events = new TranscriptTransferEvents();
        var output = new MemoryStream();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("telnet://h/"),
            Output = output,
            Upload = upload is null ? null : new MemoryStream(upload),
            TelnetOptions = telnetOptions,
            Events = events,
        };

        TransferResult result = await new TelnetProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(context);
        return new Session(result, Encoding.Latin1.GetString(output.ToArray()), events.Transcript);
    }

    private static byte[] Hex(string hex) => Convert.FromHexString(hex.Replace(" ", string.Empty, StringComparison.Ordinal));

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

    private sealed record Session(TransferResult Result, string Output, List<string> Transcript);
}
