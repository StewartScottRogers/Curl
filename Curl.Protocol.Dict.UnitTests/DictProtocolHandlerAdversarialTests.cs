using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Dict.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Dict;

/// <summary>
/// Adversarial black-box tests (BL-1506, by <c>Documentation/Wiki/Adversarial-Testing.md</c>):
/// <see cref="DictProtocolHandler" /> attacked through its public surface and injected fakes
/// with lookup paths at their limits, malformed and invalid paths, malformed server replies,
/// and repeated, split and concurrent transfers. The oracle is the handler's documented
/// contract, which pins curl 8.21.0's measured behaviour.
/// </summary>
[TestClass]
public sealed class DictProtocolHandlerAdversarialTests
{
    private const string Client = "CLIENT libcurl 8.21.0\r\n";

    private const string Quit = "QUIT\r\n";

    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    // Boundaries

    [TestMethod]
    [DataRow("/d:a%20b", "DEFINE ! a\\ b")]
    [DataRow("/d:a%21b", "DEFINE ! a!b")]
    [DataRow("/d:a%7Eb", "DEFINE ! a~b")]
    [DataRow("/d:a%1Fb", null)]
    public async Task ExecuteAsync_WordByteAtTheEscapeBoundaries_IsEscapedOnlyAtOrBelowSpace(string path, string? command)
    {
        (TransferResult result, byte[] sent, _) = await TransferAsync(path);

        if (command is null)
        {
            Diagnostics.Assert("exit code", CurlExitCode.UrlMalformat, result.ExitCode);
            Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
            Assert.IsEmpty(sent);
            return;
        }

        string expected = Client + command + "\r\n" + Quit;
        Diagnostics.Diff("request sent", expected, Encoding.Latin1.GetString(sent));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(expected, Encoding.Latin1.GetString(sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_WordOf64KibLetters_IsSentWholeInOneCommandLine()
    {
        string word = new('a', 65536);

        (TransferResult result, byte[] sent, _) = await TransferAsync("/d:" + word);

        string expected = Client + "DEFINE ! " + word + "\r\n" + Quit;
        Diagnostics.Assert("request length", expected.Length, sent.Length);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(expected, Encoding.Latin1.GetString(sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_WordOf4096EncodedSpaces_SendsEverySpaceWithABackslash()
    {
        string path = "/m:" + string.Concat(Enumerable.Repeat("%20", 4096));

        (TransferResult result, byte[] sent, _) = await TransferAsync(path);

        string expected = Client + "MATCH ! . " + string.Concat(Enumerable.Repeat("\\ ", 4096)) + "\r\n" + Quit;
        Diagnostics.Assert("request length", expected.Length, sent.Length);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(expected, Encoding.Latin1.GetString(sent));
    }

    [TestMethod]
    [DataRow("/d::::::", "DEFINE ! default")]
    [DataRow("/m:::", "MATCH ! . default")]
    [DataRow("/find::db", "MATCH db . default")]
    [DataRow("/lookup:::", "DEFINE ! default")]
    public async Task ExecuteAsync_LookupWithOnlyColons_SendsTheDefaultsAndReportsTheWordMissing(string path, string command)
    {
        var events = new TranscriptTransferEvents();

        (TransferResult result, byte[] sent, _) = await TransferAsync(path, events: events);

        string expected = Client + command + "\r\n" + Quit;
        Diagnostics.Diff("request sent", expected, Encoding.Latin1.GetString(sent));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(expected, Encoding.Latin1.GetString(sent));
        Assert.Contains("* lookup word is missing", events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReadFillingTheWholeReadBuffer_IsWrittenByteForByte()
    {
        byte[] reply = Pattern(16384, seed: 1506);

        (TransferResult result, _, byte[] output) = await TransferAsync("/d:x", reads: [reply]);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(16384L, result.BytesTransferred);
        CollectionAssert.AreEqual(reply, output);
    }

    [TestMethod]
    [DataRow(-1L)]
    [DataRow(long.MinValue)]
    [DataRow(0L)]
    public async Task ExecuteAsync_MaxFileSizeZeroOrNegative_IsNoLimit(long maxFileSize)
    {
        byte[] reply = "220 hello\r\n221 bye\r\n"u8.ToArray();

        (TransferResult result, _, byte[] output) = await TransferAsync("/d:x", reads: [reply], maxFileSize: maxFileSize);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(reply, output);
    }

    [TestMethod]
    public async Task ExecuteAsync_MaxFileSizeOfLongMaxValue_WritesTheWholeReplyWithoutOverflowing()
    {
        byte[] reply = Pattern(40000, seed: 7);

        (TransferResult result, _, byte[] output) = await TransferAsync(
            "/d:x", reads: [reply[..16384], reply[16384..32768], reply[32768..]], maxFileSize: long.MaxValue);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(40000L, result.BytesTransferred);
        CollectionAssert.AreEqual(reply, output);
    }

    [TestMethod]
    public async Task ExecuteAsync_MaxFileSizeOfOneByte_WritesOneByteAndExits63()
    {
        (TransferResult result, _, byte[] output) = await TransferAsync("/d:x", reads: ["220 hi\r\n"u8.ToArray()], maxFileSize: 1);

        Diagnostics.Act("result", DiagnosticText.Result(result));
        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual("Exceeded the maximum allowed file size (1) with 1 bytes", result.ErrorMessage);
        CollectionAssert.AreEqual("2"u8.ToArray(), output);
    }

    // Malformed input

    [TestMethod]
    [DataRow("/d:word%0D%0AQUIT")]
    [DataRow("/m:a%0Ab")]
    [DataRow("/find:%0A")]
    [DataRow("/d:w:db%0D%0ASHOW")]
    [DataRow("/m:w:db:s%0A")]
    [DataRow("/show%0D%0Aserver")]
    [DataRow("/d:a%09b")]
    [DataRow("/%0D")]
    public async Task ExecuteAsync_CrLfOrTabInjectedInAnyField_IsRefusedWithExit3AndNothingReachesTheWire(string path)
    {
        (TransferResult result, byte[] sent, byte[] output) = await TransferAsync(path, reads: ["220 hello\r\n"u8.ToArray()]);

        Diagnostics.Act("result", DiagnosticText.Result(result));
        Diagnostics.Bytes("request sent", sent);
        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.IsEmpty(sent);
        Assert.IsEmpty(output);
    }

    [TestMethod]
    [DataRow("/d:a%4", "DEFINE ! a%4")]
    [DataRow("/d:%%41", "DEFINE ! %A")]
    [DataRow("/d:%G1", "DEFINE ! %G1")]
    [DataRow("/d:%", "DEFINE ! %")]
    [DataRow("/d:a%3a%3Ab", "DEFINE ! a")]
    public async Task ExecuteAsync_TruncatedOrBrokenPercentEscape_IsSentAsWrittenAndValidEscapesAreDecoded(string path, string command)
    {
        (TransferResult result, byte[] sent, _) = await TransferAsync(path);

        string expected = Client + command + "\r\n" + Quit;
        Diagnostics.Diff("request sent", expected, Encoding.Latin1.GetString(sent));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(expected, Encoding.Latin1.GetString(sent));
    }

    [TestMethod]
    [DataRow("/%64:word", "DEFINE ! word")]
    [DataRow("/%6D%3Aword", "MATCH ! . word")]
    [DataRow("/d%3Aword", "DEFINE ! word")]
    public async Task ExecuteAsync_PrefixWrittenWithPercentEscapes_IsRecognisedAfterDecoding(string path, string command)
    {
        (TransferResult result, byte[] sent, _) = await TransferAsync(path);

        string expected = Client + command + "\r\n" + Quit;
        Diagnostics.Diff("request sent", expected, Encoding.Latin1.GetString(sent));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(expected, Encoding.Latin1.GetString(sent));
    }

    [TestMethod]
    [DataRow("/show%7F", new byte[] { (byte)'s', (byte)'h', (byte)'o', (byte)'w', 0x7F })]
    [DataRow("/x%FF", new byte[] { (byte)'x', 0xFF })]
    public async Task ExecuteAsync_DelOrHighByteInAPlainCommand_IsSentUnescaped(string path, byte[] command)
    {
        (TransferResult result, byte[] sent, _) = await TransferAsync(path);

        byte[] expected = [.. Encoding.ASCII.GetBytes(Client), .. command, (byte)'\r', (byte)'\n', .. Encoding.ASCII.GetBytes(Quit)];
        Diagnostics.Diff("request sent", expected, sent);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(expected, sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplyOfBinaryBytesIncludingNul_IsWrittenByteForByte()
    {
        byte[] reply = [0x00, 0xFF, 0x0D, 0x00, 0x0A, 0x80, 0x2E, 0x0D, 0x0A];

        (TransferResult result, _, byte[] output) = await TransferAsync("/d:x", reads: [reply]);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(reply, output);
    }

    // Invalid partitions: server replies outside the DICT grammar are passed through, never judged.

    [TestMethod]
    [DataRow("999 out of range\r\n")]
    [DataRow("099 below range\r\n")]
    [DataRow("abc not a code\r\n")]
    [DataRow("22\r\n")]
    [DataRow("220")]
    [DataRow("\r\n\r\n")]
    [DataRow("552 no match\r\n")]
    [DataRow("150 1 definitions retrieved\r\n151 \"w\" db \"Db\"\r\nunterminated text block with no dot line")]
    [DataRow("250 ok\r\n150 1 found\r\n")]
    public async Task ExecuteAsync_MalformedMissingOrOutOfOrderStatus_IsWrittenUnalteredWithExit0(string reply)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(reply);

        (TransferResult result, _, byte[] output) = await TransferAsync("/d:x", reads: [bytes]);

        Diagnostics.Act("result", DiagnosticText.Result(result));
        Diagnostics.Diff("output", bytes, output);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual((long)bytes.Length, result.BytesTransferred);
        CollectionAssert.AreEqual(bytes, output);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnknownCommandInThePath_IsSentAsTheCommandWithoutJudgingIt()
    {
        (TransferResult result, byte[] sent, _) = await TransferAsync("/NOTACOMMAND:x:y:z");

        string expected = Client + "NOTACOMMAND x y z\r\n" + Quit;
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(expected, Encoding.Latin1.GetString(sent));
    }

    // State and concurrency

    [TestMethod]
    public async Task ExecuteAsync_ReplyDeliveredOneBytePerRead_IsWrittenWholeAndCountedPerByte()
    {
        byte[] reply = "220 hello\r\n150 1 found\r\n.\r\n250 ok\r\n221 bye\r\n"u8.ToArray();
        byte[][] reads = [.. reply.Select(static value => new[] { value })];

        (TransferResult result, _, byte[] output) = await TransferAsync("/d:x", reads: reads);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual((long)reply.Length, result.BytesTransferred);
        CollectionAssert.AreEqual(reply, output);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplySplitAtEveryOffset_IsAlwaysWrittenWhole()
    {
        byte[] reply = "220 hello\r\n552 no match\r\n221 bye\r\n"u8.ToArray();

        for (int split = 1; split < reply.Length; split++)
        {
            (TransferResult result, _, byte[] output) = await TransferAsync("/d:x", reads: [reply[..split], reply[split..]]);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"split at {split}");
            CollectionAssert.AreEqual(reply, output, $"split at {split}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_OneHandlerRunTwice_GivesTheSecondTransferNothingFromTheFirst()
    {
        var connector = new QueueConnector();
        var handler = new DictProtocolHandler(connector);
        var first = new ScriptedConnection("first\r\n"u8.ToArray());
        var second = new ScriptedConnection("second\r\n"u8.ToArray());
        connector.Connections.Enqueue(first);
        connector.Connections.Enqueue(second);
        var firstOutput = new MemoryStream();
        var secondOutput = new MemoryStream();

        TransferResult firstResult = await handler.ExecuteAsync(Context("dict://h/d:one", firstOutput));
        TransferResult secondResult = await handler.ExecuteAsync(Context("dict://h/m:two", secondOutput));

        Assert.AreEqual(CurlExitCode.Ok, firstResult.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, secondResult.ExitCode);
        Assert.AreEqual(Client + "DEFINE ! one\r\n" + Quit, Encoding.ASCII.GetString(first.Sent));
        Assert.AreEqual(Client + "MATCH ! . two\r\n" + Quit, Encoding.ASCII.GetString(second.Sent));
        Assert.AreEqual("first\r\n", Encoding.ASCII.GetString(firstOutput.ToArray()));
        Assert.AreEqual("second\r\n", Encoding.ASCII.GetString(secondOutput.ToArray()));
        Assert.AreEqual(8L, secondResult.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusedPathThenValidPath_OnOneHandler_SecondTransferSucceeds()
    {
        var connector = new QueueConnector();
        var handler = new DictProtocolHandler(connector);
        var refused = new ScriptedConnection();
        var accepted = new ScriptedConnection("220 ok\r\n"u8.ToArray());
        connector.Connections.Enqueue(refused);
        connector.Connections.Enqueue(accepted);
        var output = new MemoryStream();

        TransferResult first = await handler.ExecuteAsync(Context("dict://h/d:a%0Ab", new MemoryStream()));
        TransferResult second = await handler.ExecuteAsync(Context("dict://h/d:ab", output));

        Assert.AreEqual(CurlExitCode.UrlMalformat, first.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, second.ExitCode);
        Assert.AreEqual(Client + "DEFINE ! ab\r\n" + Quit, Encoding.ASCII.GetString(accepted.Sent));
        Assert.AreEqual("220 ok\r\n", Encoding.ASCII.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_SixtyFourTransfersAtOnceOnOneHandler_EachSendsAndWritesOnlyItsOwnBytes()
    {
        const int Count = 64;
        var connections = new ScriptedConnection[Count];
        for (int index = 0; index < Count; index++)
        {
            byte[] reply = Encoding.ASCII.GetBytes($"150 {index}\r\n");
            connections[index] = new ScriptedConnection(reply[..2], reply[2..]);
        }

        var handler = new DictProtocolHandler(new ConnectorByHost(connections));
        var outputs = new MemoryStream[Count];
        var transfers = new Task<TransferResult>[Count];
        for (int index = 0; index < Count; index++)
        {
            int captured = index;
            outputs[captured] = new MemoryStream();
            transfers[captured] = Task.Run(async () =>
                await handler.ExecuteAsync(Context($"dict://h{captured}/d:w{captured}", outputs[captured])));
        }

        TransferResult[] results = await Task.WhenAll(transfers);

        for (int index = 0; index < Count; index++)
        {
            Assert.AreEqual(CurlExitCode.Ok, results[index].ExitCode, $"transfer {index}");
            Assert.AreEqual(Client + $"DEFINE ! w{index}\r\n" + Quit, Encoding.ASCII.GetString(connections[index].Sent), $"transfer {index}");
            Assert.AreEqual($"150 {index}\r\n", Encoding.ASCII.GetString(outputs[index].ToArray()), $"transfer {index}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledDuringTheReplyRead_ThrowsOperationCanceledAndDisposesTheConnection()
    {
        using var cancellation = new CancellationTokenSource();
        var connection = new CancellingConnection(cancellation, "220 hello\r\n"u8.ToArray());
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("dict://h/d:x"),
            Output = new MemoryStream(),
            CancellationToken = cancellation.Token,
        };

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await new DictProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection))).ExecuteAsync(context));

        Diagnostics.Act("connection disposed", connection.IsDisposed);
        Assert.IsTrue(connection.IsDisposed);
        Assert.AreEqual(0L, context.Output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_TokenCancelledAfterTheTransferFinished_LeavesTheResultAndOutputAsTheyWere()
    {
        using var cancellation = new CancellationTokenSource();
        var output = new MemoryStream();
        var context = new TransferContext { Url = CurlUrl.Parse("dict://h/d:x"), Output = output, CancellationToken = cancellation.Token };

        TransferResult result = await new DictProtocolHandler(new RecordingConnector(ConnectResult.Connected(new ScriptedConnection("220 ok\r\n"u8.ToArray()))))
            .ExecuteAsync(context);
        await cancellation.CancelAsync();

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("220 ok\r\n", Encoding.ASCII.GetString(output.ToArray()));
    }

    private static byte[] Pattern(int length, int seed)
    {
        var bytes = new byte[length];
        new Random(seed).NextBytes(bytes);
        return bytes;
    }

    private static TransferContext Context(string url, Stream output) =>
        new() { Url = CurlUrl.Parse(url), Output = output };

    private async Task<(TransferResult Result, byte[] Sent, byte[] Output)> TransferAsync(
        string path,
        byte[][]? reads = null,
        long? maxFileSize = null,
        ITransferEvents? events = null)
    {
        var connection = new ScriptedConnection(reads ?? []);
        var output = new MemoryStream();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("dict://h" + path),
            Output = output,
            MaxFileSize = maxFileSize,
            Events = events ?? new IgnoringTransferEvents(),
        };
        Diagnostics.Arrange("URL", "dict://h" + (path.Length > 200 ? path[..200] + "..." : path));

        TransferResult result = await new DictProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection))).ExecuteAsync(context);
        Diagnostics.Act("result", DiagnosticText.Result(result));

        return (result, connection.Sent, output.ToArray());
    }

    /// <summary>Hands out a fresh queued connection on every connect.</summary>
    private sealed class QueueConnector : IConnector
    {
        public Queue<IConnection> Connections { get; } = new();

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Connected(Connections.Dequeue()));
    }

    /// <summary>Connects host <c>h&lt;n&gt;</c> to connection <c>n</c>, safely from many tasks at once.</summary>
    private sealed class ConnectorByHost(ScriptedConnection[] connections) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Connected(connections[int.Parse(target.Host[1..], System.Globalization.CultureInfo.InvariantCulture)]));
    }

    /// <summary>Cancels the transfer's token while returning its one read.</summary>
    private sealed class CancellingConnection(CancellationTokenSource cancellation, byte[] read) : IConnection
    {
        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public bool IsDisposed { get; private set; }

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            await cancellation.CancelAsync();
            read.CopyTo(buffer);
            return read.Length;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
