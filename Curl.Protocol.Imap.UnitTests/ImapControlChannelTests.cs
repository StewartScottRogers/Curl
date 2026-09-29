using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Imap.Fakes;

namespace Curl.Protocol.Imap;

/// <summary>
/// Pins the command tags curl 8.21.0 sends: <c>A</c> and a three-digit number counted in
/// an unsigned char, so <c>A255</c> is followed by <c>A000</c> (<c>lib/imap.c</c>,
/// <c>imap_sendf</c>).
/// </summary>
[TestClass]
public sealed class ImapControlChannelTests
{
    [TestMethod]
    public void Tag_BeforeAnyCommand_IsTheGreetingsStar()
    {
        var channel = new ImapControlChannel(new ScriptedConnection(), NoTransferEvents.Instance, CancellationToken.None);

        Assert.AreEqual("*", channel.Tag);
    }

    [TestMethod]
    public async Task SendCommandAsync_TagsCountFromA001AndWrapAfterA255()
    {
        var connection = new ScriptedConnection();
        var channel = new ImapControlChannel(connection, NoTransferEvents.Instance, CancellationToken.None);

        for (int command = 0; command < 257; command++)
        {
            await channel.SendCommandAsync("NOOP");
        }

        string[] lines = Encoding.Latin1.GetString(connection.Sent).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual("A001 NOOP", lines[0]);
        Assert.AreEqual("A255 NOOP", lines[254]);
        Assert.AreEqual("A000 NOOP", lines[255]);
        Assert.AreEqual("A001 NOOP", lines[256]);
        Assert.AreEqual("A001", channel.Tag);
    }

    [TestMethod]
    public async Task ReadResponseAsync_WantedLineWithALiteral_ReportsTheLiteralSplitAtEachLineFeedAsCurlsLineReaderDoes()
    {
        var events = new RecordingTransferEvents();
        var channel = new ImapControlChannel(
            new ScriptedConnection(Encoding.Latin1.GetBytes("* 1 X {6}\r\nab\ncd)\r\nA001 OK done\r\n")), events, CancellationToken.None);
        await channel.SendCommandAsync("X");

        ImapResponse? response = await channel.ReadResponseAsync(_ => true);

        Assert.AreEqual("* 1 X {6}\r\nab\ncd)\r", response!.Untagged.Single());
        CollectionAssert.AreEqual(
            (string[])["> A001 X\r\n", "< * 1 X {6}\r\n", "< ab\n", "< cd)\r\n", "< A001 OK done\r\n"],
            events.Transcript);
    }

    [TestMethod]
    public async Task SendAsync_ConnectionReset_ReportsNothingSent()
    {
        var events = new RecordingTransferEvents();
        var channel = new ImapControlChannel(new ScriptedConnection { WritesBeforeFailure = 0 }, events, CancellationToken.None);

        await channel.SendCommandAsync("NOOP");
        await channel.SendLineAsync("abc");
        await channel.SendBytesAsync(new byte[] { 1, 2 });

        Assert.IsEmpty(events.Transcript);
    }

    [TestMethod]
    public async Task StopReporting_ThenSendAndRead_ReportsNothing()
    {
        var events = new RecordingTransferEvents();
        var channel = new ImapControlChannel(new ScriptedConnection(Encoding.Latin1.GetBytes("A001 OK bye\r\n")), events, CancellationToken.None);

        channel.StopReporting();
        await channel.SendCommandAsync("LOGOUT");
        await channel.ReadResponseAsync(_ => true);

        Assert.IsEmpty(events.Transcript);
    }

    [TestMethod]
    public async Task ReadLiteralPieceAsync_NothingBuffered_ReadsNoMoreThanTheLiteralFromTheConnection()
    {
        var channel = new ImapControlChannel(new ScriptedConnection(Encoding.Latin1.GetBytes("hello)\r\n")), NoTransferEvents.Instance, CancellationToken.None);

        ReadOnlyMemory<byte> piece = await channel.ReadLiteralPieceAsync(5);

        Assert.AreEqual("hello", Encoding.Latin1.GetString(piece.Span));
        Assert.IsFalse(channel.HasUnreadBytes);
    }
}
