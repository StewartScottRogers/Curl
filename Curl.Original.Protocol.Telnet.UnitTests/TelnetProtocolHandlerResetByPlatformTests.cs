using System.Net.Sockets;
using System.Text;
using Curl.Original.Protocol.Telnet.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Telnet;

/// <summary>
/// Pins each platform's answer to a <c>telnet://</c> connection reset, with the handler
/// choosing its platform itself: curl 8.23.0 on Windows exits 0 unless the reset strikes
/// the option negotiation (BL-5882, ADR-1352), and curl 8.18.0 on Linux always exits 56
/// <c>Recv failure: Connection reset by peer</c> (BL-5918, ADR-1376), which macOS shares.
/// </summary>
[TestClass]
public sealed class TelnetProtocolHandlerResetByPlatformTests
{
    private const string UnixResetMessage = "Recv failure: Connection reset by peer";

    private static readonly CurlUrl TelnetUrl = CurlUrl.Parse("telnet://127.0.0.1:2323/");

    private static readonly byte[] DoTerminalType = [0xFF, 0xFD, 0x18];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_OnWindowsIdleSessionIsReset_EndsWithExit0()
    {
        (TransferResult result, string output) = await RunResetAsync("login: "u8.ToArray());

        Diagnostics.AssertExitCode(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("login: ", output);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_OnWindowsSessionIsResetAfterNegotiationAndData_EndsWithExit0()
    {
        (TransferResult result, string output) = await RunResetAsync([.. DoTerminalType, .. "login: "u8]);

        Diagnostics.AssertExitCode(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("login: ", output);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_OffWindowsIdleSessionIsReset_ExitsWith56ConnectionResetByPeer()
    {
        (TransferResult result, string output) = await RunResetAsync("login: "u8.ToArray());

        Diagnostics.AssertExitCode(CurlExitCode.RecvError, result.ExitCode);
        Diagnostics.Diff("message", UnixResetMessage, result.ErrorMessage ?? string.Empty);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(UnixResetMessage, result.ErrorMessage);
        Assert.AreEqual("login: ", output);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_OffWindowsSessionIsResetBeforeAnyData_ExitsWith56ConnectionResetByPeer()
    {
        (TransferResult result, string output) = await RunResetAsync();

        Diagnostics.AssertExitCode(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(UnixResetMessage, result.ErrorMessage);
        Assert.AreEqual(string.Empty, output);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_OffWindowsSessionIsResetAfterNegotiationAndData_ExitsWith56ConnectionResetByPeer()
    {
        (TransferResult result, string output) = await RunResetAsync([.. DoTerminalType, .. "login: "u8]);

        Diagnostics.AssertExitCode(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(UnixResetMessage, result.ErrorMessage);
        Assert.AreEqual("login: ", output);
    }

    private async Task<(TransferResult Result, string Output)> RunResetAsync(params byte[][] reads)
    {
        var connection = new FaultingConnection(reads)
        {
            ReadFailsAfterReads = true,
            ReadFailure = new SocketException((int)SocketError.ConnectionReset),
        };
        Diagnostics.Arrange("connection", $"{reads.Length} reads, then a reset");
        var output = new MemoryStream();
        TransferResult result = await new TelnetProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(new TransferContext { Url = TelnetUrl, Output = output });
        Diagnostics.ActResult(result);
        Diagnostics.ActOutput(output.ToArray());
        return (result, Encoding.Latin1.GetString(output.ToArray()));
    }
}
