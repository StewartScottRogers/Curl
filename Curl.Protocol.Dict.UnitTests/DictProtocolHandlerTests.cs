using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Dict.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Dict;

/// <summary>
/// Pins the <c>dict://</c> transfer against curl 8.21.0, measured on 2026-09-26 against a
/// loopback listener: the bytes sent for each URL path, the bytes written to the output
/// and the exit code.
/// </summary>
[TestClass]
public sealed class DictProtocolHandlerTests
{
    private const string Client = "CLIENT libcurl 8.21.0\r\n";

    private const string Quit = "QUIT\r\n";

    private const string Reply = "220 hello\r\n150 1 found\r\n221 bye\r\n";

    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void SupportedSchemes_IsDictOnly()
    {
        Diagnostics.Arrange("connector", "connects to a silent scripted connection");
        var handler = new DictProtocolHandler(new RecordingConnector(ConnectResult.Connected(new ScriptedConnection())));

        string[] schemes = handler.SupportedSchemes.ToArray();
        Diagnostics.Act("supported schemes", string.Join(", ", schemes));

        Diagnostics.Assert("supported schemes", "dict", string.Join(", ", schemes));
        CollectionAssert.AreEqual(new[] { "dict" }, handler.SupportedSchemes.ToArray());
    }

    [TestMethod]
    public void Constructor_NullConnector_Throws()
    {
        Diagnostics.Arrange("connector", "null");

        Exception thrown = Assert.ThrowsExactly<ArgumentNullException>(() => new DictProtocolHandler(null!));
        Diagnostics.Act("thrown", thrown.GetType().Name);

        Diagnostics.Assert("exception type", nameof(ArgumentNullException), thrown.GetType().Name);
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws()
    {
        Diagnostics.Arrange("context", "null");
        var handler = new DictProtocolHandler(new RecordingConnector(ConnectResult.Connected(new ScriptedConnection())));

        Exception thrown = await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await handler.ExecuteAsync(null!));
        Diagnostics.Act("thrown", thrown.GetType().Name);

        Diagnostics.Assert("exception type", nameof(ArgumentNullException), thrown.GetType().Name);
    }

    [TestMethod]
    public async Task ExecuteAsync_UrlWithoutPort_ConnectsToPort2628WithoutTls()
    {
        Diagnostics.Arrange("URL", "dict://h/d:x");
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection()));

        await new DictProtocolHandler(connector).ExecuteAsync(Context("dict://h/d:x", new MemoryStream()));
        Diagnostics.Act("connect targets", string.Join(", ", connector.Targets));

        Diagnostics.Assert("connect target", new ConnectTarget("h", 2628, false), connector.Targets.Single());
        Assert.AreEqual(new ConnectTarget("h", 2628, false), connector.Targets.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_UrlWithPort_ConnectsToThatPort()
    {
        Diagnostics.Arrange("URL", "dict://h:1234/d:x");
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection()));

        await new DictProtocolHandler(connector).ExecuteAsync(Context("dict://h:1234/d:x", new MemoryStream()));
        Diagnostics.Act("connect targets", string.Join(", ", connector.Targets));

        Diagnostics.Assert("connect target", new ConnectTarget("h", 1234, false), connector.Targets.Single());
        Assert.AreEqual(new ConnectTarget("h", 1234, false), connector.Targets.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_ContextWithProxy_TunnelsToTheOriginOnPort2628ThroughThatProxy()
    {
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection()));
        var proxy = new ProxyEndpoint(ProxyKind.Http, "proxy.example", 3128, null);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("dict://example.com/d:x"),
            Output = new MemoryStream(),
            Proxy = proxy,
        };
        Diagnostics.Arrange("URL", "dict://example.com/d:x");
        Diagnostics.Arrange("proxy", proxy);

        await new DictProtocolHandler(connector).ExecuteAsync(context);
        Diagnostics.Act("connect targets", string.Join(", ", connector.Targets));

        Diagnostics.Assert("connect target", new ConnectTarget("example.com", 2628, false) { Proxy = proxy }, connector.Targets.Single());
        Assert.AreEqual(new ConnectTarget("example.com", 2628, false) { Proxy = proxy }, connector.Targets.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_ContextWithEvents_PassesThemToTheConnectTargetSoTheConnectLinesAreReported()
    {
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection()));
        var events = new IgnoringTransferEvents();
        var context = new TransferContext { Url = CurlUrl.Parse("dict://example.com/d:x"), Output = new MemoryStream(), Events = events };
        Diagnostics.Arrange("URL", "dict://example.com/d:x");
        Diagnostics.Arrange("events", nameof(IgnoringTransferEvents));

        await new DictProtocolHandler(connector).ExecuteAsync(context);
        Diagnostics.Act("connect target's events", connector.Targets.Single().Events?.GetType().Name ?? "null");

        Diagnostics.Assert("connect target's events are the context's", true, ReferenceEquals(events, connector.Targets.Single().Events));
        Assert.AreSame(events, connector.Targets.Single().Events);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContextWithoutProxy_ConnectsDirectly()
    {
        Diagnostics.Arrange("URL", "dict://example.com/d:x (no proxy)");
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection()));

        await new DictProtocolHandler(connector).ExecuteAsync(Context("dict://example.com/d:x", new MemoryStream()));
        Diagnostics.Act("connect target's proxy", connector.Targets.Single().Proxy?.ToString() ?? "null");

        Diagnostics.Assert("connect target's proxy", "null", connector.Targets.Single().Proxy?.ToString() ?? "null");
        Assert.IsNull(connector.Targets.Single().Proxy);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_ReturnsTheFailureUnchangedAndWritesNothing()
    {
        Diagnostics.Arrange("URL", "dict://h/d:x");
        Diagnostics.Arrange("connect result", "Failed(CouldntConnect, \"m\")");
        var connector = new RecordingConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "m"));
        var output = new MemoryStream();

        TransferResult result = await new DictProtocolHandler(connector).ExecuteAsync(Context("dict://h/d:x", output));
        Diagnostics.Act("result", DiagnosticText.Result(result));
        Diagnostics.Bytes("output", output.ToArray());

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Diagnostics.Assert("error message", "m", result.ErrorMessage);
        Diagnostics.Assert("output length", 0L, output.Length);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("m", result.ErrorMessage);
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.AreEqual(0L, output.Length);
        Assert.IsFalse(result.IsConnectionRefused);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectRefused_ReturnsExit7MarkedConnectionRefused()
    {
        Diagnostics.Arrange("URL", "dict://h/d:x");
        Diagnostics.Arrange("connect result", "Refused(\"m\")");
        var connector = new RecordingConnector(ConnectResult.Refused("m"));

        TransferResult result = await new DictProtocolHandler(connector).ExecuteAsync(Context("dict://h/d:x", new MemoryStream()));
        Diagnostics.Act("result", DiagnosticText.Result(result));

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Diagnostics.Assert("connection refused", true, result.IsConnectionRefused);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    public async Task ExecuteAsync_DWord_SendsDefineInAnyDatabase()
    {
        string expected = Client + "DEFINE ! word\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/d:word", expected));
    }

    [TestMethod]
    public async Task ExecuteAsync_DWordDatabase_SendsDefineInThatDatabase()
    {
        string expected = Client + "DEFINE db word\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/d:word:db", expected));
    }

    [TestMethod]
    public async Task ExecuteAsync_LookupWord_SendsDefineInAnyDatabase()
    {
        string expected = Client + "DEFINE ! word\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/lookup:word", expected));
    }

    [TestMethod]
    public async Task ExecuteAsync_MWordDatabaseStrategy_SendsMatch()
    {
        string expected = Client + "MATCH db prefix word\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/m:word:db:prefix", expected));
    }

    [TestMethod]
    public async Task ExecuteAsync_FindWord_SendsMatchInAnyDatabaseWithDefaultStrategy()
    {
        string expected = Client + "MATCH ! . word\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/find:word", expected));
    }

    [TestMethod]
    public async Task ExecuteAsync_PlainWord_SendsTheWordAsTheCommand()
    {
        string expected = Client + "word\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/word", expected));
    }

    [TestMethod]
    public async Task ExecuteAsync_EncodedSpace_SendsTheDecodedCommand()
    {
        string expected = Client + "show db\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/show%20db", expected));
    }

    [TestMethod]
    public async Task ExecuteAsync_RootPath_SendsAnEmptyCommand()
    {
        string expected = Client + "\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/", expected));
    }

    [TestMethod]
    public async Task ExecuteAsync_DefineLongSpelling_SendsDefineAsDDoes()
    {
        string expected = Client + "DEFINE ! word\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/define:word", expected));
        expected = Client + "DEFINE db word\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/define:word:db", expected));
    }

    [TestMethod]
    public async Task ExecuteAsync_MatchLongSpelling_SendsMatchAsMDoes()
    {
        string expected = Client + "MATCH ! . word\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/match:word", expected));
        expected = Client + "MATCH db prefix word\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/match:word:db:prefix", expected));
    }

    [TestMethod]
    public async Task ExecuteAsync_PrefixInOtherCase_IsRecognised()
    {
        string expected = Client + "DEFINE ! word\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/DEFINE:word", expected));
        Assert.AreEqual(expected, await SentForAsync("/Lookup:word", expected));
        expected = Client + "MATCH ! . word\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/FIND:word", expected));
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyOrMissingFields_SendTheDefaults()
    {
        string expected = Client + "DEFINE ! default\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/d:", expected));
        expected = Client + "MATCH ! . default\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/m:", expected));
        expected = Client + "MATCH ! prefix x\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/m:x::prefix", expected));
        expected = Client + "MATCH db . x\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/m:x:db:", expected));
        expected = Client + "MATCH db . word\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/m:word:db", expected));
        expected = Client + "DEFINE ! word\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/d:word:", expected));
    }

    [TestMethod]
    public async Task ExecuteAsync_FieldsBeyondTheLast_AreIgnored()
    {
        string expected = Client + "DEFINE db word\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/d:word:db:extra", expected));
        expected = Client + "MATCH db s w\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/m:w:db:s:extra", expected));
    }

    [TestMethod]
    public async Task ExecuteAsync_EncodedColon_SeparatesFields()
    {
        string expected = Client + "DEFINE b a\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/d:a%3Ab", expected));
    }

    [TestMethod]
    public async Task ExecuteAsync_WordSpecials_AreSentWithABackslash()
    {
        string expected = Client + "DEFINE ! two\\ words\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/d:two%20words", expected));
        expected = Client + "DEFINE ! a\\'b\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/d:a'b", expected));
        expected = Client + "DEFINE ! a\\\"b\\\\c\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/d:a%22b%5Cc", expected));
        expected = Client + "DEFINE ! a$b\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/d:a%24b", expected));
    }

    [TestMethod]
    public async Task ExecuteAsync_WordBytesFrom7F_AreSentWithABackslash()
    {
        byte[] expected =
        [
            .. Encoding.ASCII.GetBytes(Client + "DEFINE ! "),
            0x5C, 0xC3, 0x5C, 0xA9, 0x5C, 0x7F,
            .. Encoding.ASCII.GetBytes("\r\n" + Quit),
        ];
        Diagnostics.Bytes("expected request", expected);

        byte[] sent = await SentBytesForAsync("/d:%C3%A9%7F");

        Diagnostics.Diff("request sent", expected, sent);
        CollectionAssert.AreEqual(expected, sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_DatabaseAndStrategy_AreSentWithoutBackslashes()
    {
        string expected = Client + "DEFINE a b w\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/d:w:a%20b", expected));
        expected = Client + "MATCH a\"b s t w\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/m:w:a%22b:s%20t", expected));
    }

    [TestMethod]
    public async Task ExecuteAsync_OtherCommand_SendsColonsAsSpaces()
    {
        string expected = Client + "show db x\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/show:db:x", expected));
        expected = Client + "dx word\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/dx:word", expected));
        expected = Client + "d\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/d", expected));
    }

    [TestMethod]
    public async Task ExecuteAsync_QueryAndFragment_AreNotSent()
    {
        string expected = Client + "DEFINE ! word\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/d:word?q=1", expected));
        Assert.AreEqual(expected, await SentForAsync("/d:word#frag", expected));
    }

    [TestMethod]
    [DataRow("/d:a\\b", "DEFINE ! a/b")]
    [DataRow("/d:x/../y", "y")]
    [DataRow("/a/%2e%2e/d:x", "DEFINE ! x")]
    [DataRow("/d:x/./y", "DEFINE ! x/y")]
    public async Task ExecuteAsync_BackslashOrDotSegments_AreNormalisedAsCurlDoes(string path, string command)
    {
        string expected = Client + command + "\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync(path, expected));
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledToken_ThrowsOperationCanceledAndDisposesTheConnection()
    {
        var connection = new ScriptedConnection();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("dict://h/d:word"),
            Output = new MemoryStream(),
            CancellationToken = new CancellationToken(canceled: true),
        };
        Diagnostics.Arrange("URL", "dict://h/d:word");
        Diagnostics.Arrange("cancellation token", "already cancelled");

        Exception thrown = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await new DictProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection))).ExecuteAsync(context));
        Diagnostics.Act("thrown", thrown.GetType().Name);
        Diagnostics.Act("connection disposed", connection.IsDisposed);

        Diagnostics.Assert("connection disposed", true, connection.IsDisposed);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_PercentNotFollowedByTwoHexDigits_IsSentAsIs()
    {
        string expected = Client + "DEFINE ! %zz\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/d:%zz", expected));
        expected = Client + "DEFINE ! a%a\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/d:a%a", expected));
        expected = Client + "DEFINE ! a%\r\n" + Quit;
        Assert.AreEqual(expected, await SentForAsync("/d:a%", expected));
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerReply_IsWrittenByteForByte()
    {
        byte[] reply = Encoding.ASCII.GetBytes(Reply);
        var connection = new ScriptedConnection(reply[..10], reply[10..]);
        var output = new MemoryStream();
        Diagnostics.Arrange("URL", "dict://h/d:word");
        Diagnostics.Arrange("server reply, in reads of 10 and the rest", DiagnosticText.Escape(Reply));

        TransferResult result = await new DictProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(Context("dict://h/d:word", output));
        Diagnostics.Act("result", DiagnosticText.Result(result));
        Diagnostics.Bytes("output", output.ToArray());

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Diff("output", reply, output.ToArray());
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual((long)reply.Length, result.BytesTransferred);
        CollectionAssert.AreEqual(reply, output.ToArray());
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerClosesWithoutSending_ReturnsOkAndWritesNothing()
    {
        var connection = new ScriptedConnection();
        var output = new MemoryStream();
        Diagnostics.Arrange("URL", "dict://h/d:word");
        Diagnostics.Arrange("server reply", "none, the server closes");

        TransferResult result = await new DictProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(Context("dict://h/d:word", output));
        Diagnostics.Act("result", DiagnosticText.Result(result));
        Diagnostics.Bytes("request sent", connection.Sent);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("output length", 0L, output.Length);
        Diagnostics.Diff("request sent", Client + "DEFINE ! word\r\n" + Quit, Encoding.ASCII.GetString(connection.Sent));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.AreEqual(0L, output.Length);
        Assert.AreEqual(Client + "DEFINE ! word\r\n" + Quit, Encoding.ASCII.GetString(connection.Sent));
    }

    [TestMethod]
    [DataRow("/d:a%01b")]
    [DataRow("/show%0Adb")]
    [DataRow("/m:x:d%01b")]
    [DataRow("/d:a%00b")]
    [DataRow("/d:a%0Db")]
    public async Task ExecuteAsync_PathDecodesToAControlCharacter_ReturnsUrlMalformatAfterConnectingAndSendsNothing(string path)
    {
        var connection = new ScriptedConnection(Encoding.ASCII.GetBytes(Reply));
        var connector = new RecordingConnector(ConnectResult.Connected(connection));
        var output = new MemoryStream();
        Diagnostics.Arrange("URL", "dict://h" + path);

        TransferResult result = await new DictProtocolHandler(connector).ExecuteAsync(Context("dict://h" + path, output));
        Diagnostics.Act("result", DiagnosticText.Result(result));
        Diagnostics.Act("connects", connector.Targets.Count);
        Diagnostics.Bytes("request sent", connection.Sent);

        Diagnostics.Assert("exit code", CurlExitCode.UrlMalformat, result.ExitCode);
        Diagnostics.Assert("bytes sent", 0, connection.Sent.Length);
        Diagnostics.Assert("output length", 0L, output.Length);
        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual("URL using bad/illegal format or missing URL", result.ErrorMessage);
        Assert.HasCount(1, connector.Targets);
        Assert.IsEmpty(connection.Sent);
        Assert.AreEqual(0L, output.Length);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_Connected_ReportsTheTransferStartedAndTheRunningByteTotal()
    {
        // The runner's -m watchdog reads these to print "with 7 bytes received" (ADR-0117, BL-511).
        var connection = new ScriptedConnection("hel"u8.ToArray(), "lo\r\n"u8.ToArray());
        RecordingProgress progress = new();
        var context = new TransferContext { Url = CurlUrl.Parse("dict://h/d:x"), Output = new MemoryStream(), Progress = progress };
        Diagnostics.Arrange("URL", "dict://h/d:x");
        Diagnostics.Arrange("server reads", DiagnosticText.Lines(["hel", "lo\r\n"]));

        await new DictProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection))).ExecuteAsync(context);
        Diagnostics.Act("progress reports", DiagnosticText.Lines(progress.Reports));

        Diagnostics.Assert("progress reports", "[started, downloaded 3, downloaded 7]", "[" + string.Join(", ", progress.Reports) + "]");
        CollectionAssert.AreEqual(new[] { "started", "downloaded 3", "downloaded 7" }, progress.Reports);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFailed_ReportsNothing()
    {
        RecordingProgress progress = new();
        var context = new TransferContext { Url = CurlUrl.Parse("dict://h/d:x"), Output = new MemoryStream(), Progress = progress };
        Diagnostics.Arrange("URL", "dict://h/d:x");
        Diagnostics.Arrange("connect result", "Failed(CouldntConnect, \"refused\")");

        await new DictProtocolHandler(new RecordingConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "refused"))).ExecuteAsync(context);
        Diagnostics.Act("progress reports", DiagnosticText.Lines(progress.Reports));

        Diagnostics.Assert("progress report count", 0, progress.Reports.Count);
        Assert.IsEmpty(progress.Reports);
    }

    private static TransferContext Context(string url, Stream output) =>
        new() { Url = CurlUrl.Parse(url), Output = output };

    private sealed class RecordingProgress : ITransferProgress
    {
        public List<string> Reports { get; } = [];

        public void ReportTransferStarted() => Reports.Add("started");

        public void ReportDownloaded(long bytesSoFar, long? expectedTotal) => Reports.Add($"downloaded {bytesSoFar}{expectedTotal}");

        public void ReportUploaded(long bytesSoFar, long? expectedTotal) => Reports.Add($"uploaded {bytesSoFar}");
    }

    /// <summary>
    /// Runs a transfer for <paramref name="path" />, writes the request expected and the one
    /// sent as diagnostics, and returns what was sent for the test to assert on.
    /// </summary>
    private async Task<string> SentForAsync(string path, string expected)
    {
        Diagnostics.Arrange("expected request", DiagnosticText.Escape(expected));
        string sent = Encoding.Latin1.GetString(await SentBytesForAsync(path));
        Diagnostics.Diff("request sent for " + path, expected, sent);
        return sent;
    }

    private async Task<byte[]> SentBytesForAsync(string path)
    {
        var connection = new ScriptedConnection();
        Diagnostics.Arrange("URL", "dict://h" + path);

        TransferResult result = await new DictProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(Context("dict://h" + path, new MemoryStream()));
        Diagnostics.Act("result", DiagnosticText.Result(result));
        Diagnostics.Act("request sent", DiagnosticText.Escape(Encoding.Latin1.GetString(connection.Sent)));

        return connection.Sent;
    }
}
