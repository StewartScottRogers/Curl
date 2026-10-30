using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.SocketIo;

/// <summary>Checks <see cref="SocketIoUpgrade" /> against RFC 6455 section 4 and Engine.IO v4, "Handshake".</summary>
[TestClass]
public sealed class SocketIoUpgradeTests
{
    [TestMethod]
    public void AcceptFor_TheRfc6455Example_GivesItsAccept() =>
        Assert.AreEqual("s3pPLMBiTxaQ9kYGzzhZRbK+xOo=", SocketIoUpgrade.AcceptFor("dGhlIHNhbXBsZSBub25jZQ=="));

    [TestMethod]
    [DataRow("socketio://example.com/chat?token=abc", "GET /socket.io/?EIO=4&transport=websocket&token=abc HTTP/1.1\r\nHost: example.com\r\n")]
    [DataRow("socketio://example.com:3000", "GET /socket.io/?EIO=4&transport=websocket HTTP/1.1\r\nHost: example.com:3000\r\n")]
    [DataRow("socketio://[::1]:3000/?", "GET /socket.io/?EIO=4&transport=websocket HTTP/1.1\r\nHost: [::1]:3000\r\n")]
    public void Request_AsksForTheWebSocketTransport(string url, string start) =>
        Assert.AreEqual(
            start + "Upgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: k\r\nSec-WebSocket-Version: 13\r\n\r\n",
            Encoding.ASCII.GetString(SocketIoUpgrade.Request(CurlUrl.Parse(url), "k")));

    [TestMethod]
    public async Task ReadReplyAsync_A101SplitOverReads_GivesTheBytesAfterTheHead()
    {
        byte[] head = ScriptedConnection.SwitchingProtocols();
        ScriptedConnection connection = new(head[..10], [.. head[10..], 0x81, 0x00]);

        ReadOnlyMemory<byte> after = await SocketIoUpgrade.ReadReplyAsync(connection, ZeroRandomSource.Key, CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 0x81, 0x00 }, after.ToArray());
    }

    [TestMethod]
    public async Task ReadReplyAsync_AnAcceptHeaderInLowerCase_IsAccepted()
    {
        ScriptedConnection connection = new(Encoding.ASCII.GetBytes($"HTTP/1.1 101 OK\r\nsec-websocket-accept:{SocketIoUpgrade.AcceptFor(ZeroRandomSource.Key)}\r\n\r\n"));

        ReadOnlyMemory<byte> after = await SocketIoUpgrade.ReadReplyAsync(connection, ZeroRandomSource.Key, CancellationToken.None);

        Assert.AreEqual(0, after.Length);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 400 Bad Request\r\n\r\n", CurlExitCode.HttpReturnedError, "Refused WebSocket upgrade: 400")]
    [DataRow("garbage\r\n\r\n", CurlExitCode.WeirdServerReply, "Socket.IO: the WebSocket upgrade reply has no status line")]
    [DataRow("SIP/2.0 101 x\r\n\r\n", CurlExitCode.WeirdServerReply, "Socket.IO: the WebSocket upgrade reply has no status line")]
    [DataRow("HTTP/1.1 101 OK\r\nSec-WebSocket-Accept: wrong\r\n\r\n", CurlExitCode.WeirdServerReply, "Socket.IO: the server's Sec-WebSocket-Accept does not match the key sent")]
    [DataRow("HTTP/1.1 101 OK\r\nX: y\r\n\r\n", CurlExitCode.WeirdServerReply, "Socket.IO: the server's Sec-WebSocket-Accept does not match the key sent")]
    [DataRow("HTTP/1.1 101 OK\r\n", CurlExitCode.GotNothing, "Empty reply from server")]
    public async Task ReadReplyAsync_AReplyThatDoesNotUpgrade_Fails(string reply, CurlExitCode exitCode, string message)
    {
        ScriptedConnection connection = new(Encoding.ASCII.GetBytes(reply));

        SocketIoException failure = await Assert.ThrowsExactlyAsync<SocketIoException>(async () => await SocketIoUpgrade.ReadReplyAsync(connection, ZeroRandomSource.Key, CancellationToken.None));

        Assert.AreEqual(exitCode, failure.ExitCode);
        Assert.AreEqual(message, failure.Message);
    }

    [TestMethod]
    public async Task ReadReplyAsync_AHeadLongerThanTheLimit_FailsWithExit8()
    {
        byte[] chunk = Encoding.ASCII.GetBytes("HTTP/1.1 101 OK\r\nX: " + new string('a', 4000) + "\r\n");
        ScriptedConnection connection = new([.. Enumerable.Repeat(chunk, 20)]);

        SocketIoException failure = await Assert.ThrowsExactlyAsync<SocketIoException>(async () => await SocketIoUpgrade.ReadReplyAsync(connection, ZeroRandomSource.Key, CancellationToken.None));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
    }
}
