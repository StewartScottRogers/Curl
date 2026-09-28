using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws.Fakes;

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

    [TestMethod]
    public async Task ReadAsync_101WithAFrameBehindIt_ReturnsTheHeadAndKeepsTheFrame()
    {
        byte[] frame = [0x81, 0x05, .. "hello"u8];

        WsUpgradeResponse response = await Read([.. Bytes(Head101), .. frame]);

        Assert.AreEqual(101, response.StatusCode);
        CollectionAssert.AreEqual(Bytes(Head101), response.Head);
        CollectionAssert.AreEqual(frame, response.Remaining);
    }

    [TestMethod]
    public async Task ReadAsync_HeadSplitAcrossReads_JoinsIt()
    {
        WsUpgradeResponse response = await Read(Bytes("HTTP/1.1 101 Sw\r"), Bytes("\nUpgrade: websocket\r\n\r"), Bytes("\n"));

        Assert.AreEqual(101, response.StatusCode);
        Assert.AreEqual("HTTP/1.1 101 Sw\r\nUpgrade: websocket\r\n\r\n", Encoding.Latin1.GetString(response.Head));
        Assert.AreEqual(0, response.Remaining.Length);
    }

    [TestMethod]
    public async Task ReadAsync_BareLineFeeds_EndTheHead()
    {
        WsUpgradeResponse response = await Read(Bytes("HTTP/1.1 101 Sw\nUpgrade: websocket\n\n"));

        Assert.AreEqual(101, response.StatusCode);
    }

    [TestMethod]
    public async Task ReadAsync_StatusLineAlone_EndsAtTheBlankLine()
    {
        WsUpgradeResponse response = await Read(Bytes("HTTP/1.1 101\r\n\r\n"));

        Assert.AreEqual(101, response.StatusCode);
    }

    [TestMethod]
    public async Task ReadAsync_200_ReturnsIt()
    {
        WsUpgradeResponse response = await Read(Bytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok"));

        Assert.AreEqual(200, response.StatusCode);
        CollectionAssert.AreEqual(Bytes("ok"), response.Remaining);
    }

    [TestMethod]
    public async Task ReadAsync_NothingReceived_FailsWithEmptyReply()
    {
        await AssertFails(CurlExitCode.GotNothing, "Empty reply from server");
    }

    [TestMethod]
    public async Task ReadAsync_ClosedBeforeTheHeadEnds_FailsWithEmptyReply()
    {
        await AssertFails(CurlExitCode.GotNothing, "Empty reply from server", Bytes("HTTP/1.1 101 Sw\r\nUpgrade: websocket\r\n"));
    }

    [TestMethod]
    public async Task ReadAsync_BytesThatCannotBeginHttp_FailAtOnce()
    {
        await AssertFails(CurlExitCode.UnsupportedProtocol, "Received HTTP/0.9 when not allowed", Bytes("garbage"));
    }

    [TestMethod]
    public async Task ReadAsync_HeadLongerThanTheLimit_FailsWithTooLarge()
    {
        byte[] line = Bytes("X: " + new string('a', 4093) + "\r\n");
        byte[][] reads = [Bytes("HTTP/1.1 101 Sw\r\n"), .. Enumerable.Repeat(line, WsUpgradeResponseReader.MaximumHeadLength / line.Length + 1)];

        await AssertFails(CurlExitCode.TooLarge, "A value or data field grew larger than allowed", reads);
    }

    [TestMethod]
    public async Task ReadAsync_HeadOfExactlyTheLimitInOneRead_IsAccepted()
    {
        WsUpgradeResponse response = await Read([.. HeadOfLength(WsUpgradeResponseReader.MaximumHeadLength), 0x81, 0x00]);

        Assert.AreEqual(WsUpgradeResponseReader.MaximumHeadLength, response.Head.Length);
    }

    [TestMethod]
    public async Task ReadAsync_HeadOneByteOverTheLimitInOneRead_FailsWithTooLarge()
    {
        await AssertFails(CurlExitCode.TooLarge, "A value or data field grew larger than allowed", HeadOfLength(WsUpgradeResponseReader.MaximumHeadLength + 1));
    }

    [TestMethod]
    public async Task ReadAsync_ReadResetByThePeer_FailsWithRecvFailure()
    {
        var reset = new IOException("reset", new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionReset));

        await AssertFailsOn(new FailingConnection(readFailure: reset), CurlExitCode.RecvError, "Recv failure: Connection was reset");
    }

    [TestMethod]
    public async Task ReadAsync_ReadFails_FailsWithReceiveFailure()
    {
        await AssertFailsOn(new FailingConnection(readFailure: new IOException("broken")), CurlExitCode.RecvError, "Failure when receiving data from the peer");
    }

    [TestMethod]
    public async Task ReadAsync_HeadLongerThanTheFirstBuffer_GrowsTheBuffer()
    {
        byte[] line = Bytes("X: " + new string('a', 4093) + "\r\n");
        byte[][] reads = [Bytes("HTTP/1.1 101 Sw\r\n"), .. Enumerable.Repeat(line, 8), Bytes("\r\n")];

        WsUpgradeResponse response = await Read(reads);

        Assert.AreEqual(101, response.StatusCode);
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

    private static Task AssertFails(CurlExitCode exitCode, string message, params byte[][] reads) =>
        AssertFailsOn(new ScriptedConnection(reads), exitCode, message);

    private static async Task AssertFailsOn(IConnection connection, CurlExitCode exitCode, string message)
    {
        WsTransferException failure = await Assert.ThrowsExactlyAsync<WsTransferException>(
            async () => await WsUpgradeResponseReader.ReadAsync(connection, CancellationToken.None));

        Assert.AreEqual(exitCode, failure.ExitCode);
        Assert.AreEqual(message, failure.Message);
    }
}
