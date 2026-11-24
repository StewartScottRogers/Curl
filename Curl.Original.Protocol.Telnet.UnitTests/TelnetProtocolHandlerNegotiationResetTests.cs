using System.Net.Sockets;
using System.Text;
using Curl.Original.Protocol.Telnet.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Telnet;

/// <summary>
/// Pins how a <c>telnet://</c> session meets a connection read that fails during the option
/// negotiation: exit 56, never exit 0 with the server's data lost (BL-5882, ADR-1347),
/// against curl 8.23.0 on Windows.
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

    private async Task<(TransferResult Result, string Output)> RunAsync(FaultingConnection connection)
    {
        Diagnostics.Arrange("connection", "reads fail after the scripted ones");
        var output = new MemoryStream();
        TransferResult result = await new TelnetProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(new TransferContext { Url = TelnetUrl, Output = output });
        Diagnostics.ActResult(result);
        Diagnostics.ActOutput(output.ToArray());
        return (result, Encoding.Latin1.GetString(output.ToArray()));
    }
}
