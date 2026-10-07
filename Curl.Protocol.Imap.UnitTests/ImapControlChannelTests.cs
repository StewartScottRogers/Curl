using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Imap.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Imap;

/// <summary>
/// Pins the command tags curl 8.21.0 sends: <c>A</c> and a three-digit number counted in
/// an unsigned char, so <c>A255</c> is followed by <c>A000</c> (<c>lib/imap.c</c>,
/// <c>imap_sendf</c>).
/// </summary>
[TestClass]
public sealed class ImapControlChannelTests
{
    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Tag_BeforeAnyCommand_IsTheGreetingsStar()
    {
        Diagnostics.Arrange("commands sent", 0);
        var channel = new ImapControlChannel(new ScriptedConnection(), NoTransferEvents.Instance, CancellationToken.None);
        Diagnostics.Act("tag", DiagnosticText.Escape(channel.Tag));

        Diagnostics.Assert("tag", "*", channel.Tag);
        Assert.AreEqual("*", channel.Tag);
    }

    [TestMethod]
    public async Task SendCommandAsync_TagsCountFromA001AndWrapAfterA255()
    {
        var connection = new ScriptedConnection();
        var channel = new ImapControlChannel(connection, NoTransferEvents.Instance, CancellationToken.None);
        Diagnostics.Arrange("commands to send", "257 x NOOP");

        for (int command = 0; command < 257; command++)
        {
            await channel.SendCommandAsync("NOOP");
        }

        string[] lines = Encoding.Latin1.GetString(connection.Sent).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Diagnostics.Act("lines sent", lines.Length);
        Diagnostics.Act("lines 0, 254, 255, 256", DiagnosticText.Lines([lines[0], lines[254], lines[255], lines[256]]));
        Diagnostics.Act("tag", DiagnosticText.Escape(channel.Tag));

        Diagnostics.Diff("line 0", "A001 NOOP", lines[0]);
        Diagnostics.Diff("line 254", "A255 NOOP", lines[254]);
        Diagnostics.Diff("line 255", "A000 NOOP", lines[255]);
        Diagnostics.Diff("line 256", "A001 NOOP", lines[256]);
        Diagnostics.Assert("tag", "A001", channel.Tag);
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
        string script = "* 1 X {6}\r\nab\ncd)\r\nA001 OK done\r\n";
        Diagnostics.Arrange("server", DiagnosticText.Escape(script));
        var channel = new ImapControlChannel(
            new ScriptedConnection(Encoding.Latin1.GetBytes(script)), events, CancellationToken.None);
        await channel.SendCommandAsync("X");

        ImapResponse? response = await channel.ReadResponseAsync(_ => true);
        Diagnostics.Act("untagged", DiagnosticText.Lines(response!.Untagged));
        Diagnostics.Act("transcript", DiagnosticText.Lines(events.Transcript));

        Diagnostics.Diff("untagged", "* 1 X {6}\r\nab\ncd)\r", response.Untagged.Single());
        string[] expectedTranscript = ["> A001 X\r\n", "< * 1 X {6}\r\n", "< ab\n", "< cd)\r\n", "< A001 OK done\r\n"];
        Diagnostics.Diff(
            "transcript",
            DiagnosticText.Lines(expectedTranscript),
            DiagnosticText.Lines(events.Transcript));
        Assert.AreEqual("* 1 X {6}\r\nab\ncd)\r", response!.Untagged.Single());
        CollectionAssert.AreEqual(
            (string[])["> A001 X\r\n", "< * 1 X {6}\r\n", "< ab\n", "< cd)\r\n", "< A001 OK done\r\n"],
            events.Transcript);
    }

    [TestMethod]
    public async Task SendAsync_ConnectionReset_ReportsNothingSent()
    {
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("connection", "fails on the first write");
        var channel = new ImapControlChannel(new ScriptedConnection { WritesBeforeFailure = 0 }, events, CancellationToken.None);

        await channel.SendCommandAsync("NOOP");
        await channel.SendLineAsync("abc");
        await channel.SendBytesAsync(new byte[] { 1, 2 });
        Diagnostics.Act("transcript", DiagnosticText.Lines(events.Transcript));

        Diagnostics.Assert("transcript count", 0, events.Transcript.Count);
        Assert.IsEmpty(events.Transcript);
    }

    [TestMethod]
    public async Task StopReporting_ThenSendAndRead_ReportsNothing()
    {
        var events = new RecordingTransferEvents();
        string script = "A001 OK bye\r\n";
        Diagnostics.Arrange("server", DiagnosticText.Escape(script));
        var channel = new ImapControlChannel(new ScriptedConnection(Encoding.Latin1.GetBytes(script)), events, CancellationToken.None);

        channel.StopReporting();
        await channel.SendCommandAsync("LOGOUT");
        await channel.ReadResponseAsync(_ => true);
        Diagnostics.Act("transcript", DiagnosticText.Lines(events.Transcript));

        Diagnostics.Assert("transcript count", 0, events.Transcript.Count);
        Assert.IsEmpty(events.Transcript);
    }

    [TestMethod]
    public async Task ReadLiteralPieceAsync_NothingBuffered_ReadsNoMoreThanTheLiteralFromTheConnection()
    {
        string script = "hello)\r\n";
        Diagnostics.Arrange("server", DiagnosticText.Escape(script));
        var channel = new ImapControlChannel(new ScriptedConnection(Encoding.Latin1.GetBytes(script)), NoTransferEvents.Instance, CancellationToken.None);

        ReadOnlyMemory<byte> piece = await channel.ReadLiteralPieceAsync(5);
        string text = Encoding.Latin1.GetString(piece.Span);
        Diagnostics.Act("piece", DiagnosticText.Escape(text));
        Diagnostics.Act("has unread bytes", channel.HasUnreadBytes);

        Diagnostics.Diff("piece", "hello", text);
        Diagnostics.Assert("has unread bytes", false, channel.HasUnreadBytes);
        Assert.AreEqual("hello", Encoding.Latin1.GetString(piece.Span));
        Assert.IsFalse(channel.HasUnreadBytes);
    }
}
