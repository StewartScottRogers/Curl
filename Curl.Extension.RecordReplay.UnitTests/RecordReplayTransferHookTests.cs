using System.Text;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.RecordReplay.UnitTests;

[TestClass]
public sealed class RecordReplayTransferHookTests
{
    private const string Head = "HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nX-A: 1\r\n\r\n";

    private string _directory = null!;

    [TestInitialize]
    public void CreatePath() => _directory = Path.Combine(Path.GetTempPath(), "recordreplay-" + Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void DeleteDirectory()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_Record_WritesTheResponseAsReceivedAndRecordsIt_Adr1129Section4()
    {
        var context = Context();
        var server = new FakeServer(Head, "body");

        var result = await Hook(record: _directory).ExecuteAsync(context, server.Invoke);

        Assert.AreEqual("body", Text(context.Output));
        Assert.AreEqual(Head, Text(context.HeaderOutput!));
        Assert.AreEqual(Head, Text(context.DumpHeaderOutput!));
        Assert.AreEqual(200, result.Report!.ResponseCode);
        var file = Directory.GetFiles(_directory).Single();
        Assert.AreEqual(ExchangeKey.For("GET", "http://example.com/r", default) + ".exchange", Path.GetFileName(file));
        Assert.AreEqual("CURL-EXCHANGE 1\nGET http://example.com/r\n200 " + Head.Length + " 4\n" + Head + "body", Encoding.Latin1.GetString(File.ReadAllBytes(file)));
    }

    [TestMethod]
    public async Task ExecuteAsync_Replay_AnswersFromTheRecordingWithoutTheServer_Adr1129Section5()
    {
        await Hook(record: _directory).ExecuteAsync(Context(), new FakeServer(Head, "body").Invoke);
        var context = Context();

        var result = await Hook(replay: _directory).ExecuteAsync(context, NeverCalled);

        Assert.AreEqual("body", Text(context.Output));
        Assert.AreEqual(Head, Text(context.HeaderOutput!));
        Assert.AreEqual(Head, Text(context.DumpHeaderOutput!));
        Assert.AreEqual(4, result.BytesTransferred);
        Assert.AreEqual(200, result.Report!.ResponseCode);
        Assert.AreEqual("GET", result.Report.Method);
        Assert.AreEqual("http://example.com/r", result.Report.EffectiveUrl);
        Assert.AreEqual("text/plain", result.Report.ContentType);
        CollectionAssert.AreEqual(
            new[] { new KeyValuePair<string, string>("Content-Type", "text/plain"), new KeyValuePair<string, string>("X-A", "1") },
            result.Report.ResponseHeaders.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplayWithoutHeaderOutputs_WritesOnlyTheBody_Adr1129Section5()
    {
        await Hook(record: _directory).ExecuteAsync(Context(headerOutputs: false), new FakeServer("HTTP/1.1 204 No Content\r\n\r\n", string.Empty).Invoke);
        var context = Context(headerOutputs: false);

        var result = await Hook(replay: _directory).ExecuteAsync(context, NeverCalled);

        Assert.AreEqual(204, result.Report!.ResponseCode);
        Assert.IsNull(result.Report.ContentType);
        Assert.AreEqual(string.Empty, Text(context.Output));
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplayOfARequestNotRecorded_FailsWithExit7_Adr1129Section5()
    {
        await Hook(record: _directory).ExecuteAsync(Context(), new FakeServer(Head, "body").Invoke);
        var context = Context(postData: "a=1"u8.ToArray());

        var result = await Hook(replay: _directory).ExecuteAsync(context, NeverCalled);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual($"No recorded exchange for POST http://example.com/r in {_directory}", result.ErrorMessage);
        Assert.AreEqual(0, context.Output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplayAndRecord_FetchesAndRecordsWhatIsMissing_Adr1129Section5()
    {
        var recordings = Path.Combine(_directory, "new");
        var server = new FakeServer(Head, "body");

        await Hook(replay: _directory, record: recordings).ExecuteAsync(Context(), server.Invoke);
        var replayed = Context();
        await Hook(replay: recordings, record: Path.Combine(_directory, "other")).ExecuteAsync(replayed, NeverCalled);

        Assert.AreEqual(1, server.Calls);
        Assert.AreEqual("body", Text(replayed.Output));
        Assert.IsFalse(Directory.Exists(Path.Combine(_directory, "other")));
    }

    [TestMethod]
    public async Task ExecuteAsync_RecordOfAFailedOrUnreportedTransfer_RecordsNothing_Adr1129Section4()
    {
        await Hook(record: _directory).ExecuteAsync(Context(), _ => ValueTask.FromResult(TransferResult.Failure(CurlExitCode.CouldntConnect, "refused")));
        await Hook(record: _directory).ExecuteAsync(Context(), _ => ValueTask.FromResult(TransferResult.Success(0)));

        Assert.IsFalse(Directory.Exists(_directory));
    }

    [TestMethod]
    public async Task ExecuteAsync_ARecordDirectoryThatCannotBeWritten_FailsWithExit23NamingRecordAndTheDirectory_Ext095()
    {
        await File.WriteAllTextAsync(_directory, "a file, not a directory");
        var context = Context();

        var result = await Hook(record: _directory).ExecuteAsync(context, new FakeServer(Head, "body").Invoke);

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        StringAssert.Contains(result.ErrorMessage!, "--record");
        StringAssert.Contains(result.ErrorMessage!, _directory);
        Assert.AreEqual("body", Text(context.Output));
        File.Delete(_directory);
    }

    [TestMethod]
    public async Task ExecuteAsync_ARecordDirectoryThatDeniesAccess_FailsWithExit23NamingRecordAndTheDirectory_Bl5522()
    {
        var context = Context();
        var hook = new RecordReplayTransferHook(null, new AccessDeniedDirectory(_directory));

        var result = await hook.ExecuteAsync(context, new FakeServer(Head, "body").Invoke);

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        StringAssert.Contains(result.ErrorMessage!, "--record");
        StringAssert.Contains(result.ErrorMessage!, _directory);
        Assert.AreEqual("body", Text(context.Output));
    }

    private sealed class AccessDeniedDirectory(string path) : ExchangeDirectory(path)
    {
        public override Task WriteAsync(string key, RecordedExchange exchange, CancellationToken cancellationToken) =>
            throw new UnauthorizedAccessException("Access to the path is denied.");
    }

    [TestMethod]
    public async Task ExecuteAsync_ATransferThatIsNotHttp_RunsAsWithoutTheHook_Adr1129Section4()
    {
        var context = new TransferContext { Url = CurlUrl.Parse("ftp://example.com/r"), Output = new MemoryStream() };
        ITransferContext? sent = null;

        await Hook(replay: _directory).ExecuteAsync(context, given =>
        {
            sent = given;
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        Assert.AreSame(context, sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplayOfA302_ReportsTheLocationResolvedAgainstTheRequestUrl_Adr1135Section5()
    {
        await Hook(record: _directory).ExecuteAsync(Context(), new FakeServer("HTTP/1.1 302 Found\r\nLocation: /plain\r\n\r\n", string.Empty).Invoke);
        await Hook(record: _directory).ExecuteAsync(Context("http://example.com/plain"), new FakeServer(Head, "hello").Invoke);

        var first = await Hook(replay: _directory).ExecuteAsync(Context(), NeverCalled);
        var second = await Hook(replay: _directory).ExecuteAsync(Context(first.Report!.RedirectUrl!), NeverCalled);

        Assert.AreEqual(302, first.Report.ResponseCode);
        Assert.AreEqual("http://example.com/plain", first.Report.RedirectUrl);
        Assert.AreEqual(200, second.Report!.ResponseCode);
        Assert.IsNull(second.Report.RedirectUrl);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplayOfARedirectWhoseTargetIsNotRecorded_FailsWithExit7_Adr1135Section5()
    {
        await Hook(record: _directory).ExecuteAsync(Context(), new FakeServer("HTTP/1.1 302 Found\r\nLocation: /gone\r\n\r\n", string.Empty).Invoke);
        var first = await Hook(replay: _directory).ExecuteAsync(Context(), NeverCalled);

        var second = await Hook(replay: _directory).ExecuteAsync(Context(first.Report!.RedirectUrl!), NeverCalled);

        Assert.AreEqual(CurlExitCode.CouldntConnect, second.ExitCode);
        Assert.AreEqual($"No recorded exchange for GET http://example.com/gone in {_directory}", second.ErrorMessage);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 302 Found\r\n\r\n")]
    [DataRow("HTTP/1.1 302 Found\r\nLocation: http://[bad\r\n\r\n")]
    [DataRow("HTTP/1.1 200 OK\r\nLocation: /plain\r\n\r\n")]
    public async Task ExecuteAsync_ReplayWithoutAUsableRedirect_ReportsNoRedirectUrl_Adr1135Section5(string head)
    {
        await Hook(record: _directory).ExecuteAsync(Context(), new FakeServer(head, string.Empty).Invoke);

        var result = await Hook(replay: _directory).ExecuteAsync(Context(), NeverCalled);

        Assert.IsNull(result.Report!.RedirectUrl);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplayOfAnExchangeWhoseRecordedUrlIsNotAbsolute_ReportsNoRedirectUrl_Adr1135Section5()
    {
        var key = ExchangeKey.For("GET", "http://example.com/r", default);
        Directory.CreateDirectory(_directory);
        const string recordedHead = "HTTP/1.1 302 Found\r\nLocation: /plain\r\n\r\n";
        await File.WriteAllBytesAsync(
            Path.Combine(_directory, key + ".exchange"),
            Encoding.Latin1.GetBytes("CURL-EXCHANGE 1\nGET relative\n302 " + recordedHead.Length + " 0\n" + recordedHead));

        var result = await Hook(replay: _directory).ExecuteAsync(Context(), NeverCalled);

        Assert.AreEqual(302, result.Report!.ResponseCode);
        Assert.IsNull(result.Report.RedirectUrl);
    }

    [TestMethod]
    public async Task ExecuteAsync_RecordAUrlWithUserInfo_WritesNoPasswordAndReplayStillFindsIt_Adr1135()
    {
        const string url = "http://bob:URLSECRET5@example.com/r";
        await Hook(record: _directory).ExecuteAsync(Context(url), new FakeServer(Head, "body").Invoke);
        var context = Context(url);

        var result = await Hook(replay: _directory).ExecuteAsync(context, NeverCalled);

        var file = Directory.GetFiles(_directory).Single();
        var text = Encoding.Latin1.GetString(File.ReadAllBytes(file));
        Assert.DoesNotContain("URLSECRET5", text);
        Assert.DoesNotContain("URLSECRET5", Path.GetFileName(file));
        Assert.StartsWith("CURL-EXCHANGE 1\nGET http://REDACTED:REDACTED@example.com/r\n", text);
        Assert.AreEqual("body", Text(context.Output));
        Assert.AreEqual("http://REDACTED:REDACTED@example.com/r", result.Report!.EffectiveUrl);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplayAMissingExchangeForAUrlWithUserInfo_FailsWithoutThePassword_Adr1135()
    {
        var result = await Hook(replay: _directory).ExecuteAsync(Context("http://bob:URLSECRET5@example.com/r"), NeverCalled);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.DoesNotContain("URLSECRET5", result.ErrorMessage ?? string.Empty);
    }

    [TestMethod]
    public async Task ExecuteAsync_RecordAUrlWithoutUserInfo_WritesItUnchanged_Adr1135()
    {
        await Hook(record: _directory).ExecuteAsync(Context("http://example.com/r?a=b@c"), new FakeServer(Head, "body").Invoke);

        var text = Encoding.Latin1.GetString(File.ReadAllBytes(Directory.GetFiles(_directory).Single()));
        Assert.StartsWith("CURL-EXCHANGE 1\nGET http://example.com/r?a=b@c\n", text);
    }

    [TestMethod]
    [DataRow("http://u:p@example.com", "http://REDACTED:REDACTED@example.com")]
    [DataRow("example.com/u@v", "example.com/u@v")]
    [DataRow("http://example.com", "http://example.com")]
    [DataRow("x", "x")]
    [DataRow("", "")]
    [DataRow("user:secret@example.com/path", "REDACTED:REDACTED@example.com/path")]
    [DataRow("example.com/?u=http://a@b/", "example.com/?u=http://a@b/")]
    public void Redacted_AUrl_ReplacesOnlyTheUserInfo(string url, string expected) =>
        Assert.AreEqual(expected, RecordReplayTransferHook.Redacted(url));

    [TestMethod]
    [DataRow("HTTP/1.1 302 Found\r\nLocation: http://u:S1@h/p\r\nContent-Location: http://cu:S2@h/x\r\n\r\n", "HTTP/1.1 302 Found\r\nLocation: http://REDACTED:REDACTED@h/p\r\nContent-Location: http://REDACTED:REDACTED@h/x\r\n\r\n")]
    [DataRow("Location: http://u6:S3@[::1]:80/p\r\n", "Location: http://REDACTED:REDACTED@[::1]:80/p\r\n")]
    [DataRow("link: <http://a:b@h/x>; rel=next\r\nRefresh: 0; url=http://a:b@h/y\r\n", "link: <http://REDACTED:REDACTED@h/x>; rel=next\r\nRefresh: 0; url=http://REDACTED:REDACTED@h/y\r\n")]
    [DataRow("Location: /plain?a=b@c\r\nX-Other: http://u:p@h/\r\n", "Location: /plain?a=b@c\r\nX-Other: http://REDACTED:REDACTED@h/\r\n")]
    [DataRow("HTTP/1.1 200 OK\r\nWWW-Authenticate: Bearer realm=\"http://wu:S@h/r\"\r\nX-Upstream: https://xu:S@h/q\r\n", "HTTP/1.1 200 OK\r\nWWW-Authenticate: Bearer realm=\"http://REDACTED:REDACTED@h/r\"\r\nX-Upstream: https://REDACTED:REDACTED@h/q\r\n")]
    public void RedactedHead_UrlFieldsWithUserInfo_ReplacesOnlyTheirUserInfo_Ext090(string head, string expected) =>
        Assert.AreEqual(expected, Encoding.Latin1.GetString(RecordReplayTransferHook.RedactedHead(Encoding.Latin1.GetBytes(head))));

    private static ValueTask<TransferResult> NeverCalled(ITransferContext context) => throw new AssertFailedException("The server was contacted.");

    private static string Text(Stream stream) => Encoding.Latin1.GetString(((MemoryStream)stream).ToArray());

    private static ITransferHook Hook(string? replay = null, string? record = null)
    {
        var values = new Dictionary<string, string>();
        if (replay is not null)
        {
            values[RecordReplayExtension.ReplayOption.Name] = replay;
        }

        if (record is not null)
        {
            values[RecordReplayExtension.RecordOption.Name] = record;
        }

        return RecordReplayExtension.HookFor(new(values))!;
    }

    private static TransferContext Context(string url = "http://example.com/r", bool headerOutputs = true, ReadOnlyMemory<byte>? postData = null) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = new MemoryStream(),
        HeaderOutput = headerOutputs ? new MemoryStream() : null,
        DumpHeaderOutput = headerOutputs ? new MemoryStream() : null,
        PostData = postData,
        Http = new HttpRequestOptions(),
    };

    /// <summary>Stands in for curl's handler: writes one response to the outputs it is given.</summary>
    private sealed class FakeServer(string head, string body)
    {
        public int Calls { get; private set; }

        public async ValueTask<TransferResult> Invoke(ITransferContext context)
        {
            Calls++;
            var headBytes = Encoding.Latin1.GetBytes(head);
            await context.HeaderOutput!.WriteAsync(headBytes);
            if (context.DumpHeaderOutput is { } dump)
            {
                await dump.WriteAsync(headBytes);
            }

            await context.Output.WriteAsync(Encoding.Latin1.GetBytes(body));
            var code = int.Parse(head.Split('\n').Last(line => line.StartsWith("HTTP/", StringComparison.Ordinal)).Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture);
            return TransferResult.Success(body.Length) with { Report = new TransferReport { ResponseCode = code } };
        }
    }
}
