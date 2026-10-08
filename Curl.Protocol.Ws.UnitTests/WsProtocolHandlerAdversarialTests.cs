using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ws;

/// <summary>
/// Adversarial black-box tests of <see cref="WsProtocolHandler" /> (BL-1521, the method in
/// <c>Documentation/Wiki/Adversarial-Testing.md</c>): frames at the length boundaries, malformed
/// frames, every invalid frame partition, and frames delivered in every order and split a
/// handler's happy path never sees. Expected exit codes, messages and output bytes are curl
/// 8.21.0's (Schannel build), measured on 2026-10-07 with <c>Record-CurlExchange.ps1</c>
/// against the same 101 and frames.
/// </summary>
[TestClass]
public sealed class WsProtocolHandlerAdversarialTests
{
    private const string Head101 =
        "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=\r\n\r\n";

    // The pong curl sends for an empty ping: FIN and PONG, masked, length 0, mask 00 01 02 03.
    private const string EmptyPong = "\x8a\x80\x00\x01\x02\x03";

    private static readonly string Payload126 = new('y', 126);

    // Fragmented text, an empty ping between its fragments, a 16-bit binary frame and a close.
    private static readonly string MixedFrames = "\x01\x02he\x89\x00\x80\x03llo\x82\x7e\x00\x7e" + Payload126 + "\x88\x02\x03\xe8";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(0, "\x82\x00", DisplayName = "empty, 7-bit form")]
    [DataRow(125, "\x82\x7d", DisplayName = "125, longest 7-bit form")]
    [DataRow(126, "\x82\x7e\x00\x7e", DisplayName = "126, shortest 16-bit form")]
    [DataRow(65535, "\x82\x7e\xff\xff", DisplayName = "65535, longest 16-bit form")]
    [DataRow(65536, "\x82\x7f\x00\x00\x00\x00\x00\x01\x00\x00", DisplayName = "65536, shortest 64-bit form")]
    public async Task ExecuteAsync_BinaryFrameAtALengthFormBoundary_WritesTheWholePayload(int length, string head)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string payload = new('x', length);
        var output = new MemoryStream();
        diagnostics.Arrange("payload length", length);
        diagnostics.Bytes("frame head", Bytes(head));

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101 + head + payload + "\x88\x00"))).ExecuteAsync(Context(output));

        diagnostics.Act("result", Describe(result));
        diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        diagnostics.Assert("bytes written", (long)length, output.Length);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(payload, Encoding.Latin1.GetString(output.ToArray()));
    }

    [TestMethod]
    [DataRow("\x82\x7e\x00\x02ok", DisplayName = "2 bytes in the 16-bit form")]
    [DataRow("\x82\x7f\x00\x00\x00\x00\x00\x00\x00\x02ok", DisplayName = "2 bytes in the 64-bit form")]
    public async Task ExecuteAsync_ShortPayloadInALongerLengthFormThanNeeded_IsAcceptedAsCurlDoes(string frame)
    {
        // curl 8.21.0 accepts a length not in its shortest form: stdout "ok", exit 0.
        var diagnostics = TestDiagnostics.For(TestContext);
        var output = new MemoryStream();
        diagnostics.Bytes("frame", Bytes(frame));

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101 + frame))).ExecuteAsync(Context(output));

        diagnostics.Act("result", Describe(result));
        diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("ok", Encoding.Latin1.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_64BitLengthWithItsTopBitSet_FailsWith56AsCurlDoes()
    {
        // 2^63: curl: (56) [WS] frame length longer than 63 bits not supported, nothing written.
        var diagnostics = TestDiagnostics.For(TestContext);
        const string Frame = "\x82\x7f\x80\x00\x00\x00\x00\x00\x00\x00";
        var output = new MemoryStream();
        diagnostics.Bytes("frame head", Bytes(Frame));

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101 + Frame))).ExecuteAsync(Context(output));

        diagnostics.Act("result", Describe(result));
        diagnostics.Assert("error message", "[WS] frame length longer than 63 bits not supported", result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("[WS] frame length longer than 63 bits not supported", result.ErrorMessage);
        Assert.AreEqual(0L, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_LongestLegal64BitLengthThenTheServerCloses_SucceedsWithWhatArrived()
    {
        // 2^63 - 1 announced, two bytes sent, then the connection closes: curl writes what came.
        var diagnostics = TestDiagnostics.For(TestContext);
        const string Frame = "\x82\x7f\x7f\xff\xff\xff\xff\xff\xff\xffok";
        var output = new MemoryStream();
        diagnostics.Bytes("frame", Bytes(Frame));

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101 + Frame))).ExecuteAsync(Context(output));

        diagnostics.Act("result", Describe(result));
        diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("ok", Encoding.Latin1.GetString(output.ToArray()));
    }

    [TestMethod]
    [DataRow("\x89\x7e\x00\x7e", "[WS] received PING frame is too big", DisplayName = "ping of 126")]
    [DataRow("\x88\x7e\x00\x7e", "[WS] received CLOSE frame is too big", DisplayName = "close of 126")]
    [DataRow("\x8a\x7e\x00\x7e", "[WS] received PONG frame is too big", DisplayName = "pong of 126")]
    [DataRow("\x89\x7f\x00\x00\x00\x00\x00\x00\x00\x7e", "[WS] received PING frame is too big", DisplayName = "ping in the 64-bit form")]
    public async Task ExecuteAsync_ControlFramePast125Bytes_FailsWith56NamingTheFrame(string frame, string message)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = new ScriptedConnection(Bytes(Head101 + frame));
        diagnostics.Bytes("frame head", Bytes(frame));

        TransferResult result = await Handler(connection).ExecuteAsync(Context());

        diagnostics.Act("result", Describe(result));
        diagnostics.Assert("error message", message, result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_PingOfExactly125Bytes_EchoesItInAPong()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string payload = new('p', 125);
        var connection = new ScriptedConnection(Bytes(Head101 + "\x89\x7d" + payload));
        diagnostics.Arrange("ping payload length", 125);

        TransferResult result = await Handler(connection).ExecuteAsync(Context());

        string sent = Encoding.Latin1.GetString(connection.Sent);
        string pong = sent[sent.LastIndexOf("\r\n\r\n", StringComparison.Ordinal)..][4..];
        diagnostics.Act("result", Describe(result));
        diagnostics.Bytes("pong sent", Bytes(pong));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("\x8a\xfd\x00\x01\x02\x03", pong[..6]);
        Assert.AreEqual(131, pong.Length);
    }

    [TestMethod]
    [DataRow("\x09\x00", "[WS] invalid fragmented PING frame", DisplayName = "ping without FIN")]
    [DataRow("\x08\x00", "[WS] invalid fragmented CLOSE frame", DisplayName = "close without FIN")]
    [DataRow("\x0a\x00", "[WS] invalid fragmented PONG frame", DisplayName = "pong without FIN")]
    public async Task ExecuteAsync_FragmentedControlFrame_FailsWith56(string frame, string message)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Bytes("frame", Bytes(frame));

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101 + frame))).ExecuteAsync(Context());

        diagnostics.Act("result", Describe(result));
        diagnostics.Assert("error message", message, result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("\x81\x82\x00\x00\x00\x00ok", DisplayName = "masked text")]
    [DataRow("\x88\x82\x00\x00\x00\x00\x03\xe8", DisplayName = "masked close")]
    [DataRow("\x89\x80\x00\x00\x00\x00", DisplayName = "masked empty ping")]
    public async Task ExecuteAsync_MaskedServerFrame_FailsWith56AndWritesNothing(string frame)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var output = new MemoryStream();
        diagnostics.Bytes("frame", Bytes(frame));

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101 + frame))).ExecuteAsync(Context(output));

        diagnostics.Act("result", Describe(result));
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("[WS] masked input frame", result.ErrorMessage);
        Assert.AreEqual(0L, output.Length);
    }

    [TestMethod]
    [DataRow("\x91\x00", "[WS] invalid reserved bits: 91", DisplayName = "RSV3")]
    [DataRow("\xc1\x00", "[WS] invalid reserved bits: c1", DisplayName = "RSV1")]
    [DataRow("\xa1\x00", "[WS] invalid reserved bits: a1", DisplayName = "RSV2")]
    [DataRow("\x83\x00", "[WS] invalid opcode: 83", DisplayName = "opcode 3")]
    [DataRow("\x87\x00", "[WS] invalid opcode: 87", DisplayName = "opcode 7")]
    [DataRow("\x8b\x00", "[WS] invalid opcode: 8b", DisplayName = "opcode B")]
    [DataRow("\x8f\x00", "[WS] invalid opcode: 8f", DisplayName = "opcode F")]
    public async Task ExecuteAsync_ReservedBitOrUnknownOpcode_FailsWith56NamingTheFirstByte(string frame, string message)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Bytes("frame", Bytes(frame));

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101 + frame))).ExecuteAsync(Context());

        diagnostics.Act("result", Describe(result));
        diagnostics.Assert("error message", message, result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("\x80\x0001a", "", "[WS] no ongoing fragmented message to resume", DisplayName = "continuation with no message")]
    [DataRow("\x01\x0001a\x82\x0001b", "a", "[WS] fragmented message interrupted by new BINARY msg", DisplayName = "binary inside fragmented text")]
    [DataRow("\x02\x0001a\x81\x0001b", "a", "[WS] fragmented message interrupted by new TEXT msg", DisplayName = "text inside fragmented binary")]
    [DataRow("\x81\x0001a\x80\x0001b", "a", "[WS] no ongoing fragmented message to resume", DisplayName = "continuation after a final frame")]
    public async Task ExecuteAsync_FrameOutOfMessageOrder_FailsWith56AfterWritingWhatCameBefore(string frames, string written, string message)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var output = new MemoryStream();
        diagnostics.Bytes("frames", Bytes(frames));

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101 + frames))).ExecuteAsync(Context(output));

        diagnostics.Act("result", Describe(result));
        diagnostics.Assert("error message", message, result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        Assert.AreEqual(written, Encoding.Latin1.GetString(output.ToArray()));
    }

    [TestMethod]
    [DataRow("\x03\xe7", 999, DisplayName = "999, below the defined range")]
    [DataRow("\x03\xec", 1004, DisplayName = "1004, reserved")]
    [DataRow("\x03\xed", 1005, DisplayName = "1005, never sent on the wire")]
    [DataRow("\x03\xee", 1006, DisplayName = "1006, never sent on the wire")]
    [DataRow("\x13\x88", 5000, DisplayName = "5000, above the defined range")]
    public async Task ExecuteAsync_CloseWithAnInvalidCode_SucceedsWritingTheCodeAndWarnsOfIt(string code, int value)
    {
        // curl 8.21.0 does not police close codes: stdout is the two code bytes, exit 0.
        var diagnostics = TestDiagnostics.For(TestContext);
        var output = new MemoryStream();
        var log = new RecordingDiagnosticLog();
        diagnostics.Arrange("close code", value);

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101 + "\x88\x02" + code))).ExecuteAsync(Context(output, log));

        diagnostics.Act("result", Describe(result));
        diagnostics.Act("warnings", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Warning)));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(code, Encoding.Latin1.GetString(output.ToArray()));
        CollectionAssert.AreEqual(new[] { $"server closed with unexpected code {value}" }, log.MessagesAt(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task ExecuteAsync_CloseWithAOneBytePayload_SucceedsWritingTheByteWithoutAWarning()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var output = new MemoryStream();
        var log = new RecordingDiagnosticLog();
        diagnostics.Bytes("close frame", Bytes("\x88\x01\x03"));

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101 + "\x88\x01\x03"))).ExecuteAsync(Context(output, log));

        diagnostics.Act("result", Describe(result));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("\x03", Encoding.Latin1.GetString(output.ToArray()));
        Assert.IsEmpty(log.MessagesAt(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task ExecuteAsync_TextFrameAfterTheClose_IsStillWrittenAsCurlDoes()
    {
        // curl 8.21.0 keeps reading after a close: stdout 03 e8 'o' 'k', exit 0.
        var diagnostics = TestDiagnostics.For(TestContext);
        var output = new MemoryStream();

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101 + "\x88\x02\x03\xe8\x81\x02ok"))).ExecuteAsync(Context(output));

        diagnostics.Act("result", Describe(result));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("\x03\xe8ok", Encoding.Latin1.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_TextWithInvalidUtf8SplitAcrossFragments_WritesTheBytesUnchanged()
    {
        // curl 8.21.0 does not validate text: C3 | 28 (a lead byte, then a byte that cannot follow it) is written as sent.
        var diagnostics = TestDiagnostics.For(TestContext);
        var output = new MemoryStream();
        const string Frames = "\x01\x01\xc3\x80\x01\x28";
        diagnostics.Bytes("frames", Bytes(Frames));

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101 + Frames))).ExecuteAsync(Context(output));

        diagnostics.Act("result", Describe(result));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new byte[] { 0xc3, 0x28 }, output.ToArray());
    }

    [TestMethod]
    [DataRow("Sec-WebSocket-Accept: \r\n", DisplayName = "empty accept")]
    [DataRow("Sec-WebSocket-Accept: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=x\r\n", DisplayName = "accept one character long")]
    [DataRow("Sec-WebSocket-Accept: S3PPLMBITXAQ9KYGZZHZRBK+XOO=\r\n", DisplayName = "accept in the wrong case")]
    [DataRow("Sec-WebSocket-Accept: a\r\nSec-WebSocket-Accept: b\r\n", DisplayName = "two wrong accepts")]
    public async Task ExecuteAsync_101WithAMalformedAccept_StillSucceedsAsCurlDoes(string acceptLines)
    {
        // curl 8.21.0 does not check Sec-WebSocket-Accept (measured for BL-580).
        var diagnostics = TestDiagnostics.For(TestContext);
        string reply = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n" + acceptLines + "\r\n\x81\x02ok";
        var output = new MemoryStream();
        diagnostics.Bytes("reply", Bytes(reply));

        TransferResult result = await Handler(new ScriptedConnection(Bytes(reply))).ExecuteAsync(Context(output));

        diagnostics.Act("result", Describe(result));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("ok", Encoding.Latin1.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_PingBetweenFragments_WritesTheWholeMessageAndAnswersThePing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var output = new MemoryStream();
        var connection = new ScriptedConnection(Bytes(Head101 + "\x01\x0001a\x89\x00\x80\x0001b"));

        TransferResult result = await Handler(connection).ExecuteAsync(Context(output));

        string sent = Encoding.Latin1.GetString(connection.Sent);
        diagnostics.Act("result", Describe(result));
        diagnostics.Bytes("sent", connection.Sent);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("ab", Encoding.Latin1.GetString(output.ToArray()));
        StringAssert.EndsWith(sent, "\r\n\r\n" + EmptyPong);
    }

    [TestMethod]
    public async Task ExecuteAsync_EveryByteInItsOwnRead_MatchesTheWholeExchangeInOneRead()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] whole = Bytes(Head101 + MixedFrames);
        var expected = await RunAsync([whole]);

        var actual = await RunAsync([.. whole.Select(single => new[] { single })]);

        diagnostics.Act("whole", expected.Summary);
        diagnostics.Act("one byte per read", actual.Summary);
        Assert.AreEqual(CurlExitCode.Ok, expected.Result.ExitCode);
        Assert.AreEqual("hello" + Payload126 + "\x03\xe8", expected.Written);
        Assert.AreEqual(expected.Summary, actual.Summary);
    }

    [TestMethod]
    public async Task ExecuteAsync_ExchangeSplitAtEveryOffset_MatchesTheWholeExchange()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] whole = Bytes(Head101 + MixedFrames);
        var expected = await RunAsync([whole]);

        for (int offset = 1; offset < whole.Length; offset++)
        {
            var actual = await RunAsync([whole[..offset], whole[offset..]]);
            diagnostics.Act($"split at {offset}", actual.Summary);
            Assert.AreEqual(expected.Summary, actual.Summary, $"split at {offset}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_TooLongLengthSplitAtEveryOffset_FailsAlikeEveryTime()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] frames = Bytes("\x81\x02ok\x82\x7f\x80\x00\x00\x00\x00\x00\x00\x00");

        for (int offset = 1; offset < frames.Length; offset++)
        {
            var actual = await RunAsync([Bytes(Head101), frames[..offset], frames[offset..]]);
            diagnostics.Act($"split at {offset}", actual.Summary);
            Assert.AreEqual(CurlExitCode.RecvError, actual.Result.ExitCode, $"split at {offset}");
            Assert.AreEqual("[WS] frame length longer than 63 bits not supported", actual.Result.ErrorMessage, $"split at {offset}");
            Assert.AreEqual("ok", actual.Written, $"split at {offset}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_RandomlyChunkedExchange_MatchesTheWholeExchange()
    {
        const int Seed = 1521;
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("seed", Seed);
        var random = new Random(Seed);
        byte[] whole = Bytes(Head101 + MixedFrames);
        var expected = await RunAsync([whole]);

        for (int round = 0; round < 50; round++)
        {
            var actual = await RunAsync(RandomChunks(whole, random));
            Assert.AreEqual(expected.Summary, actual.Summary, $"seed {Seed}, round {round}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerClosesInsideAFrameHead_SucceedsWithWhatCameBefore()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var output = new MemoryStream();

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101 + "\x81\x02ok\x82\x7f\x00\x00"))).ExecuteAsync(Context(output));

        diagnostics.Act("result", Describe(result));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("ok", Encoding.Latin1.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_TokenCancelledBeforeTheCall_ThrowsOperationCanceledAndSendsNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var connection = new ScriptedConnection(Bytes(Head101 + "\x81\x02ok"));
        var context = new TransferContext { Url = CurlUrl.Parse("ws://h/"), Output = new MemoryStream(), CancellationToken = cancellation.Token };

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await Handler(connection).ExecuteAsync(context));

        diagnostics.Act("exception", exception.GetType().Name);
        Assert.IsEmpty(connection.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_TokenCancelledAfterTheTransfer_LeavesTheResultAlone()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var cancellation = new CancellationTokenSource();
        var output = new MemoryStream();
        var context = new TransferContext { Url = CurlUrl.Parse("ws://h/"), Output = output, CancellationToken = cancellation.Token };

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101 + "\x81\x02ok"))).ExecuteAsync(context);
        await cancellation.CancelAsync();

        diagnostics.Act("result", Describe(result));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("ok", Encoding.Latin1.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_SameHandlerAfterAViolation_RunsTheNextTransferCleanly()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var connector = new QueueConnector(
            new ScriptedConnection(Bytes(Head101 + "\x01\x0001a\x83\x00")),
            new ScriptedConnection(Bytes(Head101 + "\x80\x0001b")),
            new ScriptedConnection(Bytes(Head101 + "\x81\x02ok")));
        WsProtocolHandler handler = Handler(connector);
        var output = new MemoryStream();

        TransferResult violated = await handler.ExecuteAsync(Context());
        TransferResult continuation = await handler.ExecuteAsync(Context());
        TransferResult clean = await handler.ExecuteAsync(Context(output));

        diagnostics.Act("violated", Describe(violated));
        diagnostics.Act("continuation", Describe(continuation));
        diagnostics.Act("clean", Describe(clean));
        Assert.AreEqual("[WS] invalid opcode: 83", violated.ErrorMessage);
        Assert.AreEqual("[WS] no ongoing fragmented message to resume", continuation.ErrorMessage);
        Assert.AreEqual(CurlExitCode.Ok, clean.ExitCode);
        Assert.AreEqual("ok", Encoding.Latin1.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_ManyTransfersAtOnceOnOneHandler_EachMatchesASingleTransfer()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] whole = Bytes(Head101 + MixedFrames);
        WsProtocolHandler handler = Handler(new FreshConnectionConnector(whole));
        var expected = await RunAsync([whole]);

        var outputs = Enumerable.Range(0, 32).Select(_ => new MemoryStream()).ToArray();
        TransferResult[] results = await Task.WhenAll(outputs.Select(output => Task.Run(async () => await handler.ExecuteAsync(Context(output)))));

        diagnostics.Act("transfers", results.Length);
        for (int index = 0; index < results.Length; index++)
        {
            Assert.AreEqual(CurlExitCode.Ok, results[index].ExitCode, $"transfer {index}");
            Assert.AreEqual(expected.Written, Encoding.Latin1.GetString(outputs[index].ToArray()), $"transfer {index}");
        }
    }

    private static async Task<Outcome> RunAsync(byte[][] reads)
    {
        var output = new MemoryStream();
        var connection = new ScriptedConnection(reads);
        TransferResult result = await Handler(connection).ExecuteAsync(Context(output));
        return new Outcome(result, Encoding.Latin1.GetString(output.ToArray()), Encoding.Latin1.GetString(connection.Sent));
    }

    private static byte[][] RandomChunks(byte[] whole, Random random)
    {
        List<byte[]> chunks = [];
        int offset = 0;
        while (offset < whole.Length)
        {
            int length = Math.Min(random.Next(1, 20), whole.Length - offset);
            chunks.Add(whole[offset..(offset + length)]);
            offset += length;
        }

        return [.. chunks];
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);

    private static string Describe(TransferResult result) =>
        $"exit {(int)result.ExitCode} ({result.ExitCode}), error '{result.ErrorMessage}', bytes transferred {result.BytesTransferred}";

    private static WsProtocolHandler Handler(IConnection connection) => Handler(new RecordingConnector(ConnectResult.Connected(connection)));

    private static WsProtocolHandler Handler(IConnector connector) => new(connector, new RecordingAuthenticator(), new FixedRandomSource());

    private static TransferContext Context(Stream? output = null, IDiagnosticLog? log = null) =>
        log is null
            ? new() { Url = CurlUrl.Parse("ws://h/"), Output = output ?? new MemoryStream() }
            : new() { Url = CurlUrl.Parse("ws://h/"), Output = output ?? new MemoryStream(), DiagnosticLog = log };

    private sealed record Outcome(TransferResult Result, string Written, string Sent)
    {
        public string Summary =>
            $"{Describe(Result)}; download {Result.Report?.DownloadSize}; written {Convert.ToHexString(Encoding.Latin1.GetBytes(Written))}; sent {Convert.ToHexString(Encoding.Latin1.GetBytes(Sent))}";
    }

    private sealed class QueueConnector(params IConnection[] connections) : IConnector
    {
        private readonly Queue<IConnection> remaining = new(connections);

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Connected(remaining.Dequeue()));
    }
}
