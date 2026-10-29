using System.Text;

namespace Curl.Extension.WebSocketRepl.Session;

[TestClass]
public sealed class WebSocketReplSessionTests
{
    private static readonly byte[] ServerClose1000 = ServerFrame(0x88, 0x03, 0xE8);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_LinesThenEndOfInput_SendsMaskedTextThenClose1000()
    {
        ScriptedConnection connection = new((3, ServerClose1000));

        (WebSocketReplOutcome outcome, _) = await RunAsync(connection, new ScriptedLineReader(true, "hello", "//ping"));

        Assert.AreEqual(new WebSocketReplOutcome(WebSocketReplEnding.ClosedByClient, null), outcome);
        AssertWrites(connection, ClientFrame(0x81, "hello"u8), ClientFrame(0x81, "/ping"u8), ClientFrame(0x88, [0x03, 0xE8]));
    }

    [TestMethod]
    public async Task RunAsync_Commands_SendBinaryPingAndCloseAndStopReadingLines()
    {
        ScriptedConnection connection = new((4, ServerClose1000));

        (WebSocketReplOutcome outcome, _) = await RunAsync(
            connection, new ScriptedLineReader(false, "/binary ab", "/ping", "/ping hi", "/close", "never sent"));

        Assert.AreEqual(WebSocketReplEnding.ClosedByClient, outcome.Ending);
        AssertWrites(
            connection,
            ClientFrame(0x82, "ab"u8),
            ClientFrame(0x89, []),
            ClientFrame(0x89, "hi"u8),
            ClientFrame(0x88, [0x03, 0xE8]));
    }

    [TestMethod]
    public async Task RunAsync_PingTextLongerThan125Bytes_SendsThe125First()
    {
        ScriptedConnection connection = new((1, ServerClose1000));

        await RunAsync(connection, new ScriptedLineReader(false, "/ping " + new string('x', 200)));

        CollectionAssert.AreEqual(ClientFrame(0x89, Encoding.ASCII.GetBytes(new string('x', 125))), connection.Writes[0]);
    }

    [TestMethod]
    public async Task RunAsync_ServerClosesFirst_AnswersWithItsCodeAndEnds()
    {
        ScriptedConnection connection = new((0, ServerFrame(0x88, 0x03, 0xE9, (byte)'b', (byte)'y', (byte)'e')));

        (WebSocketReplOutcome outcome, _) = await RunAsync(connection, new ScriptedLineReader(false));

        Assert.AreEqual(WebSocketReplEnding.ClosedByServer, outcome.Ending);
        AssertWrites(connection, ClientFrame(0x88, [0x03, 0xE9]));
    }

    [TestMethod]
    public async Task RunAsync_ServerCloseSplitAcrossReads_AnswersWithItsCode()
    {
        byte[] close = ServerFrame(0x88, 0x03, 0xE9, (byte)'x');
        ScriptedConnection connection = new((0, close[..3]), (0, close[3..]));

        (WebSocketReplOutcome outcome, _) = await RunAsync(connection, new ScriptedLineReader(false));

        Assert.AreEqual(WebSocketReplEnding.ClosedByServer, outcome.Ending);
        AssertWrites(connection, ClientFrame(0x88, [0x03, 0xE9]));
    }

    [TestMethod]
    public async Task RunAsync_ServerCloseWithOneByte_AnswersWithNoCode()
    {
        ScriptedConnection connection = new((0, ServerFrame(0x88, 0x03)));

        await RunAsync(connection, new ScriptedLineReader(false));

        AssertWrites(connection, ClientFrame(0x88, []));
    }

    [TestMethod]
    public async Task RunAsync_EmptyServerClose_AnswersWithAnEmptyClose()
    {
        ScriptedConnection connection = new((0, ServerFrame(0x88)));

        await RunAsync(connection, new ScriptedLineReader(false));

        AssertWrites(connection, ClientFrame(0x88, []));
    }

    [TestMethod]
    public async Task RunAsync_ReceivedMessages_ShowsTextAsItIsAndBinaryAsHex()
    {
        ScriptedConnection connection = new(
            (0, [.. ServerFrame(0x01, (byte)'h', (byte)'e'), .. ServerFrame(0x80, (byte)'y')]),
            (0, ServerFrame(0x82, 0x00, 0xAB)),
            (0, ServerFrame(0x02, 0x01)),
            (0, ServerFrame(0x80, 0xFF)),
            (0, ServerClose1000));

        (_, string shown) = await RunAsync(connection, new ScriptedLineReader(false));

        Assert.AreEqual("hey\n00ab\n01ff\n", shown);
    }

    [TestMethod]
    public async Task RunAsync_FinalFrameSplitAcrossReads_EndsTheLineOnlyAtItsEnd()
    {
        byte[] text = ServerFrame(0x81, (byte)'a', (byte)'b');
        ScriptedConnection connection = new((0, text[..3]), (0, text[3..]), (0, ServerClose1000));

        (_, string shown) = await RunAsync(connection, new ScriptedLineReader(false));

        Assert.AreEqual("ab\n", shown);
    }

    [TestMethod]
    public async Task RunAsync_FramesWithTheHandshake_AreShownFirst()
    {
        ScriptedConnection connection = new((0, ServerClose1000));

        (_, string shown) = await RunAsync(connection, new ScriptedLineReader(false), ServerFrame(0x81, (byte)'h', (byte)'i'));

        Assert.AreEqual("hi\n", shown);
    }

    [TestMethod]
    public async Task RunAsync_Ping_AnsweredByPongWithItsPayloadAndNotShown()
    {
        ScriptedConnection connection = new((0, ServerFrame(0x89, (byte)'p')), (1, ServerFrame(0x8A, (byte)'q')), (1, ServerClose1000));

        (_, string shown) = await RunAsync(connection, new ScriptedLineReader(false));

        Assert.AreEqual(string.Empty, shown);
        AssertWrites(connection, ClientFrame(0x8A, "p"u8), ClientFrame(0x88, [0x03, 0xE8]));
    }

    [TestMethod]
    public async Task RunAsync_PingAfterClientClose_IsStillAnsweredAndTheCloseNotRepeated()
    {
        ScriptedConnection connection = new((1, ServerFrame(0x89)), (2, ServerClose1000));

        (WebSocketReplOutcome outcome, _) = await RunAsync(connection, new ScriptedLineReader(false, "/close"));

        Assert.AreEqual(WebSocketReplEnding.ClosedByClient, outcome.Ending);
        AssertWrites(connection, ClientFrame(0x88, [0x03, 0xE8]), ClientFrame(0x8A, []));
    }

    [TestMethod]
    public async Task RunAsync_ConnectionEnds_EndsTheSession()
    {
        ScriptedConnection connection = new();

        (WebSocketReplOutcome outcome, _) = await RunAsync(connection, new ScriptedLineReader(false));

        Assert.AreEqual(new WebSocketReplOutcome(WebSocketReplEnding.ConnectionEnded, null), outcome);
    }

    [TestMethod]
    public async Task RunAsync_MaskedServerFrame_SendsClose1002AndReportsTheViolation()
    {
        ScriptedConnection connection = new((0, [0x81, 0x81, 0, 0, 0, 0, (byte)'x']));

        (WebSocketReplOutcome outcome, _) = await RunAsync(connection, new ScriptedLineReader(false));

        Assert.AreEqual(new WebSocketReplOutcome(WebSocketReplEnding.ProtocolViolation, "[WS] masked input frame"), outcome);
        AssertWrites(connection, ClientFrame(0x88, [0x03, 0xEA]));
    }

    [TestMethod]
    public async Task RunAsync_Cancelled_ThrowsAndStopsReadingLines()
    {
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        using WebSocketReplSession session = new(new ScriptedConnection((1, ServerClose1000)), new FixedRandomSource());

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await session.RunAsync(new ScriptedLineReader(false), Stream.Null, ReadOnlyMemory<byte>.Empty, cancellation.Token));
    }

    [TestMethod]
    public async Task RunAsync_NullInputOrOutput_Throws()
    {
        using WebSocketReplSession session = new(new ScriptedConnection(), new FixedRandomSource());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await session.RunAsync(null!, Stream.Null, ReadOnlyMemory<byte>.Empty, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await session.RunAsync(TextReader.Null, null!, ReadOnlyMemory<byte>.Empty, TestContext.CancellationToken));
    }

    private static byte[] ServerFrame(byte first, params byte[] payload) => [first, (byte)payload.Length, .. payload];

    // The fixed random source gives a zero mask, so the payload goes unmasked.
    private static byte[] ClientFrame(byte first, ReadOnlySpan<byte> payload) =>
        [first, (byte)(0x80 | payload.Length), 0, 0, 0, 0, .. payload];

    private static void AssertWrites(ScriptedConnection connection, params byte[][] expected)
    {
        Assert.HasCount(expected.Length, connection.Writes);
        for (int index = 0; index < expected.Length; index++)
        {
            CollectionAssert.AreEqual(expected[index], connection.Writes[index], $"write {index}");
        }
    }

    private async Task<(WebSocketReplOutcome Outcome, string Shown)> RunAsync(
        ScriptedConnection connection, TextReader input, byte[]? alreadyReceived = null)
    {
        using WebSocketReplSession session = new(connection, new FixedRandomSource());
        using MemoryStream output = new();
        WebSocketReplOutcome outcome = await session.RunAsync(input, output, alreadyReceived ?? [], TestContext.CancellationToken);
        return (outcome, Encoding.UTF8.GetString(output.ToArray()));
    }
}
