using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Gopher.Fakes;

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

    [TestMethod]
    public void SupportedSchemes_IsExactlyGopherAndGophers()
    {
        GopherProtocolHandler handler = new(FakeConnector.For(new ScriptedConnection()));

        CollectionAssert.AreEqual(new[] { "gopher", "gophers" }, handler.SupportedSchemes.ToArray());
    }

    [TestMethod]
    public void Constructor_NullConnector_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new GopherProtocolHandler(null!));
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws()
    {
        GopherProtocolHandler handler = new(FakeConnector.For(new ScriptedConnection()));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await handler.ExecuteAsync(null!));
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

        await new GopherProtocolHandler(FakeConnector.For(connection)).ExecuteAsync(Context("gopher://h/é/x"));

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

        await new GopherProtocolHandler(connector).ExecuteAsync(Context("gopher://h/"));

        CollectionAssert.AreEqual(new[] { new ConnectTarget("h", 70, false) }, connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_Gophers_ConnectsToPort70WithTls()
    {
        FakeConnector connector = FakeConnector.For(new ScriptedConnection());

        await new GopherProtocolHandler(connector).ExecuteAsync(Context("gophers://h/"));

        CollectionAssert.AreEqual(new[] { new ConnectTarget("h", 70, true) }, connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_ExplicitPort_ConnectsToThatPort()
    {
        FakeConnector connector = FakeConnector.For(new ScriptedConnection());

        await new GopherProtocolHandler(connector).ExecuteAsync(Context("gophers://h:7070/"));

        CollectionAssert.AreEqual(new[] { new ConnectTarget("h", 7070, true) }, connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_Reply_ReachesOutputByteForByte()
    {
        ScriptedConnection connection = new(MeasuredReply[..10], MeasuredReply[10..]);
        MemoryStream output = new();

        TransferResult result = await new GopherProtocolHandler(FakeConnector.For(connection))
            .ExecuteAsync(Context("gopher://h/1", output));

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

        TransferResult result = await new GopherProtocolHandler(FakeConnector.For(connection))
            .ExecuteAsync(Context("gopher://h/1/foo", output));

        Assert.AreEqual(TransferResult.Success(0), result);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_ReturnsItsCodeAndMessageUnchanged()
    {
        FakeConnector connector = new(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect to h port 70"));
        MemoryStream output = new();

        TransferResult result = await new GopherProtocolHandler(connector).ExecuteAsync(Context("gopher://h/", output));

        Assert.AreEqual(new TransferResult(CurlExitCode.CouldntConnect, 0, "Failed to connect to h port 70"), result);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_SelectorDecodesToNul_ReturnsUrlMalformatAfterConnectingAndSendsNothing()
    {
        // Measured: gopher://127.0.0.1:17070/0/a%00b connected, sent nothing and exited 3.
        ScriptedConnection connection = new(MeasuredReply);
        FakeConnector connector = FakeConnector.For(connection);
        MemoryStream output = new();

        TransferResult result = await new GopherProtocolHandler(connector).ExecuteAsync(Context("gopher://h/0/a%00b", output));

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

        TransferResult result = await new GopherProtocolHandler(FakeConnector.For(connection))
            .ExecuteAsync(Context("gopher://h/", output));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.SendError, "Failure when sending data to the peer"), result);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReceiveFails_ReturnsRecvErrorWithBytesAlreadyWritten()
    {
        ScriptedConnection connection = new(MeasuredReply[..5], null);
        MemoryStream output = new();

        TransferResult result = await new GopherProtocolHandler(FakeConnector.For(connection))
            .ExecuteAsync(Context("gopher://h/", output));

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

        TransferResult result = await new GopherProtocolHandler(FakeConnector.For(connection))
            .ExecuteAsync(Context("gopher://h/", output));

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

        TransferResult result = await new GopherProtocolHandler(FakeConnector.For(connection))
            .ExecuteAsync(Context("gopher://h/0/x", output));

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

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await new GopherProtocolHandler(FakeConnector.For(connection)).ExecuteAsync(context));
        Assert.IsTrue(connection.IsDisposed);
    }

    private static async Task AssertSelectorSent(string url, string expectedBytes)
    {
        ScriptedConnection connection = new();

        TransferResult result = await new GopherProtocolHandler(FakeConnector.For(connection)).ExecuteAsync(Context(url));

        Assert.AreEqual(TransferResult.Success(0), result);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes(expectedBytes), connection.Written);
    }

    private static TransferContext Context(string url, Stream? output = null) =>
        new() { Url = CurlUrl.Parse(url), Output = output ?? new MemoryStream() };
}
