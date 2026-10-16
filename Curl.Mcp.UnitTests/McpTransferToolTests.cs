using System.Text;
using Curl.Api.Codes;
using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Api.Options.Http;

namespace Curl.Mcp;

[TestClass]
public sealed class McpTransferToolTests
{
    private static readonly McpResponseCap Cap = new(16);

    [TestMethod]
    public async Task RunAsync_EveryField_BecomesItsOptionAndNothingElseIsSet()
    {
        FakeEasyTransfer transfer = new();
        McpTransferRequest request = new("https://example.test/upload")
        {
            Method = "PUT",
            Headers = [new McpTransferHeader("X-One", "1"), new McpTransferHeader("X-Empty", "")],
            Body = new McpTransferBody("payload", false),
            FollowRedirects = true,
            TimeoutSeconds = 2.5,
            Insecure = true,
            Proxy = "http://proxy.test:3128",
            UserAgent = "agent/1",
        };

        McpTransferToolResult result = await new McpTransferTool(new FakeTransferEngine(transfer), Cap).RunAsync(request, CancellationToken.None);

        Assert.IsFalse(result.IsError);
        EasyOptions defaults = new();
        EasyOptions set = transfer.Options;
        Assert.AreEqual(defaults.Behaviour, set.Behaviour);
        Assert.AreEqual(defaults.Authentication, set.Authentication);
        Assert.AreEqual(defaults.Protocols, set.Protocols);
        Assert.AreEqual(defaults.Network with { Url = "https://example.test/upload" }, set.Network);
        Assert.AreEqual(defaults.Proxy with { Proxy = "http://proxy.test:3128" }, set.Proxy);
        Assert.AreEqual(defaults.Transfer with { CustomRequest = "PUT", Timeout = TimeSpan.FromSeconds(2.5) }, set.Transfer);
        Assert.AreEqual(defaults.Tls with { Origin = defaults.Tls.Origin with { VerifyPeer = false, VerifyHost = false } }, set.Tls);
        CollectionAssert.AreEqual(new[] { "X-One: 1", "X-Empty;" }, set.Http.HttpHeaders.ToArray());
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("payload"), set.Http.PostFields!.Value.ToArray());
        Assert.AreEqual(
            defaults.Http with { UserAgent = "agent/1", FollowLocation = FollowLocation.All, Post = true },
            set.Http with { HttpHeaders = defaults.Http.HttpHeaders, PostFields = null });
        Assert.IsNotNull(set.Callbacks.Write);
        Assert.IsNotNull(set.Callbacks.Header);
        Assert.AreEqual(defaults.Callbacks, set.Callbacks with { Write = null, Header = null });
        Assert.IsTrue(transfer.Disposed);
    }

    [TestMethod]
    public async Task RunAsync_Success_ReturnsStatusOrderedHeadersBodyAndInfo()
    {
        FakeEasyTransfer transfer = new()
        {
            HeaderLines = ["HTTP/1.1 301 Moved\r\n", "Location: /b\r\n", "\r\n", "HTTP/1.1 200 OK\r\n", "Set-Cookie: a=1\r\n", "Set-Cookie: b=2\r\n", "\r\n"],
            BodyChunks = [Encoding.UTF8.GetBytes("hello "), Encoding.UTF8.GetBytes("world")],
            InfoToReport = new TransferInfo { ResponseCode = 200, EffectiveUrl = "http://h/b", RedirectCount = 1, HttpVersion = 2, PrimaryIp = "127.0.0.1", PrimaryPort = 80 },
        };

        McpTransferToolResult result = await new McpTransferTool(new FakeTransferEngine(transfer), Cap)
            .RunAsync(new McpTransferRequest("http://h/a") { FollowRedirects = true }, CancellationToken.None);

        Assert.IsFalse(result.IsError);
        Assert.AreEqual(200L, result.StatusCode);
        CollectionAssert.AreEqual(
            new[] { new McpTransferHeader("Set-Cookie", "a=1"), new McpTransferHeader("Set-Cookie", "b=2") },
            result.Headers!.ToArray());
        Assert.AreEqual("hello world", result.Body!.Content);
        Assert.IsFalse(result.Body.IsBase64);
        Assert.IsFalse(result.IsBodyTruncated);
        Assert.AreEqual("http://h/b", result.Info!.EffectiveUrl);
        Assert.AreEqual(1L, result.Info.RedirectCount);
        Assert.AreEqual("1.1", result.Info.HttpVersion);
        Assert.AreEqual("127.0.0.1", result.Info.PrimaryIp);
        Assert.AreEqual(80L, result.Info.PrimaryPort);
    }

    [TestMethod]
    public async Task RunAsync_BodyOverTheCap_IsTruncatedWithTheNote()
    {
        FakeEasyTransfer transfer = new() { BodyChunks = [Encoding.UTF8.GetBytes(new string('x', 40))] };

        McpTransferToolResult result = await new McpTransferTool(new FakeTransferEngine(transfer), Cap)
            .RunAsync(new McpTransferRequest("http://h/"), CancellationToken.None);

        Assert.IsTrue(result.IsBodyTruncated);
        Assert.AreEqual(new string('x', 16), result.Body!.Content);
        Assert.AreEqual(40, result.Body.TotalBytes);
        StringAssert.Contains(result.Body.TruncationNote, "returned 16 of 40 bytes");
    }

    [TestMethod]
    public async Task RunAsync_BinaryBody_IsBase64()
    {
        FakeEasyTransfer transfer = new() { BodyChunks = [[0xFF, 0x00, 0xFE]] };

        McpTransferToolResult result = await new McpTransferTool(new FakeTransferEngine(transfer), Cap)
            .RunAsync(new McpTransferRequest("http://h/"), CancellationToken.None);

        Assert.IsTrue(result.Body!.IsBase64);
        Assert.AreEqual(Convert.ToBase64String([0xFF, 0x00, 0xFE]), result.Body.Content);
    }

    [TestMethod]
    [DataRow(CurlCode.CouldntResolveHost, "Could not resolve host: nowhere.test")]
    [DataRow(CurlCode.CouldntConnect, "Failed to connect to h port 1 after 0 ms: Could not connect to server")]
    [DataRow(CurlCode.OperationTimedOut, "Operation timed out after 1000 milliseconds with 0 bytes received")]
    [DataRow(CurlCode.PeerFailedVerification, "SSL certificate problem: unable to get local issuer certificate")]
    public async Task RunAsync_FailingCode_IsAToolErrorWithLibcurlsMessageAndTheCode(CurlCode code, string message)
    {
        FakeEasyTransfer transfer = new() { Code = code, ErrorTextToReport = message, BodyChunks = [[0x41]] };

        McpTransferToolResult result = await new McpTransferTool(new FakeTransferEngine(transfer), Cap)
            .RunAsync(new McpTransferRequest("https://nowhere.test/"), CancellationToken.None);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual(McpToolFailureKind.CurlExit, result.Failure!.Kind);
        Assert.AreEqual((int)code, result.Failure.ExitCode);
        Assert.AreEqual($"{message}\ncurl exit code {(int)code} ({code})", result.Failure.Text);
        Assert.IsNull(result.StatusCode);
        Assert.IsNull(result.Headers);
        Assert.IsNull(result.Body);
        Assert.IsNull(result.Info);
        Assert.IsFalse(result.IsBodyTruncated);
    }

    [TestMethod]
    public async Task RunAsync_InvalidRequest_FailsNamingTheFieldBeforeTheEngineIsCalled()
    {
        FakeTransferEngine engine = new(new FakeEasyTransfer());

        McpTransferToolResult result = await new McpTransferTool(engine, Cap)
            .RunAsync(new McpTransferRequest("http://h/") { TimeoutSeconds = -1 }, CancellationToken.None);

        Assert.AreEqual(0, engine.EasyHandlesCreated);
        Assert.AreEqual(McpToolFailureKind.InvalidRequest, result.Failure!.Kind);
        StringAssert.StartsWith(result.Failure.Text, "Invalid timeoutSeconds:");
    }

    [TestMethod]
    public async Task RunAsync_AlreadyCancelled_GivesTheCancelledErrorWithoutAHandle()
    {
        FakeTransferEngine engine = new(new FakeEasyTransfer());

        McpTransferToolResult result = await new McpTransferTool(engine, Cap)
            .RunAsync(new McpTransferRequest("http://h/"), new CancellationToken(canceled: true));

        Assert.AreEqual(0, engine.EasyHandlesCreated);
        Assert.AreSame(McpToolFailure.Cancelled, result.Failure);
    }

    [TestMethod]
    public async Task RunAsync_CancelledDuringTheTransfer_ReachesTheTransferAndGivesTheCancelledError()
    {
        FakeEasyTransfer transfer = new() { WaitsForCancellation = true };
        using CancellationTokenSource source = new();
        Task<McpTransferToolResult> running = new McpTransferTool(new FakeTransferEngine(transfer), Cap)
            .RunAsync(new McpTransferRequest("http://h/"), source.Token);

        await source.CancelAsync();
        McpTransferToolResult result = await running;

        Assert.AreEqual(source.Token, transfer.TokenSeen);
        Assert.AreSame(McpToolFailure.Cancelled, result.Failure);
        Assert.IsTrue(transfer.Disposed);
    }

    [TestMethod]
    public async Task RunAsync_CancelledAsTheTransferEnds_GivesTheCancelledErrorNotItsCode()
    {
        using CancellationTokenSource source = new();
        FakeEasyTransfer transfer = new() { Code = CurlCode.AbortedByCallback, CancelsAfterPerform = source };

        McpTransferToolResult result = await new McpTransferTool(new FakeTransferEngine(transfer), Cap)
            .RunAsync(new McpTransferRequest("http://h/"), source.Token);

        Assert.AreSame(McpToolFailure.Cancelled, result.Failure);
    }

    [TestMethod]
    public async Task RunAsync_OperationCanceledWithoutCancellation_IsNotSwallowed()
    {
        FakeEasyTransfer transfer = new() { Throws = new OperationCanceledException() };

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => new McpTransferTool(new FakeTransferEngine(transfer), Cap)
            .RunAsync(new McpTransferRequest("http://h/"), CancellationToken.None));
    }

    [TestMethod]
    public async Task RunAsync_NullRequest_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new McpTransferTool(new FakeTransferEngine(new FakeEasyTransfer()), Cap)
            .RunAsync(null!, CancellationToken.None));
    }
}
