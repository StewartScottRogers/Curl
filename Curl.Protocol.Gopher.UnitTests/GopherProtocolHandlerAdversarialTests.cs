using System.Collections.Concurrent;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Gopher.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Gopher;

/// <summary>
/// Adversarial black-box tests (BL-1509, by <c>Documentation/Wiki/Adversarial-Testing.md</c>):
/// <see cref="GopherProtocolHandler" /> attacked through its public surface and injected fakes
/// with selectors at their limits, selectors carrying CR, LF, tabs, NUL and bytes above
/// 0x7F, every item-type character and unknown ones, failed <c>gophers</c> handshakes,
/// malformed replies, and repeated, split, cancelled and concurrent transfers. Every selector
/// pinned here was measured against curl 8.21.0 (Schannel, Windows) with
/// <c>Record-CurlExchange.ps1</c> on 2026-10-07; the rest is the handler's documented contract.
/// </summary>
[TestClass]
public sealed class GopherProtocolHandlerAdversarialTests
{
    private const int ReadBufferSize = 16384;

    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    // Boundaries

    [TestMethod]
    public async Task ExecuteAsync_PathOfThreeBytes_SendsTheOneByteAfterTheItemType()
    {
        // Measured: gopher://127.0.0.1:17071/1x sent "x\r\n".
        await AssertSelectorSent("gopher://h/1x", "x\r\n"u8.ToArray());
    }

    [TestMethod]
    [DataRow("gopher://h/1%41", "A\r\n", DisplayName = "escape ending the path is decoded")]
    [DataRow("gopher://h/1%4", "%4\r\n", DisplayName = "escape one digit short is sent as written")]
    [DataRow("gopher://h/1%G1x", "%G1x\r\n", DisplayName = "escape with a non-hex digit is sent as written")]
    public async Task ExecuteAsync_EscapeAtTheEndOfThePath_IsDecodedOnlyWhenTwoHexDigitsFollow(string url, string expected)
    {
        // Measured: /1%41 sent "A\r\n", /1%4 sent "%4\r\n", /1%G1x sent "%G1x\r\n".
        await AssertSelectorSent(url, Encoding.ASCII.GetBytes(expected));
    }

    [TestMethod]
    public async Task ExecuteAsync_SelectorOf64KibBytes_IsSentWholeBeforeItsCrlf()
    {
        string selector = new('s', 65536);

        await AssertSelectorSent("gopher://h/1" + selector, Encoding.ASCII.GetBytes(selector + "\r\n"));
    }

    [TestMethod]
    public async Task ExecuteAsync_SelectorOf4096EncodedTabs_SendsEveryTabDecoded()
    {
        string path = string.Concat(Enumerable.Repeat("%09", 4096));

        await AssertSelectorSent("gopher://h/7" + path, [.. Enumerable.Repeat((byte)'\t', 4096), .. "\r\n"u8]);
    }

    [TestMethod]
    [DataRow(ReadBufferSize - 1)]
    [DataRow(ReadBufferSize)]
    [DataRow(ReadBufferSize + 1)]
    public async Task ExecuteAsync_ReplyAroundTheReadBufferSize_ReachesOutputWhole(int length)
    {
        byte[] reply = [.. Enumerable.Range(0, length).Select(index => (byte)(index % 251))];
        ScriptedConnection connection = new(ChunksOf(reply, ReadBufferSize));
        MemoryStream output = new();
        Diagnostics.Arrange("reply length", length);

        TransferResult result = await Handler(connection).ExecuteAsync(Context("gopher://h/0/x", output));
        Diagnostics.Act("result", DiagnosticText.Result(result));

        Diagnostics.Assert("result", TransferResult.Success(length), result);
        Assert.AreEqual(TransferResult.Success(length), result);
        CollectionAssert.AreEqual(reply, output.ToArray());
    }

    [TestMethod]
    [DataRow(1L, 2, 1L, CurlExitCode.FilesizeExceeded, DisplayName = "limit 1, reply 2: cut at 1, exit 63")]
    [DataRow(2L, 2, 2L, CurlExitCode.Ok, DisplayName = "limit 2, reply 2: whole, exit 0")]
    [DataRow(3L, 2, 2L, CurlExitCode.Ok, DisplayName = "limit 3, reply 2: whole, exit 0")]
    [DataRow(0L, 2, 2L, CurlExitCode.Ok, DisplayName = "limit 0 means no limit")]
    [DataRow(-1L, 2, 2L, CurlExitCode.Ok, DisplayName = "a negative limit means no limit")]
    public async Task ExecuteAsync_MaxFileSizeAroundTheReplyLength_CutsOnlyPastTheLimit(
        long maxFileSize,
        int replyLength,
        long expectedWritten,
        CurlExitCode expectedExit)
    {
        ScriptedConnection connection = new([.. Enumerable.Repeat((byte)'r', replyLength)]);
        MemoryStream output = new();
        TransferContext context = new() { Url = CurlUrl.Parse("gopher://h/0/x"), Output = output, MaxFileSize = maxFileSize };
        Diagnostics.Arrange("max file size", maxFileSize);
        Diagnostics.Arrange("reply length", replyLength);

        TransferResult result = await Handler(connection).ExecuteAsync(context);
        Diagnostics.Act("result", DiagnosticText.Result(result));

        Diagnostics.Assert("exit code", expectedExit, result.ExitCode);
        Diagnostics.Assert("bytes written", expectedWritten, output.Length);
        Assert.AreEqual(expectedExit, result.ExitCode);
        Assert.AreEqual(expectedWritten, result.BytesTransferred);
        Assert.AreEqual(expectedWritten, output.Length);
        if (expectedExit == CurlExitCode.FilesizeExceeded)
        {
            Assert.AreEqual($"Exceeded the maximum allowed file size ({maxFileSize}) with {expectedWritten} bytes", result.ErrorMessage);
        }
    }

    [TestMethod]
    [DataRow("gopher://h:65535/", 65535, false)]
    [DataRow("gopher://h:70/", 70, false)]
    [DataRow("gopher://h:1/", 1, false)]
    [DataRow("gophers://h:443/", 443, true)]
    [DataRow("gophers://h:65535/", 65535, true)]
    public async Task ExecuteAsync_PortAtItsLimits_ConnectsToThatPort(string url, int port, bool useTls)
    {
        FakeConnector connector = FakeConnector.For(new ScriptedConnection());
        Diagnostics.Arrange("url", url);

        await new GopherProtocolHandler(connector).ExecuteAsync(Context(url));
        ConnectTarget target = connector.Targets.Single();
        Diagnostics.Act("target", $"{target.Host}:{target.Port} tls={target.UseTls}");

        Diagnostics.Assert("port", port, target.Port);
        Assert.AreEqual(port, target.Port);
        Assert.AreEqual(useTls, target.UseTls);
    }

    // Malformed input

    [TestMethod]
    [DataRow("gopher://h/1a%0D%0Ab", new byte[] { 0x61, 0x0D, 0x0A, 0x62, 0x0D, 0x0A }, DisplayName = "CRLF in the middle")]
    [DataRow("gopher://h/1a%0Ab%0D", new byte[] { 0x61, 0x0A, 0x62, 0x0D, 0x0D, 0x0A }, DisplayName = "bare LF, then a CR before the CRLF")]
    [DataRow("gopher://h/1a?b%0d%0a#c%00", new byte[] { 0x61, 0x3F, 0x62, 0x0D, 0x0A, 0x0D, 0x0A }, DisplayName = "CRLF in the query, NUL in the fragment")]
    public async Task ExecuteAsync_EncodedCrAndLfInTheSelector_AreSentDecodedAsCurlSendsThem(string url, byte[] expected)
    {
        // Measured: curl 8.21.0 refuses only NUL in a gopher selector; CR and LF go out
        // decoded, and the fragment, NUL and all, is never sent.
        await AssertSelectorSent(url, expected);
    }

    [TestMethod]
    public async Task ExecuteAsync_SeveralEncodedTabs_SendsEachTabDecoded()
    {
        // Measured: /1a%09b%09c%09d sent "a\tb\tc\td\r\n".
        await AssertSelectorSent("gopher://h/1a%09b%09c%09d", "a\tb\tc\td\r\n"u8.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_EncodedNulInTheItemTypePosition_IsRemovedBeforeDecodingSoNothingIsRefused()
    {
        // Measured: /%00foo sent "00foo\r\n", as curl removes "/%" before decoding.
        await AssertSelectorSent("gopher://h/%00foo", "00foo\r\n"u8.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_EncodedPercentBeforeZeros_DecodesOnceAndSendsALiteralEscape()
    {
        // Measured: /1%2500 sent "%00\r\n", exit 0.
        await AssertSelectorSent("gopher://h/1%2500", "%00\r\n"u8.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_EncodedNulAsTheLastByte_ReturnsUrlMalformatAndSendsNothing()
    {
        // Measured: /1a%00 sent nothing and exited 3.
        ScriptedConnection connection = new("unread"u8.ToArray());
        Diagnostics.Arrange("url", "gopher://h/1a%00");

        TransferResult result = await Handler(connection).ExecuteAsync(Context("gopher://h/1a%00"));
        Diagnostics.Act("result", DiagnosticText.Result(result));

        Diagnostics.Assert("exit code", CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.IsEmpty(connection.Written);
        Assert.AreEqual(0, connection.ReadCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_EncodedBytesAbove0x7F_AreSentAsRawBytes()
    {
        // Measured: /1%FF%fe sent FF FE 0D 0A.
        await AssertSelectorSent("gopher://h/1%FF%fe", [0xFF, 0xFE, 0x0D, 0x0A]);
    }

    [TestMethod]
    public async Task ExecuteAsync_EncodedDotsAfterTheItemType_AreNotDotSegments()
    {
        // Measured: /i%2E%2E/x sent "../x\r\n".
        await AssertSelectorSent("gopher://h/i%2E%2E/x", "../x\r\n"u8.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplyOfEveryByteValueWithNoTerminatorLine_ReachesOutputUnaltered()
    {
        byte[] reply = [.. Enumerable.Range(0, 256).Select(value => (byte)value), .. "\n\r\0.\n"u8];
        ScriptedConnection connection = new(reply);
        MemoryStream output = new();
        Diagnostics.Arrange("reply", "bytes 0x00 to 0xFF, then a bare LF, a CR, a NUL and a dot line with no CR");

        TransferResult result = await Handler(connection).ExecuteAsync(Context("gopher://h/1", output));
        Diagnostics.Act("result", DiagnosticText.Result(result));

        Diagnostics.Assert("result", TransferResult.Success(reply.Length), result);
        Assert.AreEqual(TransferResult.Success(reply.Length), result);
        CollectionAssert.AreEqual(reply, output.ToArray());
    }

    // Invalid partitions

    [TestMethod]
    [DataRow('0')]
    [DataRow('1')]
    [DataRow('2')]
    [DataRow('3')]
    [DataRow('4')]
    [DataRow('5')]
    [DataRow('6')]
    [DataRow('7')]
    [DataRow('8')]
    [DataRow('9')]
    [DataRow('+')]
    [DataRow('g')]
    [DataRow('I')]
    [DataRow('T')]
    [DataRow('h')]
    [DataRow('i')]
    [DataRow('s')]
    [DataRow('d')]
    [DataRow('Z', DisplayName = "unknown item type Z")]
    [DataRow('~', DisplayName = "unknown item type ~")]
    [DataRow('-', DisplayName = "unknown item type -")]
    public async Task ExecuteAsync_AnyItemTypeCharacterKnownOrNot_IsRemovedWithoutBeingInterpreted(char itemType)
    {
        // Measured: /Zfoo sent "foo\r\n", as /1foo does; curl never reads the item type.
        await AssertSelectorSent($"gopher://h/{itemType}foo", "foo\r\n"u8.ToArray());
    }

    [TestMethod]
    [DataRow("GOPHER://h/1abc", false, DisplayName = "upper-case gopher")]
    [DataRow("GoPhErS://h/1abc", true, DisplayName = "mixed-case gophers")]
    public async Task ExecuteAsync_SchemeInUpperOrMixedCase_IsServedAsItsLowerCaseScheme(string url, bool useTls)
    {
        // Measured: GOPHER://127.0.0.1:17071/1abc sent "abc\r\n", exit 0.
        ScriptedConnection connection = new();
        FakeConnector connector = FakeConnector.For(connection);
        Diagnostics.Arrange("url", url);

        TransferResult result = await new GopherProtocolHandler(connector).ExecuteAsync(Context(url));
        Diagnostics.Act("result", DiagnosticText.Result(result));

        Diagnostics.Assert("tls", useTls, connector.Targets.Single().UseTls);
        Assert.AreEqual(TransferResult.Success(0), result);
        Assert.AreEqual(useTls, connector.Targets.Single().UseTls);
        Assert.AreEqual(70, connector.Targets.Single().Port);
        CollectionAssert.AreEqual("abc\r\n"u8.ToArray(), connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_GophersHandshakeFails_ReturnsTheConnectorsExit35UnchangedAndReportsNothing()
    {
        const string Message = "schannel: failed to receive handshake, SSL/TLS connection failed";
        FakeConnector connector = new(ConnectResult.Failed(CurlExitCode.SslConnectError, Message));
        TranscriptTransferEvents events = new();
        RecordingProgress progress = new();
        MemoryStream output = new();
        TransferContext context = new() { Url = CurlUrl.Parse("gophers://h/1x"), Output = output, Events = events, Progress = progress };
        Diagnostics.Arrange("connect result", "exit 35, " + Message);

        TransferResult result = await new GopherProtocolHandler(connector).ExecuteAsync(context);
        Diagnostics.Act("result", DiagnosticText.Result(result));
        Diagnostics.Act("transcript", DiagnosticText.Lines(events.Transcript));

        Diagnostics.Assert("result", new TransferResult(CurlExitCode.SslConnectError, 0, Message), result);
        Assert.AreEqual(new TransferResult(CurlExitCode.SslConnectError, 0, Message), result);
        Assert.IsTrue(connector.Targets.Single().UseTls);
        Assert.IsEmpty(events.Transcript);
        Assert.IsEmpty(progress.Reports);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_GophersEndsWithoutCloseNotifyAfterPartialReply_KeepsTheBytesAndReturnsExit56()
    {
        const string Message = "schannel: server closed abruptly (missing close_notify)";
        ScriptedConnection connection = new("par"u8.ToArray(), "tial"u8.ToArray(), null)
        {
            ReadFailure = new MissingCloseNotifyException(Message),
        };
        MemoryStream output = new();
        Diagnostics.Arrange("scripted reads", "\"par\", \"tial\", then a read without close_notify");

        TransferResult result = await Handler(connection).ExecuteAsync(Context("gophers://h/1x", output));
        Diagnostics.Act("result", DiagnosticText.Result(result));

        Diagnostics.Assert("result", new TransferResult(CurlExitCode.RecvError, 7, Message), result);
        Assert.AreEqual(new TransferResult(CurlExitCode.RecvError, 7, Message), result);
        CollectionAssert.AreEqual("partial"u8.ToArray(), output.ToArray());
        Assert.IsTrue(connection.IsDisposed);
    }

    // State and concurrency

    [TestMethod]
    public async Task ExecuteAsync_ReplyDeliveredOneBytePerRead_ReachesOutputWholeWithOneDownloadReportPerByte()
    {
        byte[] reply = "0About\tfake\t(NULL)\t0\r\n.\r\n"u8.ToArray();
        ScriptedConnection connection = new([.. reply.Select(value => new[] { value })]);
        MemoryStream output = new();
        RecordingProgress progress = new();
        TransferContext context = new() { Url = CurlUrl.Parse("gopher://h/1"), Output = output, Progress = progress };
        Diagnostics.Arrange("scripted reads", $"{reply.Length} reads of one byte each");

        TransferResult result = await Handler(connection).ExecuteAsync(context);
        Diagnostics.Act("result", DiagnosticText.Result(result));

        Diagnostics.Assert("result", TransferResult.Success(reply.Length), result);
        Assert.AreEqual(TransferResult.Success(reply.Length), result);
        CollectionAssert.AreEqual(reply, output.ToArray());
        Assert.HasCount(reply.Length + 1, progress.Reports);
        Assert.AreEqual($"downloaded {reply.Length}", progress.Reports[^1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_ZeroByteReadBeforeMoreData_EndsTheTransferThereAndReadsNoFurther()
    {
        ScriptedConnection connection = new("ab"u8.ToArray(), [], "late"u8.ToArray());
        MemoryStream output = new();
        Diagnostics.Arrange("scripted reads", "\"ab\", a zero-byte read, then \"late\"");

        TransferResult result = await Handler(connection).ExecuteAsync(Context("gopher://h/1", output));
        Diagnostics.Act("result", DiagnosticText.Result(result));

        Diagnostics.Assert("result", TransferResult.Success(2), result);
        Assert.AreEqual(TransferResult.Success(2), result);
        CollectionAssert.AreEqual("ab"u8.ToArray(), output.ToArray());
        Assert.AreEqual(2, connection.ReadCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledWhileTheReplyIsBeingWritten_ThrowsAndDisposesTheConnection()
    {
        using CancellationTokenSource cancellation = new();
        ScriptedConnection connection = new("first"u8.ToArray(), "second"u8.ToArray());
        CancellingOnWriteStream output = new(cancellation);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("gopher://h/1"),
            Output = output,
            CancellationToken = cancellation.Token,
        };
        Diagnostics.Arrange("output", "a stream that cancels the transfer once it has taken a write");

        Exception thrown = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await Handler(connection).ExecuteAsync(context));
        Diagnostics.Act("thrown", thrown.GetType().Name + ": " + thrown.Message);

        Diagnostics.Assert("connection disposed", true, connection.IsDisposed);
        Assert.IsTrue(connection.IsDisposed);
        CollectionAssert.AreEqual("first"u8.ToArray(), output.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_TokenCancelledAfterTheTransferFinished_LeavesItsResultUntouched()
    {
        using CancellationTokenSource cancellation = new();
        ScriptedConnection connection = new("done"u8.ToArray());
        MemoryStream output = new();
        TransferContext context = new() { Url = CurlUrl.Parse("gopher://h/1"), Output = output, CancellationToken = cancellation.Token };

        TransferResult result = await Handler(connection).ExecuteAsync(context);
        await cancellation.CancelAsync();
        Diagnostics.Act("result", DiagnosticText.Result(result));

        Assert.AreEqual(TransferResult.Success(4), result);
        CollectionAssert.AreEqual("done"u8.ToArray(), output.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_SameHandlerAfterARefusedSelector_ServesTheNextTransferNormally()
    {
        ConnectionPerCallConnector connector = new(() => new ScriptedConnection("ok"u8.ToArray()));
        GopherProtocolHandler handler = new(connector);
        MemoryStream output = new();
        Diagnostics.Arrange("transfers", "gopher://h/1%00 then gopher://h/1next on one handler");

        TransferResult refused = await handler.ExecuteAsync(Context("gopher://h/1%00"));
        TransferResult served = await handler.ExecuteAsync(Context("gopher://h/1next", output));
        Diagnostics.Act("results", DiagnosticText.Result(refused) + " | " + DiagnosticText.Result(served));

        Assert.AreEqual(CurlExitCode.UrlMalformat, refused.ExitCode);
        Assert.AreEqual(TransferResult.Success(2), served);
        Assert.IsEmpty(connector.Connections[0].Written);
        CollectionAssert.AreEqual("next\r\n"u8.ToArray(), connector.Connections[1].Written);
        CollectionAssert.AreEqual("ok"u8.ToArray(), output.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_SixtyFourConcurrentTransfersOnOneHandler_EachSendsItsOwnSelectorAndGetsTheWholeReply()
    {
        const int Count = 64;
        byte[] reply = "iHello\tfake\t(NULL)\t0\r\n.\r\n"u8.ToArray();
        ConnectionPerCallConnector connector = new(() => new ScriptedConnection(reply[..5], reply[5..]));
        GopherProtocolHandler handler = new(connector);
        MemoryStream[] outputs = [.. Enumerable.Range(0, Count).Select(_ => new MemoryStream())];
        Diagnostics.Arrange("transfers", $"{Count} at once on one handler, gopher://h/1sel<n>");

        TransferResult[] results = await Task.WhenAll(Enumerable.Range(0, Count).Select(
            index => Task.Run(async () => await handler.ExecuteAsync(Context($"gopher://h/1sel{index}", outputs[index])))));
        Diagnostics.Act("distinct results", DiagnosticText.Lines(results.Select(DiagnosticText.Result).Distinct()));

        string[] expectedSelectors = [.. Enumerable.Range(0, Count).Select(index => $"sel{index}\r\n").Order(StringComparer.Ordinal)];
        string[] sentSelectors = [.. connector.Connections.Select(connection => Encoding.ASCII.GetString(connection.Written)).Order(StringComparer.Ordinal)];
        Diagnostics.Assert("connections", Count, connector.Connections.Count);
        Assert.IsTrue(results.All(result => result == TransferResult.Success(reply.Length)));
        CollectionAssert.AreEqual(expectedSelectors, sentSelectors);
        Assert.IsTrue(outputs.All(output => output.ToArray().AsSpan().SequenceEqual(reply)));
        Assert.IsTrue(connector.Connections.All(connection => connection.IsDisposed));
    }

    private static GopherProtocolHandler Handler(ScriptedConnection connection) => new(FakeConnector.For(connection));

    private static TransferContext Context(string url, Stream? output = null) =>
        new() { Url = CurlUrl.Parse(url), Output = output ?? new MemoryStream() };

    private static byte[][] ChunksOf(byte[] bytes, int size) =>
        [.. Enumerable.Range(0, (bytes.Length + size - 1) / size).Select(index => bytes[(index * size)..Math.Min(bytes.Length, (index + 1) * size)])];

    private async Task AssertSelectorSent(string url, byte[] expected)
    {
        ScriptedConnection connection = new();
        Diagnostics.Arrange("url", url.Length > 200 ? url[..200] + "..." : url);

        TransferResult result = await Handler(connection).ExecuteAsync(Context(url));
        Diagnostics.Act("result", DiagnosticText.Result(result));

        Diagnostics.Assert("result", TransferResult.Success(0), result);
        Diagnostics.Diff("selector sent", expected, connection.Written);
        Assert.AreEqual(TransferResult.Success(0), result);
        CollectionAssert.AreEqual(expected, connection.Written);
    }

    /// <summary>
    /// An <see cref="IConnector" /> that hands each connect a new connection and keeps them
    /// all, so concurrent transfers on one handler never share one.
    /// </summary>
    private sealed class ConnectionPerCallConnector(Func<ScriptedConnection> create) : IConnector
    {
        private readonly ConcurrentQueue<ScriptedConnection> connections = new();

        public List<ScriptedConnection> Connections => [.. connections];

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            ScriptedConnection connection = create();
            connections.Enqueue(connection);
            return ValueTask.FromResult(ConnectResult.Connected(connection));
        }
    }

    /// <summary>
    /// An output stream that takes each write and then cancels the transfer, so the next
    /// read sees a cancelled token.
    /// </summary>
    private sealed class CancellingOnWriteStream(CancellationTokenSource cancellation) : MemoryStream
    {
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await base.WriteAsync(buffer, cancellationToken);
            await cancellation.CancelAsync();
        }
    }

    private sealed class RecordingProgress : ITransferProgress
    {
        public List<string> Reports { get; } = [];

        public void ReportTransferStarted() => Reports.Add("started");

        public void ReportDownloaded(long bytesSoFar, long? expectedTotal) => Reports.Add($"downloaded {bytesSoFar}");

        public void ReportUploaded(long bytesSoFar, long? expectedTotal) => Reports.Add($"uploaded {bytesSoFar}");
    }
}
