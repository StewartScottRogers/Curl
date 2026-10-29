using System.Text;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.HttpCache;

namespace Curl.Extension.HttpCache.UnitTests;

[TestClass]
public sealed class HttpCacheTransferHookTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private string _directory = null!;

    [TestInitialize]
    public void CreateDirectory() => _directory = Path.Combine(Path.GetTempPath(), "httpcache-" + Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void DeleteDirectory()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_AFreshStoredResponse_IsAnsweredWithoutContactingTheOrigin_Rfc9111Section4_2()
    {
        await RunAsync(Noon, Origin(200, "OK", "fresh body", "Cache-Control: max-age=60").Invoke);
        var later = Context(Noon.AddSeconds(30));
        var contacted = false;

        var result = await Hook().ExecuteAsync(later, _ =>
        {
            contacted = true;
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        Assert.IsFalse(contacted);
        Assert.AreEqual("fresh body", Text(later.Output));
        Assert.AreEqual("HTTP/1.1 200 OK\r\nCache-Control: max-age=60\r\n\r\n", Text(later.HeaderOutput!));
        Assert.AreEqual("HTTP/1.1 200 OK\r\nCache-Control: max-age=60\r\n\r\n", Text(later.DumpHeaderOutput!));
        Assert.AreEqual(10, result.BytesTransferred);
        Assert.AreEqual(200, result.Report!.ResponseCode);
        Assert.AreEqual("GET", result.Report.Method);
        Assert.AreEqual("http://example.com/r", result.Report.EffectiveUrl);
        Assert.IsNull(result.Report.ContentType);
    }

    [TestMethod]
    public async Task ExecuteAsync_AFirstFetch_WritesTheResponseAsReceived_Rfc9111Section3()
    {
        var context = Context(Noon);

        var result = await Hook().ExecuteAsync(context, Origin(200, "OK", "body", "Content-Type: text/plain", "Cache-Control: max-age=60").Invoke);

        Assert.AreEqual("body", Text(context.Output));
        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nCache-Control: max-age=60\r\n\r\n", Text(context.HeaderOutput!));
        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nCache-Control: max-age=60\r\n\r\n", Text(context.DumpHeaderOutput!));
        Assert.AreEqual(200, result.Report!.ResponseCode);
        var fresh = Context(Noon.AddSeconds(1));
        var answered = await Hook().ExecuteAsync(fresh, NeverCalled);
        Assert.AreEqual("text/plain", answered.Report!.ContentType);
    }

    [TestMethod]
    public async Task ExecuteAsync_AStaleStoredResponse_IsRevalidatedWithBothValidatorsAndA304SelectsIt_Rfc9111Sections4_3_1And4_3_4()
    {
        await RunAsync(Noon, Origin(200, "OK", "stored body", "ETag: \"v1\"", "Last-Modified: Wed, 28 Oct 2026 12:00:00 GMT", "Cache-Control: max-age=10", "X-Old: 1").Invoke);
        var later = Context(Noon.AddMinutes(5));
        var origin = Origin(304, "Not Modified", string.Empty, "ETag: W/\"v1\"", "X-Old: 2");

        var result = await Hook().ExecuteAsync(later, origin.Invoke);

        CollectionAssert.AreEqual(
            new[] { "If-None-Match: \"v1\"", "If-Modified-Since: Wed, 28 Oct 2026 12:00:00 GMT" },
            origin.Sent!.Http!.Headers.ToArray());
        Assert.AreEqual("stored body", Text(later.Output));
        StringAssert.StartsWith(Text(later.HeaderOutput!), "HTTP/1.1 200 OK\r\n");
        StringAssert.Contains(Text(later.HeaderOutput!), "X-Old: 2\r\n");
        Assert.AreEqual(200, result.Report!.ResponseCode);
        Assert.AreEqual(11, result.BytesTransferred);
        var refreshed = Context(Noon.AddMinutes(5).AddSeconds(5));
        await Hook().ExecuteAsync(refreshed, NeverCalled);
        StringAssert.Contains(Text(refreshed.HeaderOutput!), "X-Old: 2\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_A304ThatDoesNotSelectTheStoredResponse_IsWrittenAsReceived_Rfc9111Section4_3_3()
    {
        await RunAsync(Noon, Origin(200, "OK", "stored", "ETag: \"v1\"", "Cache-Control: max-age=0").Invoke);
        var later = Context(Noon.AddMinutes(1));

        var result = await Hook().ExecuteAsync(later, Origin(304, "Not Modified", string.Empty, "ETag: \"v2\"").Invoke);

        Assert.AreEqual(304, result.Report!.ResponseCode);
        Assert.AreEqual(string.Empty, Text(later.Output));
        Assert.AreEqual("HTTP/1.1 304 Not Modified\r\nETag: \"v2\"\r\n\r\n", Text(later.HeaderOutput!));
    }

    [TestMethod]
    public async Task ExecuteAsync_AFailedRevalidation_IsReturnedAsCurlsHandlerGaveIt()
    {
        await RunAsync(Noon, Origin(200, "OK", "stored", "ETag: \"v1\"", "Cache-Control: max-age=0").Invoke);
        var failure = TransferResult.Failure(CurlExitCode.CouldntConnect, "refused");

        var result = await Hook().ExecuteAsync(Context(Noon.AddMinutes(1)), _ => ValueTask.FromResult(failure));

        Assert.AreSame(failure, result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ARequestWithNoCache_RevalidatesAFreshStoredResponse_Rfc9111Section5_2_1_4()
    {
        await RunAsync(Noon, Origin(200, "OK", "stored", "ETag: \"v1\"", "Cache-Control: max-age=600").Invoke);
        var origin = Origin(200, "OK", "new", "Cache-Control: max-age=600");
        var context = Context(Noon.AddSeconds(1), new HttpRequestOptions { Headers = ["Cache-Control: no-cache"] });

        await Hook().ExecuteAsync(context, origin.Invoke);

        Assert.IsNotNull(origin.Sent);
        CollectionAssert.Contains(origin.Sent.Http!.Headers.ToArray(), "If-None-Match: \"v1\"");
        Assert.AreEqual("new", Text(context.Output));
    }

    [TestMethod]
    [DataRow("Cache-Control: no-store", DisplayName = "no-store, §5.2.2.5")]
    [DataRow("Vary: Accept", DisplayName = "Vary, §4.1")]
    public async Task ExecuteAsync_AResponseTheCacheMustNotReuse_IsNotStored(string field)
    {
        await RunAsync(Noon, Origin(200, "OK", "body", "Cache-Control: max-age=60", field).Invoke);
        var origin = Origin(200, "OK", "again");

        await Hook().ExecuteAsync(Context(Noon.AddSeconds(1)), origin.Invoke);

        Assert.IsNotNull(origin.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_AFailedTransfer_IsNotStored()
    {
        await RunAsync(Noon, _ => ValueTask.FromResult(TransferResult.Failure(CurlExitCode.HttpReturnedError, "error")));
        await RunAsync(Noon, _ => ValueTask.FromResult(TransferResult.Success(0)));

        Assert.IsFalse(Directory.Exists(_directory));
    }

    [TestMethod]
    public async Task ExecuteAsync_ACacheDirectoryThatCannotBeWritten_LeavesTheTransferAsItWas()
    {
        await File.WriteAllTextAsync(_directory, "a file, not a directory");
        try
        {
            var context = Context(Noon);
            var result = await Hook().ExecuteAsync(context, Origin(200, "OK", "body", "Cache-Control: max-age=60").Invoke);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("body", Text(context.Output));
        }
        finally
        {
            File.Delete(_directory);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutHeaderOutputs_WritesOnlyTheBody()
    {
        var context = Context(Noon, headerOutputs: false);
        var origin = Origin(200, "OK", "body", "Cache-Control: max-age=60");

        await Hook().ExecuteAsync(context, origin.Invoke);

        Assert.IsNull(origin.Sent!.DumpHeaderOutput);
        Assert.AreEqual("body", Text(context.Output));
        var fresh = Context(Noon.AddSeconds(1), headerOutputs: false);
        await Hook().ExecuteAsync(fresh, NeverCalled);
        Assert.AreEqual("body", Text(fresh.Output));
    }

    [TestMethod]
    [DataRow("ftp://example.com/r", false, DisplayName = "not HTTP")]
    [DataRow("http://example.com/r", true, DisplayName = "a custom method")]
    public async Task ExecuteAsync_ARequestTheCacheDoesNotHandle_RunsUnchanged(string url, bool http)
    {
        var context = Context(Noon, http ? new HttpRequestOptions { CustomMethod = "DELETE" } : null, url);
        ITransferContext? sent = null;

        await Hook().ExecuteAsync(context, given =>
        {
            sent = given;
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        Assert.AreSame(context, sent);
        Assert.IsFalse(Directory.Exists(_directory));
    }

    [TestMethod]
    public void TargetUriOf_APlainGet_GivesTheUriWithItsPortAndQuery_Rfc9111Section2()
    {
        var context = Context(Noon, url: "http://example.com:8080/a?b=c#d");

        Assert.AreEqual(new Uri("http://example.com:8080/a?b=c"), CacheableRequest.TargetUriOf(context));
    }

    [TestMethod]
    public void TargetUriOf_ARequestForPartOfTheResourceOrWithABody_GivesNone()
    {
        Assert.IsNull(CacheableRequest.TargetUriOf(Context(Noon, new HttpRequestOptions { Body = new BytesBody(new byte[] { 1 }, "text/plain") })));
        var head = Context(Noon, noBody: true);
        Assert.IsNull(CacheableRequest.TargetUriOf(head));
        Assert.IsNull(CacheableRequest.TargetUriOf(Bare(postData: new byte[] { 1 })));
        Assert.IsNull(CacheableRequest.TargetUriOf(Bare(postWithoutData: true)));
        Assert.IsNull(CacheableRequest.TargetUriOf(Bare(upload: new MemoryStream())));
        Assert.IsNull(CacheableRequest.TargetUriOf(Bare(resumeFrom: 10)));
        Assert.IsNotNull(CacheableRequest.TargetUriOf(Bare()));
    }

    [TestMethod]
    public void HeaderFieldsOf_SkipsLinesWithoutAColon()
    {
        var fields = CacheableRequest.HeaderFieldsOf(new HttpRequestOptions { Headers = ["Accept: text/html", "X-Removed;"] });

        Assert.HasCount(1, fields);
        Assert.AreEqual(new KeyValuePair<string, string>("Accept", "text/html"), fields[0]);
    }

    [TestMethod]
    public void StatusLineIn_HeadersWithoutAStatusLine_GivesHttp11AndTheCode()
    {
        Assert.AreEqual("HTTP/1.1 204", StoredResponseOutput.StatusLineIn([], 204));
        Assert.AreEqual("HTTP/2 200", StoredResponseOutput.StatusLineIn(Encoding.Latin1.GetBytes("HTTP/1.1 301 Moved\r\n\r\nHTTP/2 200\r\n\r\n"), 200));
    }

    private static ValueTask<TransferResult> NeverCalled(ITransferContext context) => throw new AssertFailedException("The origin was contacted.");

    private static string Text(Stream stream) => Encoding.Latin1.GetString(((MemoryStream)stream).ToArray());

    private static OriginServer Origin(int code, string reason, string body, params string[] fields) => new(code, reason, body, fields);

    private ITransferHook Hook() => HttpCacheExtension.HookFor(new(new Dictionary<string, string> { [HttpCacheExtension.Option.Name] = _directory }))!;

    private async Task RunAsync(DateTimeOffset now, Func<ITransferContext, ValueTask<TransferResult>> origin) =>
        await Hook().ExecuteAsync(Context(now), origin);

    private static TransferContext Bare(ReadOnlyMemory<byte>? postData = null, bool postWithoutData = false, Stream? upload = null, long? resumeFrom = null) => new()
    {
        Url = CurlUrl.Parse("http://example.com/r"),
        Output = new MemoryStream(),
        Http = new HttpRequestOptions(),
        PostData = postData,
        PostWithoutData = postWithoutData,
        Upload = upload,
        ResumeFrom = resumeFrom,
    };

    private static TransferContext Context(DateTimeOffset now, HttpRequestOptions? http = null, string url = "http://example.com/r", bool headerOutputs = true, bool noBody = false) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = new MemoryStream(),
        HeaderOutput = headerOutputs ? new MemoryStream() : null,
        DumpHeaderOutput = headerOutputs ? new MemoryStream() : null,
        Http = url.StartsWith("http", StringComparison.Ordinal) ? http ?? new HttpRequestOptions() : null,
        NoBody = noBody,
        TimeProvider = new FixedTimeProvider(now),
    };

    /// <summary>Stands in for curl's handler: writes one recorded response to the outputs it is given.</summary>
    private sealed class OriginServer(int code, string reason, string body, string[] fields)
    {
        public ITransferContext? Sent { get; private set; }

        public async ValueTask<TransferResult> Invoke(ITransferContext context)
        {
            Sent = context;
            var head = Encoding.Latin1.GetBytes($"HTTP/1.1 {code} {reason}\r\n" + string.Concat(fields.Select(field => field + "\r\n")) + "\r\n");
            await context.HeaderOutput!.WriteAsync(head);
            if (context.DumpHeaderOutput is { } dump)
            {
                await dump.WriteAsync(head);
            }

            await context.Output.WriteAsync(Encoding.Latin1.GetBytes(body));
            return TransferResult.Success(body.Length) with
            {
                Report = new TransferReport
                {
                    ResponseCode = code,
                    ResponseHeaders = [.. fields.Select(field => field.Split(": ", 2)).Select(parts => new KeyValuePair<string, string>(parts[0], parts[1]))],
                },
            };
        }
    }
}
