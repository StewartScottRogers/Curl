using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extension.SocketIo;

/// <summary>Checks <see cref="WebSocketMessageChannel" /> against RFC 6455 sections 5.4 (fragmentation), 5.5 (control frames) and 5.6 (data frames).</summary>
[TestClass]
public sealed class WebSocketMessageChannelTests
{
    [TestMethod]
    public async Task ReceiveTextAsync_AFragmentedTextMessage_GivesItWhole()
    {
        ScriptedConnection connection = new(ScriptedConnection.ServerFrame(WsOpcode.Continuation, "lo", isFinal: true));
        WebSocketMessageChannel channel = new(connection, new ZeroRandomSource(), ScriptedConnection.ServerFrame(WsOpcode.Text, "hel", isFinal: false));

        Assert.AreEqual("hello", await channel.ReceiveTextAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task ReceiveTextAsync_BinaryAndPongFrames_AreSkipped()
    {
        ScriptedConnection connection = new(
            ScriptedConnection.ServerFrame(WsOpcode.Binary, "b", isFinal: false),
            ScriptedConnection.ServerFrame(WsOpcode.Continuation, "in"),
            ScriptedConnection.ServerFrame(WsOpcode.Pong, ""),
            ScriptedConnection.ServerFrame(WsOpcode.Text, "4x"));
        WebSocketMessageChannel channel = new(connection, new ZeroRandomSource(), ReadOnlyMemory<byte>.Empty);

        Assert.AreEqual("4x", await channel.ReceiveTextAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task ReceiveTextAsync_APing_IsAnsweredWithAPongOfItsPayload()
    {
        ScriptedConnection connection = new([.. ScriptedConnection.ServerFrame(WsOpcode.Ping, "p1"), .. ScriptedConnection.ServerFrame(WsOpcode.Text, "m")]);
        WebSocketMessageChannel channel = new(connection, new ZeroRandomSource(), ReadOnlyMemory<byte>.Empty);

        string? text = await channel.ReceiveTextAsync(CancellationToken.None);

        Assert.AreEqual("m", text);
        CollectionAssert.AreEqual(new[] { "10:p1" }, connection.SentFrames());
    }

    [TestMethod]
    public async Task ReceiveTextAsync_ACloseFrameOrTheEndOfTheStream_GivesNull()
    {
        ScriptedConnection connection = new(ScriptedConnection.ServerFrame(WsOpcode.Close, ""));
        WebSocketMessageChannel channel = new(connection, new ZeroRandomSource(), ReadOnlyMemory<byte>.Empty);

        Assert.IsNull(await channel.ReceiveTextAsync(CancellationToken.None));
        Assert.IsNull(await channel.ReceiveTextAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task ReceiveTextAsync_AFrameWithReservedBits_FailsWithExit56()
    {
        ScriptedConnection connection = new([0xF1, 0x00]);
        WebSocketMessageChannel channel = new(connection, new ZeroRandomSource(), ReadOnlyMemory<byte>.Empty);

        SocketIoException failure = await Assert.ThrowsExactlyAsync<SocketIoException>(async () => await channel.ReceiveTextAsync(CancellationToken.None));

        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
    }

    [TestMethod]
    public async Task SendTextAsync_WritesOneMaskedTextFrame()
    {
        ScriptedConnection connection = new();
        WebSocketMessageChannel channel = new(connection, new ZeroRandomSource(), ReadOnlyMemory<byte>.Empty);

        await channel.SendTextAsync("40", CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 0x81, 0x82, 0, 0, 0, 0, (byte)'4', (byte)'0' }, connection.Writes[0]);
    }
}
