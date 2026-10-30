using System.Net;
using System.Text;
using System.Text.Json;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.Har.UnitTests;

[TestClass]
public sealed class HarTransferHookTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 30, 15, 250, TimeSpan.FromHours(1));

    private string _directory = null!;

    private string _path = null!;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Directory.CreateTempSubdirectory("har-").FullName;
        _path = Path.Combine(_directory, "out.har");
    }

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, true);

    [TestMethod]
    public async Task ExecuteAsync_AnHttpTransfer_PassesTheBodyAndResultThroughAndWritesALogVersion12_Har12Log()
    {
        var context = Context();
        var origin = Origin(200, "hello", [("Content-Type", "text/plain")]);

        var result = await new HarTransferHook(_path).ExecuteAsync(context, origin);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("hello", Encoding.ASCII.GetString(((MemoryStream)context.Output).ToArray()));
        using var har = Read();
        var log = har.RootElement.GetProperty("log");
        Assert.AreEqual("1.2", log.GetProperty("version").GetString());
        Assert.AreEqual("curl", log.GetProperty("creator").GetProperty("name").GetString());
        Assert.IsFalse(string.IsNullOrEmpty(log.GetProperty("creator").GetProperty("version").GetString()));
        Assert.AreEqual(1, log.GetProperty("entries").GetArrayLength());
    }

    [TestMethod]
    public async Task ExecuteAsync_AnHttpTransfer_WritesTheEntrysStartTimeTimeCacheAndServerAddress_Har12Entries()
    {
        var entry = await EntryOfAsync(Context(), Origin(200, "hello"));

        Assert.AreEqual("2026-10-29T11:30:15.250Z", entry.GetProperty("startedDateTime").GetString());
        Assert.AreEqual(30, entry.GetProperty("time").GetDouble());
        Assert.AreEqual(JsonValueKind.Object, entry.GetProperty("cache").ValueKind);
        Assert.AreEqual(20, entry.GetProperty("timings").GetProperty("wait").GetDouble());
        Assert.AreEqual("192.0.2.7", entry.GetProperty("serverIPAddress").GetString());
    }

    [TestMethod]
    public async Task ExecuteAsync_ADataBodyWithHeaders_WritesTheRequestsMethodUrlHeadersQueryAndPostData_Har12Request()
    {
        var http = new HttpRequestOptions
        {
            UserAgent = "curl/8.17.0",
            Referer = "http://example.com/from",
            Headers = ["X-One: 1", "Accept:", "X-Empty;", "referer: http://other/", "NoColon"],
            Body = new BytesBody(Encoding.UTF8.GetBytes("a=b"), "application/x-www-form-urlencoded"),
        };

        var request = (await EntryOfAsync(Context(http, "http://example.com/p?q=a+b&x=%41&flag#frag"), Origin(200, string.Empty, effectiveUrl: null))).GetProperty("request");

        Assert.AreEqual("POST", request.GetProperty("method").GetString());
        Assert.AreEqual("http://example.com/p?q=a+b&x=%41&flag#frag", request.GetProperty("url").GetString());
        Assert.AreEqual("HTTP/1.1", request.GetProperty("httpVersion").GetString());
        Assert.AreEqual(0, request.GetProperty("cookies").GetArrayLength());
        CollectionAssert.AreEqual(new[] { "User-Agent: curl/8.17.0", "X-One: 1", "referer: http://other/" }, NameValues(request.GetProperty("headers")));
        CollectionAssert.AreEqual(new[] { "q: a b", "x: A", "flag: " }, NameValues(request.GetProperty("queryString")));
        Assert.AreEqual("application/x-www-form-urlencoded", request.GetProperty("postData").GetProperty("mimeType").GetString());
        Assert.AreEqual("a=b", request.GetProperty("postData").GetProperty("text").GetString());
        Assert.AreEqual(-1, request.GetProperty("headersSize").GetInt32());
        Assert.AreEqual(3, request.GetProperty("bodySize").GetInt32());
    }

    [TestMethod]
    public async Task ExecuteAsync_TheReportedMethod_WinsOverTheOneTheOptionsImply()
    {
        var request = (await EntryOfAsync(Context(new HttpRequestOptions { CustomMethod = "PATCH" }), Origin(200, string.Empty, method: "DELETE"))).GetProperty("request");

        Assert.AreEqual("DELETE", request.GetProperty("method").GetString());
        Assert.IsFalse(request.TryGetProperty("postData", out _));
        Assert.AreEqual(0, request.GetProperty("bodySize").GetInt32());
    }

    [TestMethod]
    public async Task ExecuteAsync_NoReportedMethod_TakesItFromTheOptions()
    {
        Assert.AreEqual("PATCH", await MethodOfAsync(Context(new HttpRequestOptions { CustomMethod = "PATCH" })));
        Assert.AreEqual("HEAD", await MethodOfAsync(Context(noBody: true)));
        Assert.AreEqual("PUT", await MethodOfAsync(Context(upload: new MemoryStream())));
        Assert.AreEqual("GET", await MethodOfAsync(Context()));
    }

    [TestMethod]
    public async Task ExecuteAsync_AResponse_WritesItsStatusVersionHeadersContentRedirectAndSizes_Har12Response()
    {
        var response = (await EntryOfAsync(Context(), Origin(301, "moved", [("Content-Type", "text/html"), ("Location", "/new")]))).GetProperty("response");

        Assert.AreEqual(301, response.GetProperty("status").GetInt32());
        Assert.AreEqual(string.Empty, response.GetProperty("statusText").GetString());
        Assert.AreEqual("HTTP/1.1", response.GetProperty("httpVersion").GetString());
        Assert.AreEqual(0, response.GetProperty("cookies").GetArrayLength());
        CollectionAssert.AreEqual(new[] { "Content-Type: text/html", "Location: /new" }, NameValues(response.GetProperty("headers")));
        var content = response.GetProperty("content");
        Assert.AreEqual(5, content.GetProperty("size").GetInt32());
        Assert.AreEqual("text/html", content.GetProperty("mimeType").GetString());
        Assert.AreEqual("moved", content.GetProperty("text").GetString());
        Assert.IsFalse(content.TryGetProperty("encoding", out _));
        Assert.AreEqual("http://example.com/new", response.GetProperty("redirectURL").GetString());
        Assert.AreEqual(42, response.GetProperty("headersSize").GetInt32());
        Assert.AreEqual(5, response.GetProperty("bodySize").GetInt32());
    }

    [TestMethod]
    public async Task ExecuteAsync_ABinaryBody_WritesItAsBase64WithEncoding_Har12Content()
    {
        var content = (await EntryOfAsync(Context(), Origin(200, bytes: [0xFF, 0x00, 0x80], contentType: "image/png"))).GetProperty("response").GetProperty("content");

        Assert.AreEqual("/wCA", content.GetProperty("text").GetString());
        Assert.AreEqual("base64", content.GetProperty("encoding").GetString());
        Assert.AreEqual("image/png", content.GetProperty("mimeType").GetString());
    }

    [TestMethod]
    public async Task ExecuteAsync_HttpVersions_AreWrittenAsCurlNamesThem()
    {
        Assert.AreEqual("HTTP/2", await ResponseVersionOfAsync(new Version(2, 0)));
        Assert.AreEqual("HTTP/3", await ResponseVersionOfAsync(new Version(3, 0)));
        Assert.AreEqual("HTTP/1.0", await ResponseVersionOfAsync(new Version(1, 0)));
        Assert.AreEqual(string.Empty, await ResponseVersionOfAsync(null));
    }

    [TestMethod]
    public async Task ExecuteAsync_AFailedTransferWithNoResponse_WritesStatusZeroAndUnknownSizesAndKeepsTheFailure()
    {
        var failure = TransferResult.Failure(CurlExitCode.CouldntConnect, "Failed to connect", 0);
        var hook = new HarTransferHook(_path);

        var result = await hook.ExecuteAsync(Context(), _ => ValueTask.FromResult(failure));

        Assert.AreSame(failure, result);
        using var har = Read();
        var entry = har.RootElement.GetProperty("log").GetProperty("entries")[0];
        var response = entry.GetProperty("response");
        Assert.AreEqual(0, response.GetProperty("status").GetInt32());
        Assert.AreEqual(string.Empty, response.GetProperty("content").GetProperty("mimeType").GetString());
        Assert.AreEqual(-1, response.GetProperty("headersSize").GetInt32());
        Assert.AreEqual(-1, response.GetProperty("bodySize").GetInt32());
        Assert.AreEqual("http://example.com/r", entry.GetProperty("request").GetProperty("url").GetString());
        Assert.IsFalse(entry.TryGetProperty("serverIPAddress", out _));
        Assert.AreEqual(-1, entry.GetProperty("timings").GetProperty("dns").GetDouble());
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoTransfers_WritesBothEntriesInOrder_Har12Entries()
    {
        var hook = new HarTransferHook(_path);

        await hook.ExecuteAsync(Context(url: "http://example.com/1"), Origin(200, "one"));
        await hook.ExecuteAsync(Context(url: "http://example.com/2"), Origin(404, "two", effectiveUrl: null));

        using var har = Read();
        var entries = har.RootElement.GetProperty("log").GetProperty("entries");
        Assert.AreEqual(2, entries.GetArrayLength());
        Assert.AreEqual("http://example.com/r", entries[0].GetProperty("request").GetProperty("url").GetString());
        Assert.AreEqual("http://example.com/2", entries[1].GetProperty("request").GetProperty("url").GetString());
        Assert.AreEqual(404, entries[1].GetProperty("response").GetProperty("status").GetInt32());
    }

    [TestMethod]
    public async Task ExecuteAsync_ATransferThatIsNotHttp_PassesThroughAndWritesNothing()
    {
        var success = TransferResult.Success(0);
        TransferContext context = new() { Url = CurlUrl.Parse("ftp://example.com/f"), Output = new MemoryStream() };

        Assert.AreSame(success, await new HarTransferHook(_path).ExecuteAsync(context, _ => ValueTask.FromResult(success)));
        Assert.IsFalse(File.Exists(_path));
    }

    [TestMethod]
    public async Task ExecuteAsync_AFileThatCannotBeWritten_FailsWithExit23AndKeepsTheReport()
    {
        var missing = await new HarTransferHook(Path.Combine(_directory, "no", "such", "dir.har")).ExecuteAsync(Context(), Origin(200, "x"));
        var directory = await new HarTransferHook(_directory).ExecuteAsync(Context(), Origin(200, "x"));

        Assert.AreEqual(CurlExitCode.WriteError, missing.ExitCode);
        StringAssert.StartsWith(missing.ErrorMessage, "Failed writing the HAR file ");
        Assert.AreEqual(1, missing.BytesTransferred);
        Assert.AreEqual(200, missing.Report!.ResponseCode);
        Assert.AreEqual(CurlExitCode.WriteError, directory.ExitCode);
    }

    private static string[] NameValues(JsonElement array) =>
        [.. array.EnumerateArray().Select(pair => pair.GetProperty("name").GetString() + ": " + pair.GetProperty("value").GetString())];

    private static TransferContext Context(HttpRequestOptions? http = null, string url = "http://example.com/r", bool noBody = false, Stream? upload = null) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = new MemoryStream(),
        Http = http ?? new HttpRequestOptions(),
        NoBody = noBody,
        Upload = upload,
        TimeProvider = new FixedTimeProvider(),
    };

    /// <summary>Stands in for curl's handler: writes one recorded response body and reports it.</summary>
    private static Func<ITransferContext, ValueTask<TransferResult>> Origin(
        int code,
        string? body = null,
        (string Name, string Value)[]? fields = null,
        string? method = null,
        string? effectiveUrl = "http://example.com/r",
        byte[]? bytes = null,
        string? contentType = null) =>
        async context =>
        {
            var content = bytes ?? Encoding.UTF8.GetBytes(body ?? string.Empty);
            await context.Output.WriteAsync(content);
            return TransferResult.Success(content.Length) with
            {
                Report = new TransferReport
                {
                    ResponseCode = code,
                    Method = method,
                    HttpVersion = new Version(1, 1),
                    EffectiveUrl = effectiveUrl,
                    ContentType = contentType,
                    RedirectUrl = code == 301 ? "http://example.com/new" : null,
                    HeaderSize = 42,
                    RemoteEndPoint = new IPEndPoint(IPAddress.Parse("192.0.2.7"), 80),
                    ResponseHeaders = [.. (fields ?? []).Select(field => new KeyValuePair<string, string>(field.Name, field.Value))],
                    Timings = new TransferTimings(0, null, 0, 0, 20, 30),
                },
            };
        };

    private async Task<JsonElement> EntryOfAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> origin)
    {
        await new HarTransferHook(_path).ExecuteAsync(context, origin);
        using var har = Read();
        return har.RootElement.GetProperty("log").GetProperty("entries")[0].Clone();
    }

    private async Task<string?> MethodOfAsync(ITransferContext context) =>
        (await EntryOfAsync(context, Origin(200, string.Empty))).GetProperty("request").GetProperty("method").GetString();

    private async Task<string?> ResponseVersionOfAsync(Version? version)
    {
        var entry = await EntryOfAsync(Context(), async context =>
        {
            var result = await Origin(200, string.Empty)(context);
            return result with { Report = result.Report! with { HttpVersion = version } };
        });
        return entry.GetProperty("response").GetProperty("httpVersion").GetString();
    }

    private JsonDocument Read() => JsonDocument.Parse(File.ReadAllBytes(_path));

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;

        public override long TimestampFrequency => 1000;
    }
}
