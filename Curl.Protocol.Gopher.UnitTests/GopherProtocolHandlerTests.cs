using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Gopher.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Gopher;

/// <summary>
/// Pins <c>gopher://</c> and <c>gophers://</c> against curl 8.21.0: the selector bytes each
/// URL sends, the target each connects to, the reply copied to the output, and the exit
/// code and message of every way a transfer ends. Each exchange is replayed through
/// <see cref="ScriptedConnection" />, so nothing here touches a network.
/// </summary>
[TestClass]
public sealed class GopherProtocolHandlerTests
{
    /// <summary>The reply the measured server sent: one info line and the terminator.</summary>
    private static readonly byte[] MeasuredReply = "iHello\tfake\t(NULL)\t0\r\n.\r\n"u8.ToArray();

    /// <summary>The line end each measured server line closed with.</summary>
    private static readonly byte[] CarriageReturnLineFeed = "\r\n"u8.ToArray();

    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void SupportedSchemes_IsExactlyGopherAndGophers()
    {
        GopherProtocolHandler handler = new(FakeConnector.For(new ScriptedConnection()));
        Diagnostics.Arrange("handler", "a GopherProtocolHandler over a fake connector");

        string schemes = string.Join(", ", handler.SupportedSchemes);
        Diagnostics.Act("supported schemes", schemes);

        Diagnostics.Diff("supported schemes", "gopher, gophers", schemes);
        CollectionAssert.AreEqual(new[] { "gopher", "gophers" }, handler.SupportedSchemes.ToArray());
    }

    [TestMethod]
    public void Constructor_NullConnector_Throws()
    {
        Diagnostics.Arrange("connector", "null");

        Exception thrown = Assert.ThrowsExactly<ArgumentNullException>(() => new GopherProtocolHandler(null!));
        Diagnostics.Act("thrown", thrown.GetType().Name + ": " + thrown.Message);

        Diagnostics.Assert("exception type", nameof(ArgumentNullException), thrown.GetType().Name);
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws()
    {
        GopherProtocolHandler handler = new(FakeConnector.For(new ScriptedConnection()));
        Diagnostics.Arrange("context", "null");

        Exception thrown = await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await handler.ExecuteAsync(null!));
        Diagnostics.Act("thrown", thrown.GetType().Name + ": " + thrown.Message);

        Diagnostics.Assert("exception type", nameof(ArgumentNullException), thrown.GetType().Name);
    }

    [TestMethod]
    public async Task ExecuteAsync_RootPath_SendsEmptySelector()
    {
        await AssertSelectorSent("gopher://h/", "\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_ItemTypeOnly_SendsEmptySelector()
    {
        await AssertSelectorSent("gopher://h/1", "\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_ItemTypeAndPath_SendsPathWithoutItemType()
    {
        await AssertSelectorSent("gopher://h/1/foo", "/foo\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_EncodedTab_SendsDecodedTab()
    {
        await AssertSelectorSent("gopher://h/0/a%09b", "/a\tb\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_SearchWithEncodedTabAndSpace_SendsDecodedSearch()
    {
        await AssertSelectorSent("gopher://h/7/search%09term%20x", "/search\tterm x\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_Query_SendsQueryAfterPathAndNoFragment()
    {
        // Measured: gopher://127.0.0.1:17070/0/a%3fb?x=1#frag sent "/a?b?x=1\r\n".
        await AssertSelectorSent("gopher://h/0/a%3fb?x=1#frag", "/a?b?x=1\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_QueryRightAfterItemType_SendsDecodedQuery()
    {
        // Measured: gopher://127.0.0.1:17070/1?x%20y sent "?x y\r\n".
        await AssertSelectorSent("gopher://h/1?x%20y", "?x y\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_EncodedItemType_RemovesTwoEncodedCharactersBeforeDecoding()
    {
        // Measured: gopher://127.0.0.1:17070/%31%2Ffoo sent "31/foo\r\n", because curl
        // removes "/%" from the still-encoded path and decodes what is left.
        await AssertSelectorSent("gopher://h/%31%2Ffoo", "31/foo\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_DotSegments_AreRemovedBeforeDecodingAndEncodedDotsAreNot()
    {
        // Measured: gopher://127.0.0.1:17070/0/a/../b%2e%2E/c sent "/b../c\r\n".
        await AssertSelectorSent("gopher://h/0/a/../b%2e%2E/c", "/b../c\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_TrailingDotSegment_LeavesTrailingSlash()
    {
        await AssertSelectorSent("gopher://h/1/a/./b/.", "/a/b/\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_DotDotAboveRoot_StaysAtRoot()
    {
        await AssertSelectorSent("gopher://h/../1/foo", "/foo\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_DotDotRemovingEverySegment_SendsEmptySelector()
    {
        await AssertSelectorSent("gopher://h/1/..", "\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_DotDotEndingPath_LeavesTrailingSlash()
    {
        // Measured: gopher://127.0.0.1:17070/1/a/.. sent "/\r\n".
        await AssertSelectorSent("gopher://h/1/a/..", "/\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_NonAsciiItemType_RemovesTwoUtf8Bytes()
    {
        // "é" is two UTF-8 bytes, so "/é" is three and only its last byte stays.
        ScriptedConnection connection = new();
        Diagnostics.Arrange("url", "gopher://h/é/x");

        TransferResult result = await new GopherProtocolHandler(FakeConnector.For(connection)).ExecuteAsync(Context("gopher://h/é/x"));
        Diagnostics.Act("result", DiagnosticText.Result(result));

        Diagnostics.Diff("selector sent", new byte[] { 0xA9, (byte)'/', (byte)'x', 13, 10 }, connection.Written);
        CollectionAssert.AreEqual(new byte[] { 0xA9, (byte)'/', (byte)'x', 13, 10 }, connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoPath_SendsEmptySelector()
    {
        await AssertSelectorSent("gopher://h", "\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_QueryWithNoPath_SendsQuery()
    {
        // Measured: gopher://127.0.0.1:17070?q sent "q\r\n".
        await AssertSelectorSent("gopher://h?q", "q\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_FragmentWithNoPath_SendsEmptySelector()
    {
        await AssertSelectorSent("gopher://h#1/foo", "\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_UserInformationAndIpv6Host_AreNotPartOfSelector()
    {
        // Measured: gopher://u:p@127.0.0.1:17070/1/x sent "/x\r\n".
        await AssertSelectorSent("gopher://u:p@[::1]:7070/1/x", "/x\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_EncodedSlash_SendsDecodedSlash()
    {
        await AssertSelectorSent("gopher://h/1/a%2Fb", "/a/b\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_PercentWithoutTwoFollowingCharacters_SendsPercentAsIs()
    {
        await AssertSelectorSent("gopher://h/1/a%2", "/a%2\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_PercentFollowedByNonHex_SendsPercentAsIs()
    {
        await AssertSelectorSent("gopher://h/1/a%zzb", "/a%zzb\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_Gopher_ConnectsToPort70WithoutTls()
    {
        FakeConnector connector = FakeConnector.For(new ScriptedConnection());
        Diagnostics.Arrange("url", "gopher://h/");

        TransferResult result = await new GopherProtocolHandler(connector).ExecuteAsync(Context("gopher://h/"));
        Diagnostics.Act("result", DiagnosticText.Result(result));
        Diagnostics.Act("connect targets", string.Join(", ", connector.Targets));

        Diagnostics.Assert("connect target", new ConnectTarget("h", 70, false), connector.Targets.SingleOrDefault());
        CollectionAssert.AreEqual(new[] { new ConnectTarget("h", 70, false) }, connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_Gophers_ConnectsToPort70WithTls()
    {
        FakeConnector connector = FakeConnector.For(new ScriptedConnection());
        Diagnostics.Arrange("url", "gophers://h/");

        TransferResult result = await new GopherProtocolHandler(connector).ExecuteAsync(Context("gophers://h/"));
        Diagnostics.Act("result", DiagnosticText.Result(result));
        Diagnostics.Act("connect targets", string.Join(", ", connector.Targets));

        Diagnostics.Assert("connect target", new ConnectTarget("h", 70, true), connector.Targets.SingleOrDefault());
        CollectionAssert.AreEqual(new[] { new ConnectTarget("h", 70, true) }, connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContextWithProxy_TunnelsToTheOriginOnPort70ThroughThatProxy()
    {
        FakeConnector connector = FakeConnector.For(new ScriptedConnection());
        var proxy = new ProxyEndpoint(ProxyKind.Http, "proxy.example", 3128, null);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("gopher://example.com/"),
            Output = new MemoryStream(),
            Proxy = proxy,
        };
        Diagnostics.Arrange("url", "gopher://example.com/");
        Diagnostics.Arrange("proxy", proxy);

        TransferResult result = await new GopherProtocolHandler(connector).ExecuteAsync(context);
        Diagnostics.Act("result", DiagnosticText.Result(result));
        Diagnostics.Act("connect targets", string.Join(", ", connector.Targets));

        ConnectTarget expected = new("example.com", 70, false) { Proxy = proxy };
        Diagnostics.Assert("connect target", expected, connector.Targets.SingleOrDefault());
        CollectionAssert.AreEqual(new[] { new ConnectTarget("example.com", 70, false) { Proxy = proxy } }, connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContextWithEvents_PassesThemToTheConnectTargetSoTheConnectLinesAreReported()
    {
        FakeConnector connector = FakeConnector.For(new ScriptedConnection());
        var events = new IgnoringTransferEvents();
        var context = new TransferContext { Url = CurlUrl.Parse("gopher://example.com/"), Output = new MemoryStream(), Events = events };
        Diagnostics.Arrange("url", "gopher://example.com/");
        Diagnostics.Arrange("events", nameof(IgnoringTransferEvents));

        TransferResult result = await new GopherProtocolHandler(connector).ExecuteAsync(context);
        Diagnostics.Act("result", DiagnosticText.Result(result));
        Diagnostics.Act("connect targets", connector.Targets.Count);

        Diagnostics.Assert("connect target's events are the context's", true, ReferenceEquals(events, connector.Targets.SingleOrDefault()?.Events));
        Assert.AreSame(events, connector.Targets.Single().Events);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContextWithoutProxy_ConnectsDirectly()
    {
        FakeConnector connector = FakeConnector.For(new ScriptedConnection());
        Diagnostics.Arrange("url", "gopher://example.com/");
        Diagnostics.Arrange("proxy", "none");

        TransferResult result = await new GopherProtocolHandler(connector).ExecuteAsync(Context("gopher://example.com/"));
        Diagnostics.Act("result", DiagnosticText.Result(result));
        Diagnostics.Act("connect targets", string.Join(", ", connector.Targets));

        Diagnostics.Assert("connect target's proxy", "null", connector.Targets.SingleOrDefault()?.Proxy?.ToString() ?? "null");
        Assert.IsNull(connector.Targets.Single().Proxy);
    }

    [TestMethod]
    public async Task ExecuteAsync_ExplicitPort_ConnectsToThatPort()
    {
        FakeConnector connector = FakeConnector.For(new ScriptedConnection());
        Diagnostics.Arrange("url", "gophers://h:7070/");

        TransferResult result = await new GopherProtocolHandler(connector).ExecuteAsync(Context("gophers://h:7070/"));
        Diagnostics.Act("result", DiagnosticText.Result(result));
        Diagnostics.Act("connect targets", string.Join(", ", connector.Targets));

        Diagnostics.Assert("connect target", new ConnectTarget("h", 7070, true), connector.Targets.SingleOrDefault());
        CollectionAssert.AreEqual(new[] { new ConnectTarget("h", 7070, true) }, connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_Reply_ReachesOutputByteForByte()
    {
        ScriptedConnection connection = new(MeasuredReply[..10], MeasuredReply[10..]);
        MemoryStream output = new();
        Diagnostics.Arrange("url", "gopher://h/1");
        Diagnostics.Arrange("scripted reads", DiagnosticText.Lines([Encoding.ASCII.GetString(MeasuredReply[..10]), Encoding.ASCII.GetString(MeasuredReply[10..])]));

        TransferResult result = await new GopherProtocolHandler(FakeConnector.For(connection))
            .ExecuteAsync(Context("gopher://h/1", output));
        Diagnostics.Act("result", DiagnosticText.Result(result));
        Diagnostics.Bytes("output", output.ToArray());

        Diagnostics.Assert("result", TransferResult.Success(MeasuredReply.Length), result);
        Diagnostics.Diff("output", MeasuredReply, output.ToArray());
        Diagnostics.Assert("connection disposed", true, connection.IsDisposed);
        Assert.AreEqual(TransferResult.Success(MeasuredReply.Length), result);
        CollectionAssert.AreEqual(MeasuredReply, output.ToArray());
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerClosesWithoutReplying_SucceedsWithNothingWritten()
    {
        // Measured: curl 8.21.0 against a listener that read the selector and closed
        // printed nothing and exited 0.
        ScriptedConnection connection = new();
        MemoryStream output = new();
        Diagnostics.Arrange("url", "gopher://h/1/foo");
        Diagnostics.Arrange("scripted reads", "none: the server closes at once");

        TransferResult result = await new GopherProtocolHandler(FakeConnector.For(connection))
            .ExecuteAsync(Context("gopher://h/1/foo", output));
        Diagnostics.Act("result", DiagnosticText.Result(result));

        Diagnostics.Assert("result", TransferResult.Success(0), result);
        Diagnostics.Assert("output length", 0, output.Length);
        Assert.AreEqual(TransferResult.Success(0), result);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_ReturnsItsCodeAndMessageUnchanged()
    {
        FakeConnector connector = new(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect to h port 70"));
        MemoryStream output = new();
        Diagnostics.Arrange("connect result", "Failed CouldntConnect \"Failed to connect to h port 70\"");

        TransferResult result = await new GopherProtocolHandler(connector).ExecuteAsync(Context("gopher://h/", output));
        Diagnostics.Act("result", DiagnosticText.Result(result));

        Diagnostics.Assert("result", new TransferResult(CurlExitCode.CouldntConnect, 0, "Failed to connect to h port 70"), result);
        Diagnostics.Assert("output length", 0, output.Length);
        Diagnostics.Assert("connection refused", false, result.IsConnectionRefused);
        Assert.AreEqual(new TransferResult(CurlExitCode.CouldntConnect, 0, "Failed to connect to h port 70"), result);
        Assert.AreEqual(0, output.Length);
        Assert.IsFalse(result.IsConnectionRefused);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectRefused_ReturnsExit7MarkedConnectionRefused()
    {
        FakeConnector connector = new(ConnectResult.Refused("Failed to connect to h port 70"));
        Diagnostics.Arrange("connect result", "Refused \"Failed to connect to h port 70\"");

        TransferResult result = await new GopherProtocolHandler(connector).ExecuteAsync(Context("gopher://h/", new MemoryStream()));
        Diagnostics.Act("result", DiagnosticText.Result(result));

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Diagnostics.Assert("connection refused", true, result.IsConnectionRefused);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    public async Task ExecuteAsync_SelectorDecodesToNul_ReturnsUrlMalformatAfterConnectingAndSendsNothing()
    {
        // Measured: gopher://127.0.0.1:17070/0/a%00b connected, sent nothing and exited 3.
        ScriptedConnection connection = new(MeasuredReply);
        FakeConnector connector = FakeConnector.For(connection);
        MemoryStream output = new();
        Diagnostics.Arrange("url", "gopher://h/0/a%00b");

        TransferResult result = await new GopherProtocolHandler(connector).ExecuteAsync(Context("gopher://h/0/a%00b", output));
        Diagnostics.Act("result", DiagnosticText.Result(result));
        Diagnostics.Bytes("selector sent", connection.Written);

        Diagnostics.Assert("result", TransferResult.Failure(CurlExitCode.UrlMalformat, "URL using bad/illegal format or missing URL"), result);
        Diagnostics.Assert("connect count", 1, connector.Targets.Count);
        Diagnostics.Assert("bytes sent", 0, connection.Written.Length);
        Diagnostics.Assert("output length", 0, output.Length);
        Diagnostics.Assert("connection disposed", true, connection.IsDisposed);
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.UrlMalformat, "URL using bad/illegal format or missing URL"),
            result);
        Assert.HasCount(1, connector.Targets);
        Assert.IsEmpty(connection.Written);
        Assert.AreEqual(0, output.Length);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_SendFails_ReturnsSendError()
    {
        ScriptedConnection connection = new(MeasuredReply) { FailWrites = true };
        MemoryStream output = new();
        Diagnostics.Arrange("connection", "every write throws a plain IOException");

        TransferResult result = await new GopherProtocolHandler(FakeConnector.For(connection))
            .ExecuteAsync(Context("gopher://h/", output));
        Diagnostics.Act("result", DiagnosticText.Result(result));

        Diagnostics.Assert("result", TransferResult.Failure(CurlExitCode.SendError, "Failed sending data to the peer"), result);
        Diagnostics.Assert("output length", 0, output.Length);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.SendError, "Failed sending data to the peer"), result);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReceiveFails_ReturnsRecvErrorWithBytesAlreadyWritten()
    {
        ScriptedConnection connection = new(MeasuredReply[..5], null);
        MemoryStream output = new();
        Diagnostics.Arrange("scripted reads", DiagnosticText.Escape(Encoding.ASCII.GetString(MeasuredReply[..5])) + ", then a read that throws IOException");

        TransferResult result = await new GopherProtocolHandler(FakeConnector.For(connection))
            .ExecuteAsync(Context("gopher://h/", output));
        Diagnostics.Act("result", DiagnosticText.Result(result));
        Diagnostics.Bytes("output", output.ToArray());

        Diagnostics.Assert("result", new TransferResult(CurlExitCode.RecvError, 5, "Failure when receiving data from the peer"), result);
        Diagnostics.Diff("output", MeasuredReply[..5], output.ToArray());
        Assert.AreEqual(
            new TransferResult(CurlExitCode.RecvError, 5, "Failure when receiving data from the peer"),
            result);
        CollectionAssert.AreEqual(MeasuredReply[..5], output.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_OutputWriteFailsWithPlainIOException_ReportsReturnedZero()
    {
        ScriptedConnection connection = new(MeasuredReply);
        WriteRefusingStream output = new();
        Diagnostics.Arrange("output", "every write throws a plain IOException");
        Diagnostics.Arrange("reply length", MeasuredReply.Length);

        TransferResult result = await new GopherProtocolHandler(FakeConnector.For(connection))
            .ExecuteAsync(Context("gopher://h/", output));
        Diagnostics.Act("result", DiagnosticText.Result(result));

        TransferResult expected = new(
            CurlExitCode.WriteError,
            0,
            $"Failure writing output to destination, passed {MeasuredReply.Length} returned 0");
        Diagnostics.Assert("result", expected, result);
        Assert.AreEqual(
            new TransferResult(
                CurlExitCode.WriteError,
                0,
                $"Failure writing output to destination, passed {MeasuredReply.Length} returned 0"),
            result);
    }

    // Measured 2026-09-26 against curl 8.21.0 (BL-117): a loopback server sent count lines
    // of size bytes, 5 ms apart, to curl -sS gopher://127.0.0.1:<port>/0/x 2>&1 >&-.
    // Each line arrives as one read; curl's 4096-byte stdio buffer takes whole lines until
    // one overflows it, and returned M is the room left then.
    [TestMethod]
    [DataRow(100, 100, 40, 96, DisplayName = "100 x 100: passed 100 returned 96")]
    [DataRow(300, 100, 13, 196, DisplayName = "300 x 100: passed 300 returned 196")]
    [DataRow(1000, 20, 4, 96, DisplayName = "1000 x 20: passed 1000 returned 96")]
    [DataRow(30, 400, 136, 16, DisplayName = "30 x 400: passed 30 returned 16")]
    [DataRow(5000, 5, 0, 0, DisplayName = "5000 x 5: passed 5000 returned 0")]
    public async Task ExecuteAsync_OutputFailsWhenStdioBufferOverflows_ReportsMeasuredPassedAndReturned(
        int size,
        int count,
        int linesBuffered,
        int returned)
    {
        byte[] line = [.. Enumerable.Repeat((byte)'x', size - 2), .. CarriageReturnLineFeed];
        ScriptedConnection connection = new([.. Enumerable.Repeat(line, count)]);
        BufferOverflowRefusingStream output = new(4096);
        Diagnostics.Arrange("scripted reads", $"{count} lines of {size} bytes, one per read");
        Diagnostics.Arrange("output", "a 4096-byte buffer that refuses the write that overflows it");

        TransferResult result = await new GopherProtocolHandler(FakeConnector.For(connection))
            .ExecuteAsync(Context("gopher://h/0/x", output));
        Diagnostics.Act("result", DiagnosticText.Result(result));

        TransferResult expected = new(
            CurlExitCode.WriteError,
            (long)linesBuffered * size,
            $"Failure writing output to destination, passed {size} returned {returned}");
        Diagnostics.Assert("result", expected, result);
        Assert.AreEqual(
            new TransferResult(
                CurlExitCode.WriteError,
                (long)linesBuffered * size,
                $"Failure writing output to destination, passed {size} returned {returned}"),
            result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Cancelled_Throws()
    {
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        ScriptedConnection connection = new(MeasuredReply);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("gopher://h/"),
            Output = new MemoryStream(),
            CancellationToken = cancellation.Token,
        };
        Diagnostics.Arrange("cancellation token", "already cancelled");

        Exception thrown = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await new GopherProtocolHandler(FakeConnector.For(connection)).ExecuteAsync(context));
        Diagnostics.Act("thrown", thrown.GetType().Name + ": " + thrown.Message);

        Diagnostics.Assert("connection disposed", true, connection.IsDisposed);
        Assert.IsTrue(connection.IsDisposed);
    }

    private async Task AssertSelectorSent(string url, string expectedBytes)
    {
        ScriptedConnection connection = new();
        Diagnostics.Arrange("url", url);

        TransferResult result = await new GopherProtocolHandler(FakeConnector.For(connection)).ExecuteAsync(Context(url));
        Diagnostics.Act("result", DiagnosticText.Result(result));
        Diagnostics.Act("selector sent", DiagnosticText.Escape(Encoding.Latin1.GetString(connection.Written)));

        Diagnostics.Assert("result", TransferResult.Success(0), result);
        Diagnostics.Diff("selector sent", Encoding.ASCII.GetBytes(expectedBytes), connection.Written);
        Assert.AreEqual(TransferResult.Success(0), result);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes(expectedBytes), connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_Connected_ReportsTheTransferStartedAndTheRunningByteTotal()
    {
        // The runner's -m watchdog reads these to print "with 7 bytes received" (ADR-0117, BL-511).
        ScriptedConnection connection = new("hel"u8.ToArray(), "lo\r\n"u8.ToArray());
        RecordingProgress progress = new();
        var context = new TransferContext { Url = CurlUrl.Parse("gopher://h/1"), Output = new MemoryStream(), Progress = progress };
        Diagnostics.Arrange("scripted reads", DiagnosticText.Lines(["hel", "lo\r\n"]));

        TransferResult result = await new GopherProtocolHandler(FakeConnector.For(connection)).ExecuteAsync(context);
        Diagnostics.Act("result", DiagnosticText.Result(result));
        Diagnostics.Act("progress reports", DiagnosticText.Lines(progress.Reports));

        Diagnostics.Diff("progress reports", "started|downloaded 3|downloaded 7", string.Join("|", progress.Reports));
        CollectionAssert.AreEqual(new[] { "started", "downloaded 3", "downloaded 7" }, progress.Reports);
    }

    private static TransferContext Context(string url, Stream? output = null) =>
        new() { Url = CurlUrl.Parse(url), Output = output ?? new MemoryStream() };

    private sealed class RecordingProgress : ITransferProgress
    {
        public List<string> Reports { get; } = [];

        public void ReportTransferStarted() => Reports.Add("started");

        public void ReportDownloaded(long bytesSoFar, long? expectedTotal) => Reports.Add($"downloaded {bytesSoFar}{expectedTotal}");

        public void ReportUploaded(long bytesSoFar, long? expectedTotal) => Reports.Add($"uploaded {bytesSoFar}");
    }
}
