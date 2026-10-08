using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins how the reply to an upgrade request is read, against curl 8.21.0 measured on
/// 2026-09-28 with <c>Record-CurlExchange.ps1</c> (BL-580).
/// </summary>
[TestClass]
public sealed class WsUpgradeResponseReaderTests
{
    private const string Head101 =
        "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: x\r\n\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadAsync_101WithAFrameBehindIt_ReturnsTheHeadAndKeepsTheFrame()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] frame = [0x81, 0x05, .. "hello"u8];
        diagnostics.Bytes("scripted head", Bytes(Head101));
        diagnostics.Bytes("scripted frame", frame);
        diagnostics.Arrange("scripted read count", 1);

        WsUpgradeResponse response = await Read([.. Bytes(Head101), .. frame]);

        diagnostics.Act("status code", response.StatusCode);
        diagnostics.Assert("status code", 101, response.StatusCode);
        Assert.AreEqual(101, response.StatusCode);
        diagnostics.Diff("head", Bytes(Head101), response.Head);
        CollectionAssert.AreEqual(Bytes(Head101), response.Head);
        diagnostics.Diff("remaining", frame, response.Remaining);
        CollectionAssert.AreEqual(frame, response.Remaining);
    }

    [TestMethod]
    public async Task ReadAsync_HeadSplitAcrossReads_JoinsIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("read 1", "HTTP/1.1 101 Sw\\r");
        diagnostics.Arrange("read 2", "\\nUpgrade: websocket\\r\\n\\r");
        diagnostics.Arrange("read 3", "\\n");

        WsUpgradeResponse response = await Read(Bytes("HTTP/1.1 101 Sw\r"), Bytes("\nUpgrade: websocket\r\n\r"), Bytes("\n"));

        string head = Encoding.Latin1.GetString(response.Head);
        diagnostics.Act("status code", response.StatusCode);
        diagnostics.Act("head", head);
        diagnostics.Assert("status code", 101, response.StatusCode);
        Assert.AreEqual(101, response.StatusCode);
        diagnostics.Diff("head", "HTTP/1.1 101 Sw\r\nUpgrade: websocket\r\n\r\n", head);
        Assert.AreEqual("HTTP/1.1 101 Sw\r\nUpgrade: websocket\r\n\r\n", Encoding.Latin1.GetString(response.Head));
        diagnostics.Assert("remaining length", 0, response.Remaining.Length);
        Assert.AreEqual(0, response.Remaining.Length);
    }

    [TestMethod]
    public async Task ReadAsync_BareLineFeeds_EndTheHead()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Bytes("scripted reply", Bytes("HTTP/1.1 101 Sw\nUpgrade: websocket\n\n"));
        diagnostics.Arrange("reader", "WsUpgradeResponseReader over the scripted reply above");

        WsUpgradeResponse response = await Read(Bytes("HTTP/1.1 101 Sw\nUpgrade: websocket\n\n"));

        diagnostics.Act("status code", response.StatusCode);
        diagnostics.Assert("status code", 101, response.StatusCode);
        Assert.AreEqual(101, response.StatusCode);
    }

    [TestMethod]
    public async Task ReadAsync_StatusLineAlone_EndsAtTheBlankLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Bytes("scripted reply", Bytes("HTTP/1.1 101\r\n\r\n"));
        diagnostics.Arrange("reader", "WsUpgradeResponseReader over the scripted reply above");

        WsUpgradeResponse response = await Read(Bytes("HTTP/1.1 101\r\n\r\n"));

        diagnostics.Act("status code", response.StatusCode);
        diagnostics.Assert("status code", 101, response.StatusCode);
        Assert.AreEqual(101, response.StatusCode);
    }

    [TestMethod]
    public async Task ReadAsync_200_ReturnsIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Bytes("scripted reply", Bytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok"));
        diagnostics.Arrange("reader", "WsUpgradeResponseReader over the scripted reply above");

        WsUpgradeResponse response = await Read(Bytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok"));

        diagnostics.Act("status code", response.StatusCode);
        diagnostics.Assert("status code", 200, response.StatusCode);
        Assert.AreEqual(200, response.StatusCode);
        diagnostics.Diff("remaining", Bytes("ok"), response.Remaining);
        CollectionAssert.AreEqual(Bytes("ok"), response.Remaining);
    }

    [TestMethod]
    public async Task ReadAsync_NothingReceived_FailsWithEmptyReply()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("scripted reads", "none");

        await AssertFails(diagnostics, CurlExitCode.GotNothing, "Empty reply from server");
    }

    [TestMethod]
    public async Task ReadAsync_ClosedBeforeTheHeadEnds_FailsWithEmptyReply()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Bytes("scripted reply", Bytes("HTTP/1.1 101 Sw\r\nUpgrade: websocket\r\n"));
        diagnostics.Arrange("reader", "WsUpgradeResponseReader over the scripted reply above");

        await AssertFails(diagnostics, CurlExitCode.GotNothing, "Empty reply from server", Bytes("HTTP/1.1 101 Sw\r\nUpgrade: websocket\r\n"));
    }

    [TestMethod]
    public async Task ReadAsync_BytesThatCannotBeginHttp_FailAtOnce()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Bytes("scripted reply", Bytes("garbage"));
        diagnostics.Arrange("reader", "WsUpgradeResponseReader over the scripted reply above");

        await AssertFails(diagnostics, CurlExitCode.UnsupportedProtocol, "Received HTTP/0.9 when not allowed", Bytes("garbage"));
    }

    [TestMethod]
    public async Task ReadAsync_HeadLongerThanTheLimit_FailsWithTooLarge()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] line = Bytes("X: " + new string('a', 4093) + "\r\n");
        byte[][] reads = [Bytes("HTTP/1.1 101 Sw\r\n"), .. Enumerable.Repeat(line, WsUpgradeResponseReader.MaximumHeadLength / line.Length + 1)];
        diagnostics.Arrange("maximum head length", WsUpgradeResponseReader.MaximumHeadLength);
        diagnostics.Arrange("scripted read count", reads.Length);

        await AssertFails(diagnostics, CurlExitCode.TooLarge, "A value or data field grew larger than allowed", reads);
    }

    [TestMethod]
    public async Task ReadAsync_HeadOfExactlyTheLimitInOneRead_IsAccepted()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("head length", WsUpgradeResponseReader.MaximumHeadLength);

        WsUpgradeResponse response = await Read([.. HeadOfLength(WsUpgradeResponseReader.MaximumHeadLength), 0x81, 0x00]);

        diagnostics.Act("head length", response.Head.Length);
        diagnostics.Assert("head length", WsUpgradeResponseReader.MaximumHeadLength, response.Head.Length);
        Assert.AreEqual(WsUpgradeResponseReader.MaximumHeadLength, response.Head.Length);
    }

    [TestMethod]
    public async Task ReadAsync_HeadOneByteOverTheLimitInOneRead_FailsWithTooLarge()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("head length", WsUpgradeResponseReader.MaximumHeadLength + 1);

        await AssertFails(diagnostics, CurlExitCode.TooLarge, "A value or data field grew larger than allowed", HeadOfLength(WsUpgradeResponseReader.MaximumHeadLength + 1));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ReadAsync_ReadResetByThePeer_FailsWithTheWinsockWords()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var reset = new IOException("reset", new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionReset));
        diagnostics.Arrange("read failure", "IOException wrapping SocketError.ConnectionReset");

        await AssertFailsOn(diagnostics, new FailingConnection(readFailure: reset), CurlExitCode.RecvError, "Recv failure: Connection was reset");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ReadAsync_ReadResetByThePeer_FailsWithTheSocketErrorsOwnMessage()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var reset = new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionReset);
        diagnostics.Arrange("read failure", "IOException wrapping SocketError.ConnectionReset");

        await AssertFailsOn(diagnostics, new FailingConnection(readFailure: new IOException("reset", reset)), CurlExitCode.RecvError, "Recv failure: " + reset.Message);
    }

    [TestMethod]
    public async Task ReadAsync_ReadFails_FailsWithReceiveFailure()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("read failure", "IOException: broken");

        await AssertFailsOn(diagnostics, new FailingConnection(readFailure: new IOException("broken")), CurlExitCode.RecvError, "Failure when receiving data from the peer");
    }

    [TestMethod]
    public async Task ReadAsync_HeadLongerThanTheFirstBuffer_GrowsTheBuffer()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] line = Bytes("X: " + new string('a', 4093) + "\r\n");
        byte[][] reads = [Bytes("HTTP/1.1 101 Sw\r\n"), .. Enumerable.Repeat(line, 8), Bytes("\r\n")];
        diagnostics.Arrange("scripted read count", reads.Length);
        diagnostics.Arrange("header line length", line.Length);

        WsUpgradeResponse response = await Read(reads);

        diagnostics.Act("status code", response.StatusCode);
        diagnostics.Act("head length", response.Head.Length);
        diagnostics.Assert("status code", 101, response.StatusCode);
        Assert.AreEqual(101, response.StatusCode);
        diagnostics.Assert("head length", 17 + (8 * 4098) + 2, response.Head.Length);
        Assert.AreEqual(17 + (8 * 4098) + 2, response.Head.Length);
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);

    private static ValueTask<WsUpgradeResponse> Read(params byte[][] reads) =>
        WsUpgradeResponseReader.ReadAsync(new ScriptedConnection(reads), CancellationToken.None);

    /// <summary>A <c>101</c> head of exactly <paramref name="length" /> bytes, padded in one header.</summary>
    private static byte[] HeadOfLength(int length)
    {
        const string Start = "HTTP/1.1 101 Sw\r\nX: ";
        return Bytes(Start + new string('a', length - Start.Length - 4) + "\r\n\r\n");
    }

    private static Task AssertFails(TestDiagnostics diagnostics, CurlExitCode exitCode, string message, params byte[][] reads) =>
        AssertFailsOn(diagnostics, new ScriptedConnection(reads), exitCode, message);

    private static async Task AssertFailsOn(TestDiagnostics diagnostics, IConnection connection, CurlExitCode exitCode, string message)
    {
        WsTransferException failure = await Assert.ThrowsExactlyAsync<WsTransferException>(
            async () => await WsUpgradeResponseReader.ReadAsync(connection, CancellationToken.None));

        diagnostics.Act("failure", $"{failure.GetType().Name}: {failure.ExitCode} ({failure.Message})");
        diagnostics.Assert("exit code", exitCode, failure.ExitCode);
        Assert.AreEqual(exitCode, failure.ExitCode);
        diagnostics.Assert("message", message, failure.Message);
        Assert.AreEqual(message, failure.Message);
    }
}
