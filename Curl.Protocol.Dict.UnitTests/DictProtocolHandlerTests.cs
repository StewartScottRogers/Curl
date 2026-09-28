using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Dict.Fakes;

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

    [TestMethod]
    public void SupportedSchemes_IsDictOnly()
    {
        var handler = new DictProtocolHandler(new RecordingConnector(ConnectResult.Connected(new ScriptedConnection())));

        CollectionAssert.AreEqual(new[] { "dict" }, handler.SupportedSchemes.ToArray());
    }

    [TestMethod]
    public void Constructor_NullConnector_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new DictProtocolHandler(null!));
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws()
    {
        var handler = new DictProtocolHandler(new RecordingConnector(ConnectResult.Connected(new ScriptedConnection())));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await handler.ExecuteAsync(null!));
    }

    [TestMethod]
    public async Task ExecuteAsync_UrlWithoutPort_ConnectsToPort2628WithoutTls()
    {
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection()));

        await new DictProtocolHandler(connector).ExecuteAsync(Context("dict://h/d:x", new MemoryStream()));

        Assert.AreEqual(new ConnectTarget("h", 2628, false), connector.Targets.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_UrlWithPort_ConnectsToThatPort()
    {
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection()));

        await new DictProtocolHandler(connector).ExecuteAsync(Context("dict://h:1234/d:x", new MemoryStream()));

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

        await new DictProtocolHandler(connector).ExecuteAsync(context);

        Assert.AreEqual(new ConnectTarget("example.com", 2628, false) { Proxy = proxy }, connector.Targets.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_ContextWithoutProxy_ConnectsDirectly()
    {
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection()));

        await new DictProtocolHandler(connector).ExecuteAsync(Context("dict://example.com/d:x", new MemoryStream()));

        Assert.IsNull(connector.Targets.Single().Proxy);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_ReturnsTheFailureUnchangedAndWritesNothing()
    {
        var connector = new RecordingConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "m"));
        var output = new MemoryStream();

        TransferResult result = await new DictProtocolHandler(connector).ExecuteAsync(Context("dict://h/d:x", output));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("m", result.ErrorMessage);
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.AreEqual(0L, output.Length);
        Assert.IsFalse(result.IsConnectionRefused);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectRefused_ReturnsExit7MarkedConnectionRefused()
    {
        var connector = new RecordingConnector(ConnectResult.Refused("m"));

        TransferResult result = await new DictProtocolHandler(connector).ExecuteAsync(Context("dict://h/d:x", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    public async Task ExecuteAsync_DWord_SendsDefineInAnyDatabase()
    {
        Assert.AreEqual(Client + "DEFINE ! word\r\n" + Quit, await SentForAsync("/d:word"));
    }

    [TestMethod]
    public async Task ExecuteAsync_DWordDatabase_SendsDefineInThatDatabase()
    {
        Assert.AreEqual(Client + "DEFINE db word\r\n" + Quit, await SentForAsync("/d:word:db"));
    }

    [TestMethod]
    public async Task ExecuteAsync_LookupWord_SendsDefineInAnyDatabase()
    {
        Assert.AreEqual(Client + "DEFINE ! word\r\n" + Quit, await SentForAsync("/lookup:word"));
    }

    [TestMethod]
    public async Task ExecuteAsync_MWordDatabaseStrategy_SendsMatch()
    {
        Assert.AreEqual(Client + "MATCH db prefix word\r\n" + Quit, await SentForAsync("/m:word:db:prefix"));
    }

    [TestMethod]
    public async Task ExecuteAsync_FindWord_SendsMatchInAnyDatabaseWithDefaultStrategy()
    {
        Assert.AreEqual(Client + "MATCH ! . word\r\n" + Quit, await SentForAsync("/find:word"));
    }

    [TestMethod]
    public async Task ExecuteAsync_PlainWord_SendsTheWordAsTheCommand()
    {
        Assert.AreEqual(Client + "word\r\n" + Quit, await SentForAsync("/word"));
    }

    [TestMethod]
    public async Task ExecuteAsync_EncodedSpace_SendsTheDecodedCommand()
    {
        Assert.AreEqual(Client + "show db\r\n" + Quit, await SentForAsync("/show%20db"));
    }

    [TestMethod]
    public async Task ExecuteAsync_RootPath_SendsAnEmptyCommand()
    {
        Assert.AreEqual(Client + "\r\n" + Quit, await SentForAsync("/"));
    }

    [TestMethod]
    public async Task ExecuteAsync_DefineLongSpelling_SendsDefineAsDDoes()
    {
        Assert.AreEqual(Client + "DEFINE ! word\r\n" + Quit, await SentForAsync("/define:word"));
        Assert.AreEqual(Client + "DEFINE db word\r\n" + Quit, await SentForAsync("/define:word:db"));
    }

    [TestMethod]
    public async Task ExecuteAsync_MatchLongSpelling_SendsMatchAsMDoes()
    {
        Assert.AreEqual(Client + "MATCH ! . word\r\n" + Quit, await SentForAsync("/match:word"));
        Assert.AreEqual(Client + "MATCH db prefix word\r\n" + Quit, await SentForAsync("/match:word:db:prefix"));
    }

    [TestMethod]
    public async Task ExecuteAsync_PrefixInOtherCase_IsRecognised()
    {
        Assert.AreEqual(Client + "DEFINE ! word\r\n" + Quit, await SentForAsync("/DEFINE:word"));
        Assert.AreEqual(Client + "DEFINE ! word\r\n" + Quit, await SentForAsync("/Lookup:word"));
        Assert.AreEqual(Client + "MATCH ! . word\r\n" + Quit, await SentForAsync("/FIND:word"));
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyOrMissingFields_SendTheDefaults()
    {
        Assert.AreEqual(Client + "DEFINE ! default\r\n" + Quit, await SentForAsync("/d:"));
        Assert.AreEqual(Client + "MATCH ! . default\r\n" + Quit, await SentForAsync("/m:"));
        Assert.AreEqual(Client + "MATCH ! prefix x\r\n" + Quit, await SentForAsync("/m:x::prefix"));
        Assert.AreEqual(Client + "MATCH db . x\r\n" + Quit, await SentForAsync("/m:x:db:"));
        Assert.AreEqual(Client + "MATCH db . word\r\n" + Quit, await SentForAsync("/m:word:db"));
        Assert.AreEqual(Client + "DEFINE ! word\r\n" + Quit, await SentForAsync("/d:word:"));
    }

    [TestMethod]
    public async Task ExecuteAsync_FieldsBeyondTheLast_AreIgnored()
    {
        Assert.AreEqual(Client + "DEFINE db word\r\n" + Quit, await SentForAsync("/d:word:db:extra"));
        Assert.AreEqual(Client + "MATCH db s w\r\n" + Quit, await SentForAsync("/m:w:db:s:extra"));
    }

    [TestMethod]
    public async Task ExecuteAsync_EncodedColon_SeparatesFields()
    {
        Assert.AreEqual(Client + "DEFINE b a\r\n" + Quit, await SentForAsync("/d:a%3Ab"));
    }

    [TestMethod]
    public async Task ExecuteAsync_WordSpecials_AreSentWithABackslash()
    {
        Assert.AreEqual(Client + "DEFINE ! two\\ words\r\n" + Quit, await SentForAsync("/d:two%20words"));
        Assert.AreEqual(Client + "DEFINE ! a\\'b\r\n" + Quit, await SentForAsync("/d:a'b"));
        Assert.AreEqual(Client + "DEFINE ! a\\\"b\\\\c\r\n" + Quit, await SentForAsync("/d:a%22b%5Cc"));
        Assert.AreEqual(Client + "DEFINE ! a$b\r\n" + Quit, await SentForAsync("/d:a%24b"));
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

        CollectionAssert.AreEqual(expected, await SentBytesForAsync("/d:%C3%A9%7F"));
    }

    [TestMethod]
    public async Task ExecuteAsync_DatabaseAndStrategy_AreSentWithoutBackslashes()
    {
        Assert.AreEqual(Client + "DEFINE a b w\r\n" + Quit, await SentForAsync("/d:w:a%20b"));
        Assert.AreEqual(Client + "MATCH a\"b s t w\r\n" + Quit, await SentForAsync("/m:w:a%22b:s%20t"));
    }

    [TestMethod]
    public async Task ExecuteAsync_OtherCommand_SendsColonsAsSpaces()
    {
        Assert.AreEqual(Client + "show db x\r\n" + Quit, await SentForAsync("/show:db:x"));
        Assert.AreEqual(Client + "dx word\r\n" + Quit, await SentForAsync("/dx:word"));
        Assert.AreEqual(Client + "d\r\n" + Quit, await SentForAsync("/d"));
    }

    [TestMethod]
    public async Task ExecuteAsync_QueryAndFragment_AreNotSent()
    {
        Assert.AreEqual(Client + "DEFINE ! word\r\n" + Quit, await SentForAsync("/d:word?q=1"));
        Assert.AreEqual(Client + "DEFINE ! word\r\n" + Quit, await SentForAsync("/d:word#frag"));
    }

    [TestMethod]
    [DataRow("/d:a\\b", "DEFINE ! a/b")]
    [DataRow("/d:x/../y", "y")]
    [DataRow("/a/%2e%2e/d:x", "DEFINE ! x")]
    [DataRow("/d:x/./y", "DEFINE ! x/y")]
    public async Task ExecuteAsync_BackslashOrDotSegments_AreNormalisedAsCurlDoes(string path, string command)
    {
        Assert.AreEqual(Client + command + "\r\n" + Quit, await SentForAsync(path));
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

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await new DictProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection))).ExecuteAsync(context));

        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_PercentNotFollowedByTwoHexDigits_IsSentAsIs()
    {
        Assert.AreEqual(Client + "DEFINE ! %zz\r\n" + Quit, await SentForAsync("/d:%zz"));
        Assert.AreEqual(Client + "DEFINE ! a%a\r\n" + Quit, await SentForAsync("/d:a%a"));
        Assert.AreEqual(Client + "DEFINE ! a%\r\n" + Quit, await SentForAsync("/d:a%"));
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerReply_IsWrittenByteForByte()
    {
        byte[] reply = Encoding.ASCII.GetBytes(Reply);
        var connection = new ScriptedConnection(reply[..10], reply[10..]);
        var output = new MemoryStream();

        TransferResult result = await new DictProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(Context("dict://h/d:word", output));

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

        TransferResult result = await new DictProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(Context("dict://h/d:word", output));

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

        TransferResult result = await new DictProtocolHandler(connector).ExecuteAsync(Context("dict://h" + path, output));

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

        await new DictProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection))).ExecuteAsync(context);

        CollectionAssert.AreEqual(new[] { "started", "downloaded 3", "downloaded 7" }, progress.Reports);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFailed_ReportsNothing()
    {
        RecordingProgress progress = new();
        var context = new TransferContext { Url = CurlUrl.Parse("dict://h/d:x"), Output = new MemoryStream(), Progress = progress };

        await new DictProtocolHandler(new RecordingConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "refused"))).ExecuteAsync(context);

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

    private static async Task<string> SentForAsync(string path) =>
        Encoding.Latin1.GetString(await SentBytesForAsync(path));

    private static async Task<byte[]> SentBytesForAsync(string path)
    {
        var connection = new ScriptedConnection();

        await new DictProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(Context("dict://h" + path, new MemoryStream()));

        return connection.Sent;
    }
}
