using System.Net.Sockets;
using System.Text;
using Curl.Original.Protocol.Telnet.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Telnet;

/// <summary>
/// Pins how a <c>telnet://</c> session meets a connection read that fails during the option
/// negotiation: exit 56, never exit 0 with the server's data lost (BL-5882, ADR-1352),
/// against curl 8.23.0 on Windows, and one that strikes an upload the server has yet to
/// answer (BL-6012, ADR-1418). The Windows rule runs on every platform here, beside
/// the Linux and macOS rule (BL-5918, ADR-1382); each platform's own answer is pinned in
/// <c>TelnetProtocolHandlerResetByPlatformTests</c>.
/// </summary>
[TestClass]
public sealed class TelnetProtocolHandlerNegotiationResetTests
{
    private static readonly CurlUrl TelnetUrl = CurlUrl.Parse("telnet://127.0.0.1:2323/");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_ReadFailsAfterRepliesWithNoDataWritten_ExitsWith56ReceiveFailure()
    {
        var connection = new FaultingConnection([0xFF, 0xFD, 0x18]) { ReadFailsAfterReads = true };

        (TransferResult result, string output) = await RunAsync(connection);

        Diagnostics.AssertExitCode(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Failure when receiving data from the peer", result.ErrorMessage);
        Assert.AreEqual(0, result.BytesTransferred);
        Assert.AreEqual(string.Empty, output);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReadFailsAfterAReplyFailedToSend_ExitsWith56KeepingTheDataWritten()
    {
        var connection = new FaultingConnection([0xFF, 0xFD, 0x18, .. "login: "u8])
        {
            ReadFailsAfterReads = true,
            WriteFailure = new SocketException((int)SocketError.ConnectionAborted),
        };

        (TransferResult result, string output) = await RunAsync(connection);

        Diagnostics.AssertExitCode(CurlExitCode.RecvError, result.ExitCode);
        Diagnostics.Diff("output", "login: ", output);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(7, result.BytesTransferred);
        Assert.AreEqual("login: ", output);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReadFailsWithASocketErrorDuringNegotiation_ReportsRecvFailureWithItsWords()
    {
        var failure = new SocketException((int)SocketError.ConnectionReset);
        var connection = new FaultingConnection([0xFF, 0xFD, 0x18])
        {
            ReadFailsAfterReads = true,
            ReadFailure = failure,
        };

        (TransferResult result, _) = await RunAsync(connection);

        string expected = "Recv failure: " + CurlSocketErrorText.Words(failure, OperatingSystem.IsWindows());
        Diagnostics.Diff("message", expected, result.ErrorMessage ?? string.Empty);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(expected, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReadFailsAfterRepliesWentAndDataWasWritten_EndsWithExit0()
    {
        var connection = new FaultingConnection([0xFF, 0xFD, 0x18, .. "login: "u8]) { ReadFailsAfterReads = true };

        (TransferResult result, string output) = await RunAsync(connection);

        Diagnostics.AssertExitCode(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Diff("output", "login: ", output);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("login: ", output);
    }

    [TestMethod]
    public async Task ExecuteAsync_LinuxRuleReadFailsAfterRepliesWentAndDataWasWritten_ExitsWith56KeepingTheData()
    {
        var connection = new FaultingConnection([0xFF, 0xFD, 0x18, .. "login: "u8]) { ReadFailsAfterReads = true };

        (TransferResult result, string output) = await RunAsync(connection, runsOnWindows: false);

        Diagnostics.AssertExitCode(CurlExitCode.RecvError, result.ExitCode);
        Diagnostics.Diff("output", "login: ", output);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Failure when receiving data from the peer", result.ErrorMessage);
        Assert.AreEqual(7, result.BytesTransferred);
        Assert.AreEqual("login: ", output);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReadFailsAfterRepliesToTheDataThatFollowedTheUpload_ExitsWith56KeepingTheData()
    {
        // STD-201: the upload goes out after the first read, login: and its requests are answered,
        // and the reset destroys the server's last line.
        var connection = new FaultingConnection([0xFF, 0xFD, 0x18], [0xFF, 0xFD, 0x1F, .. "login: "u8])
        {
            ReadFailsAfterReads = true,
        };

        (TransferResult result, string output) = await RunAsync(connection, upload: "quit\n");

        Diagnostics.AssertExitCode(CurlExitCode.RecvError, result.ExitCode);
        Diagnostics.Diff("output", "login: ", output);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(7, result.BytesTransferred);
        Assert.AreEqual("login: ", output);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReadFailsAfterAnUploadSentSinceTheLastData_ExitsWith56KeepingTheData()
    {
        var connection = new FaultingConnection("login: "u8.ToArray()) { ReadFailsAfterReads = true };

        (TransferResult result, string output) = await RunAsync(connection, upload: "quit\n");

        Diagnostics.AssertExitCode(CurlExitCode.RecvError, result.ExitCode);
        Diagnostics.Diff("output", "login: ", output);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("login: ", output);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReadFailsAfterDataThatFollowedTheUpload_EndsWithExit0()
    {
        // The second read answers the upload; its negotiation-only successor writes no data.
        var connection = new FaultingConnection("login: "u8.ToArray(), "bye\r\n"u8.ToArray(), [0xFF, 0xF1])
        {
            ReadFailsAfterReads = true,
        };

        (TransferResult result, string output) = await RunAsync(connection, upload: "quit\n");

        Diagnostics.AssertExitCode(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Diff("output", "login: bye\r\n", output);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("login: bye\r\n", output);
    }

    private async Task<(TransferResult Result, string Output)> RunAsync(
        FaultingConnection connection,
        bool runsOnWindows = true,
        string? upload = null)
    {
        Diagnostics.Arrange("connection", "reads fail after the scripted ones");
        Diagnostics.Arrange("Windows rule", runsOnWindows);
        var output = new MemoryStream();
        var handler = new TelnetProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
        {
            RunsOnWindows = runsOnWindows,
        };
        Diagnostics.Arrange("upload", upload ?? "(none)");
        MemoryStream? source = upload is null ? null : new MemoryStream(Encoding.Latin1.GetBytes(upload));
        TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = TelnetUrl, Output = output, Upload = source });
        Diagnostics.ActResult(result);
        Diagnostics.ActOutput(output.ToArray());
        return (result, Encoding.Latin1.GetString(output.ToArray()));
    }
}
