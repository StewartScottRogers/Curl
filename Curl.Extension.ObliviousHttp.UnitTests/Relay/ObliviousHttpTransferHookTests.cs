using System.Text;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.ObliviousHttp.BinaryHttp;

namespace Curl.Extension.ObliviousHttp.Relay;

[TestClass]
public sealed class ObliviousHttpTransferHookTests
{
    private const string RelayUrl = "https://relay.example/ohttp";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ExecuteAsync_UserBodyAndRequestTarget_NeverReachTheKeyServerOrTheRelayInClear()
    {
        FakeObliviousServers servers = new();
        TransferContext context = Context("https://example.com/hello.txt", new HttpRequestOptions { RequestTarget = "/private/account" }, "secret"u8.ToArray());

        await new ObliviousHttpTransferHook(RelayUrl, null).ExecuteAsync(context, servers.Invoke);

        ITransferContext keys = servers.Sent[0];
        Assert.AreEqual(0, keys.PostData!.Value.Length);
        Assert.IsNull(keys.Http!.RequestTarget);
        Assert.AreEqual("https://example.com/.well-known/ohttp-gateway", keys.Url.OriginalString);
        CollectionAssert.DoesNotContain(keys.Http.Headers.Where(h => h.StartsWith("Content-Type", StringComparison.Ordinal)).ToList(), "Content-Type: application/x-www-form-urlencoded");
        Assert.IsNull(servers.Sent[1].Http!.RequestTarget);
        Assert.AreEqual(RelayUrl, servers.Sent[1].Url.OriginalString);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoKeysOption_FetchesTheWellKnownGatewayKeysThenPostsTheEncapsulatedRequestToTheRelay()
    {
        FakeObliviousServers servers = new();
        TransferContext context = Context("https://example.com/hello.txt?x=1", new HttpRequestOptions { Headers = ["Accept: text/plain"] });

        TransferResult result = await new ObliviousHttpTransferHook(RelayUrl, null).ExecuteAsync(context, servers.Invoke);

        Assert.HasCount(2, servers.Sent);
        ITransferContext keys = servers.Sent[0];
        Assert.AreEqual("https://example.com/.well-known/ohttp-gateway", keys.Url.OriginalString);
        Assert.AreEqual("GET", keys.Http!.CustomMethod);
        Assert.AreEqual(0, keys.PostData!.Value.Length);
        CollectionAssert.Contains(keys.Http.Headers.ToList(), "Accept: application/ohttp-keys");
        ITransferContext relay = servers.Sent[1];
        Assert.AreEqual(RelayUrl, relay.Url.OriginalString);
        Assert.AreEqual("POST", relay.Http!.CustomMethod);
        CollectionAssert.Contains(relay.Http.Headers.ToList(), "Content-Type: message/ohttp-req");
        CollectionAssert.Contains(relay.Http.Headers.ToList(), "Accept: message/ohttp-res");
        CollectionAssert.DoesNotContain(relay.Http.Headers.ToList(), "Accept: text/plain");
        CollectionAssert.DoesNotContain(keys.Http.Headers.ToList(), "Accept: text/plain");
        Assert.IsNull(relay.Credentials);
        byte[] expected = BinaryHttpRequestEncoder.EncodeKnownLength(new BinaryHttpRequest("GET", "https", "example.com", "/hello.txt?x=1", [new("Accept", "text/plain")], ReadOnlyMemory<byte>.Empty));
        CollectionAssert.AreEqual(expected, servers.OpenedRequest);
        Assert.AreEqual("hello", Text(context.Output));
        Assert.AreEqual("HTTP/1.1 200 \r\ncontent-type: text/plain\r\n\r\n", Text(context.HeaderOutput!));
        Assert.AreEqual("HTTP/1.1 200 \r\ncontent-type: text/plain\r\n\r\n", Text(context.DumpHeaderOutput!));
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(5, result.BytesTransferred);
        Assert.AreEqual(200, result.Report!.ResponseCode);
        Assert.AreEqual("text/plain", result.Report.ContentType);
        Assert.AreEqual("GET", result.Report.Method);
        Assert.AreEqual("https://example.com/hello.txt?x=1", result.Report.EffectiveUrl);
    }

    [TestMethod]
    public async Task ExecuteAsync_KeysFile_ReadsTheFileAndSendsThePostBodyInside()
    {
        string path = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ohttp-keys");
        await File.WriteAllBytesAsync(path, FakeObliviousServers.KeyList(), TestContext.CancellationToken);
        FakeObliviousServers servers = new();
        TransferContext context = Context("http://example.com:8080/form", new HttpRequestOptions { UserAgent = "curl/8.21.0" }, postData: "a=b"u8.ToArray());

        try
        {
            TransferResult result = await new ObliviousHttpTransferHook(RelayUrl, path).ExecuteAsync(context, servers.Invoke);

            Assert.IsTrue(result.IsSuccess);
        }
        finally
        {
            File.Delete(path);
        }

        Assert.HasCount(1, servers.Sent);
        byte[] expected = BinaryHttpRequestEncoder.EncodeKnownLength(new BinaryHttpRequest(
            "POST", "http", "example.com:8080", "/form", [new("user-agent", "curl/8.21.0"), new("content-type", "application/x-www-form-urlencoded")], "a=b"u8.ToArray()));
        CollectionAssert.AreEqual(expected, servers.OpenedRequest);
    }

    [TestMethod]
    [DataRow("https://keys.example/k")]
    [DataRow("HTTP://keys.example/k")]
    public async Task ExecuteAsync_HeaderLinesAndCustomMethodWithKeysUrl_FetchesTheKeysAndEncodesAsCurlSendsThem(string keysUrl)
    {
        FakeObliviousServers servers = new();
        HttpRequestOptions http = new()
        {
            CustomMethod = "PUT",
            RequestTarget = "/target",
            UserAgent = "ignored",
            Headers = ["User-Agent: mine", "X-Empty;", "Removed:", "junk", ";", "Content-Type: text/plain"],
        };
        TransferContext context = Context("https://example.com/r", http, postData: "x"u8.ToArray());

        await new ObliviousHttpTransferHook(RelayUrl, keysUrl).ExecuteAsync(context, servers.Invoke);

        Assert.AreEqual(keysUrl, servers.Sent[0].Url.OriginalString);
        byte[] expected = BinaryHttpRequestEncoder.EncodeKnownLength(new BinaryHttpRequest(
            "PUT", "https", "example.com", "/target", [new("User-Agent", "mine"), new("X-Empty", string.Empty), new("Content-Type", "text/plain")], "x"u8.ToArray()));
        CollectionAssert.AreEqual(expected, servers.OpenedRequest);
    }

    [TestMethod]
    public async Task ExecuteAsync_PostWithoutDataAndNoHeaderOutputs_PostsAndWritesOnlyTheBody()
    {
        FakeObliviousServers servers = new() { BinaryResponse = FakeObliviousServers.KnownLengthResponse(404, [], "gone") };
        TransferContext context = new() { Url = CurlUrl.Parse("https://example.com/r"), Output = new MemoryStream(), Http = new HttpRequestOptions(), PostWithoutData = true };

        TransferResult result = await new ObliviousHttpTransferHook(RelayUrl, null).ExecuteAsync(context, servers.Invoke);

        Assert.AreEqual("gone", Text(context.Output));
        Assert.AreEqual(404, result.Report!.ResponseCode);
        Assert.IsNull(result.Report.ContentType);
        Assert.AreEqual("POST", result.Report.Method);
    }

    [TestMethod]
    public async Task ExecuteAsync_NotHttp_RunsTheTransferUnchanged()
    {
        TransferContext context = new() { Url = CurlUrl.Parse("ftp://example.com/f"), Output = new MemoryStream() };
        ITransferContext? sent = null;

        TransferResult result = await new ObliviousHttpTransferHook(RelayUrl, null).ExecuteAsync(context, given =>
        {
            sent = given;
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        Assert.AreSame(context, sent);
        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    public async Task ExecuteAsync_RequestItCannotEncapsulate_FailsWithExit2WithoutContactingAnyone()
    {
        TransferContext[] contexts =
        [
            Context(upload: new MemoryStream()),
            Context(noBody: true),
            Context(range: ByteRange.Bounded(0, 1)),
            Context(resumeFrom: 5),
            Context(timeCondition: new TimeCondition(DateTimeOffset.UnixEpoch, TimeConditionKind.IfModifiedSince)),
            Context(http: new HttpRequestOptions { Body = new BytesBody("x"u8.ToArray(), "multipart/form-data") }),
        ];

        foreach (TransferContext context in contexts)
        {
            TransferResult result = await new ObliviousHttpTransferHook(RelayUrl, null).ExecuteAsync(context, NeverCalled);

            Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
            StringAssert.StartsWith(result.ErrorMessage, "--ohttp-relay cannot send ");
        }
    }

    [TestMethod]
    [DataRow("http://[", null)]
    [DataRow(RelayUrl, "https://[")]
    public async Task ExecuteAsync_MalformedUrl_FailsWithExit3(string relay, string? keys)
    {
        TransferResult result = await new ObliviousHttpTransferHook(relay, keys).ExecuteAsync(Context(), NeverCalled);

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_KeysFileMissing_FailsWithExit26()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.ohttp-keys");

        TransferResult result = await new ObliviousHttpTransferHook(RelayUrl, path).ExecuteAsync(Context(), NeverCalled);

        Assert.AreEqual(CurlExitCode.ReadError, result.ExitCode);
        Assert.AreEqual("Failed to read the Oblivious HTTP key configuration file " + path, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledWhileReadingTheKeysFile_Throws()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ohttp-keys");
        TransferContext context = new() { Url = CurlUrl.Parse("https://example.com/r"), Output = new MemoryStream(), Http = new HttpRequestOptions(), CancellationToken = new CancellationToken(canceled: true) };

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await new ObliviousHttpTransferHook(RelayUrl, path).ExecuteAsync(context, NeverCalled));
    }

    [TestMethod]
    [DataRow(403, "message/ohttp-res", CurlExitCode.HttpReturnedError, "The Oblivious HTTP relay returned error: 403")]
    [DataRow(200, "text/html", CurlExitCode.WeirdServerReply, "The Oblivious HTTP relay answered with Content-Type text/html, not message/ohttp-res")]
    [DataRow(200, null, CurlExitCode.WeirdServerReply, "The Oblivious HTTP relay answered with Content-Type (none), not message/ohttp-res")]
    public async Task ExecuteAsync_RelayDoesNotAnswerWithAnEncapsulatedResponse_Fails(int status, string? contentType, CurlExitCode exitCode, string message)
    {
        FakeObliviousServers servers = new() { RelayStatus = status, RelayContentType = contentType };
        TransferContext context = Context();

        TransferResult result = await new ObliviousHttpTransferHook(RelayUrl, null).ExecuteAsync(context, servers.Invoke);

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        Assert.AreEqual(0, context.Output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_RelayContentTypeWithParameters_IsAccepted()
    {
        FakeObliviousServers servers = new() { RelayContentType = "Message/OHTTP-res; charset=binary" };

        TransferResult result = await new ObliviousHttpTransferHook(RelayUrl, null).ExecuteAsync(Context(), servers.Invoke);

        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    public async Task ExecuteAsync_KeyFetchFails_GivesItsResult()
    {
        TransferResult refused = TransferResult.Failure(CurlExitCode.CouldntConnect, "Failed to connect");

        TransferResult result = await new ObliviousHttpTransferHook(RelayUrl, null).ExecuteAsync(Context(), _ => ValueTask.FromResult(refused));

        Assert.AreSame(refused, result);
    }

    [TestMethod]
    public async Task ExecuteAsync_KeyFetchWithNoReport_FailsWithExit22()
    {
        TransferResult result = await new ObliviousHttpTransferHook(RelayUrl, null).ExecuteAsync(Context(), _ => ValueTask.FromResult(TransferResult.Success(0)));

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("The Oblivious HTTP gateway returned error: 0", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResponseDoesNotAuthenticate_FailsWithExit8()
    {
        FakeObliviousServers servers = new() { TamperWithResponse = true };

        TransferResult result = await new ObliviousHttpTransferHook(RelayUrl, null).ExecuteAsync(Context(), servers.Invoke);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        StringAssert.StartsWith(result.ErrorMessage, "Oblivious HTTP: ");
    }

    [TestMethod]
    [DataRow(new byte[] { 0 })]
    [DataRow(new byte[] { 0, 3, 1, 0, 0x99 })]
    public async Task ExecuteAsync_KeyConfigurationUnusable_FailsWithExit8(byte[] keyList)
    {
        TransferResult result = await new ObliviousHttpTransferHook(RelayUrl, null).ExecuteAsync(Context(), async context =>
        {
            await context.Output.WriteAsync(keyList);
            return TransferResult.Success(keyList.Length) with { Report = new TransferReport { ResponseCode = 200, ContentType = "application/ohttp-keys" } };
        });

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
    }

    [TestMethod]
    [DataRow(HttpFailMode.Fail, 404, 22, "")]
    [DataRow(HttpFailMode.Fail, 200, 0, "gone")]
    [DataRow(HttpFailMode.None, 404, 0, "gone")]
    public async Task ExecuteAsync_FailMode_ExitsWith22AndNoBodyOnlyForAnInnerErrorStatus(HttpFailMode fail, int status, int exitCode, string body)
    {
        FakeObliviousServers servers = new() { BinaryResponse = FakeObliviousServers.KnownLengthResponse(status, [], "gone") };
        TransferContext context = Context(http: new HttpRequestOptions { Fail = fail });

        TransferResult result = await new ObliviousHttpTransferHook(RelayUrl, null).ExecuteAsync(context, servers.Invoke);

        Assert.AreEqual((CurlExitCode)exitCode, result.ExitCode);
        Assert.AreEqual(body, Text(context.Output));
    }

    [TestMethod]
    public async Task ExecuteAsync_CredentialsBearerRefererAndCookies_AreCarriedInsideTheEncapsulatedRequest()
    {
        FakeObliviousServers servers = new();
        HttpRequestOptions http = new() { Referer = "https://from/", CookieStrings = "a=b" };
        TransferContext context = Context("https://example.com/r", http, credentials: new System.Net.NetworkCredential("alice", "secret"));

        await new ObliviousHttpTransferHook(RelayUrl, null).ExecuteAsync(context, servers.Invoke);

        byte[] expected = BinaryHttpRequestEncoder.EncodeKnownLength(new BinaryHttpRequest(
            "GET", "https", "example.com", "/r", [new("authorization", "Basic YWxpY2U6c2VjcmV0"), new("referer", "https://from/"), new("cookie", "a=b")], ReadOnlyMemory<byte>.Empty));
        CollectionAssert.AreEqual(expected, servers.OpenedRequest);
    }

    [TestMethod]
    public async Task ExecuteAsync_BearerToken_IsCarriedAsABearerAuthorization()
    {
        FakeObliviousServers servers = new();
        TransferContext context = Context("https://example.com/r", new HttpRequestOptions { BearerToken = "tok" }, credentials: new System.Net.NetworkCredential("a", "b"));

        await new ObliviousHttpTransferHook(RelayUrl, null).ExecuteAsync(context, servers.Invoke);

        byte[] expected = BinaryHttpRequestEncoder.EncodeKnownLength(new BinaryHttpRequest(
            "GET", "https", "example.com", "/r", [new("authorization", "Bearer tok")], ReadOnlyMemory<byte>.Empty));
        CollectionAssert.AreEqual(expected, servers.OpenedRequest);
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Digest)]
    [DataRow(HttpAuthSchemes.Any)]
    public async Task ExecuteAsync_CredentialsWithASchemeOtherThanBasic_FailsWithExit2WithoutContactingAnyone(HttpAuthSchemes schemes)
    {
        TransferContext context = Context(http: new HttpRequestOptions { AuthSchemes = schemes }, credentials: new System.Net.NetworkCredential("a", "b"));

        TransferResult result = await new ObliviousHttpTransferHook(RelayUrl, null).ExecuteAsync(context, NeverCalled);

        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        StringAssert.Contains(result.ErrorMessage, "--digest");
    }

    [TestMethod]
    public async Task ExecuteAsync_BearerSchemeWithCredentials_IsCarriedAsABearerAuthorization()
    {
        FakeObliviousServers servers = new();
        TransferContext context = Context(http: new HttpRequestOptions { BearerToken = "tok", AuthSchemes = HttpAuthSchemes.Bearer }, credentials: new System.Net.NetworkCredential("a", "b"));

        await new ObliviousHttpTransferHook(RelayUrl, null).ExecuteAsync(context, servers.Invoke);

        AssertOpenedRequestFields(servers, new BinaryHttpField("authorization", "Bearer tok"));
    }

    [TestMethod]
    public async Task ExecuteAsync_AuthorizationHeaderWithheld_GeneratesNoAuthorization()
    {
        FakeObliviousServers servers = new();
        TransferContext context = Context(http: new HttpRequestOptions { AuthorizationHeaderWithheld = true }, credentials: new System.Net.NetworkCredential("a", "b"));

        await new ObliviousHttpTransferHook(RelayUrl, null).ExecuteAsync(context, servers.Invoke);

        AssertOpenedRequestFields(servers);
    }

    [TestMethod]
    [DataRow("Authorization:", "authorization")]
    [DataRow("Referer:", "referer")]
    [DataRow("Cookie:", "cookie")]
    public async Task ExecuteAsync_HeaderLineWithNoValue_SuppressesTheFieldTheHookWouldAdd(string line, string name)
    {
        FakeObliviousServers servers = new();
        HttpRequestOptions http = new() { Headers = [line], Referer = "https://from/", CookieStrings = "a=b" };
        TransferContext context = Context(http: http, credentials: new System.Net.NetworkCredential("a", "b"));

        await new ObliviousHttpTransferHook(RelayUrl, null).ExecuteAsync(context, servers.Invoke);

        BinaryHttpField[] others = [.. new BinaryHttpField[] { new("authorization", "Basic YTpi"), new("referer", "https://from/"), new("cookie", "a=b") }.Where(field => field.Name != name)];
        AssertOpenedRequestFields(servers, others);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_NonAsciiPasswordOnWindows_IsEncodedInTheAnsiCodePage()
    {
        await AssertBasicValueAsync("dTpw5A==");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_NonAsciiPasswordOnPosix_IsEncodedAsUtf8()
    {
        await AssertBasicValueAsync("w6Q=");
    }

    [TestMethod]
    [DataRow(HttpFailMode.FailWithBody, 404, 22, "gone")]
    [DataRow(HttpFailMode.FailWithBody, 200, 0, "gone")]
    public async Task ExecuteAsync_FailWithBody_WritesTheBodyAndExits22OnlyForAnInnerErrorStatus(HttpFailMode fail, int status, int exitCode, string body)
    {
        FakeObliviousServers servers = new() { BinaryResponse = FakeObliviousServers.KnownLengthResponse(status, [], "gone") };
        TransferContext context = Context(http: new HttpRequestOptions { Fail = fail });

        TransferResult result = await new ObliviousHttpTransferHook(RelayUrl, null).ExecuteAsync(context, servers.Invoke);

        Assert.AreEqual((CurlExitCode)exitCode, result.ExitCode);
        Assert.AreEqual(body, Text(context.Output));
        if (exitCode == 22)
        {
            Assert.AreEqual("The requested URL returned error: 404", result.ErrorMessage);
        }
    }

    private static async Task AssertBasicValueAsync(string base64)
    {
        FakeObliviousServers servers = new();
        TransferContext context = Context(credentials: new System.Net.NetworkCredential("u", "pä"));

        await new ObliviousHttpTransferHook(RelayUrl, null).ExecuteAsync(context, servers.Invoke);

        AssertOpenedRequestFields(servers, new BinaryHttpField("authorization", "Basic " + base64));
    }

    private static void AssertOpenedRequestFields(FakeObliviousServers servers, params BinaryHttpField[] fields) =>
        CollectionAssert.AreEqual(BinaryHttpRequestEncoder.EncodeKnownLength(new BinaryHttpRequest("GET", "https", "example.com", "/r", fields, ReadOnlyMemory<byte>.Empty)), servers.OpenedRequest);

    [TestMethod]
    [DataRow(true, false, "a compressed response (--compressed)")]
    [DataRow(false, true, "a cookie file (-b)")]
    public async Task ExecuteAsync_CompressedOrCookieFile_FailsWithExit2WithoutContactingAnyone(bool compressed, bool cookieFile, string refused)
    {
        TransferContext context = Context(http: new HttpRequestOptions { Compressed = compressed, HasCookieFiles = cookieFile });

        TransferResult result = await new ObliviousHttpTransferHook(RelayUrl, null).ExecuteAsync(context, NeverCalled);

        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        StringAssert.EndsWith(result.ErrorMessage, refused);
    }

    private static ValueTask<TransferResult> NeverCalled(ITransferContext context) => throw new AssertFailedException("A server was contacted.");

    private static string Text(Stream stream) => Encoding.Latin1.GetString(((MemoryStream)stream).ToArray());

    private static TransferContext Context(
        string url = "https://example.com/r",
        HttpRequestOptions? http = null,
        byte[]? postData = null,
        Stream? upload = null,
        bool noBody = false,
        ByteRange? range = null,
        long? resumeFrom = null,
        TimeCondition? timeCondition = null,
        System.Net.NetworkCredential? credentials = null) => new()
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            HeaderOutput = new MemoryStream(),
            DumpHeaderOutput = new MemoryStream(),
            Http = http ?? new HttpRequestOptions(),
            PostData = postData is null ? default(ReadOnlyMemory<byte>?) : postData,
            Upload = upload,
            NoBody = noBody,
            Range = range,
            ResumeFrom = resumeFrom,
            TimeCondition = timeCondition,
            Credentials = credentials,
        };
}
