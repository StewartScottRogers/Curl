using System.Text;

namespace Curl.Extension.ConnectRpc;

[TestClass]
public sealed class ConnectTransferHookTests
{
    [TestMethod]
    public async Task ExecuteAsync_AUnaryCall_PostsTheMessageAsJsonAndWritesTheResponseUnchanged_UnaryRequest()
    {
        MemoryStream output = new();
        TransferContext context = Context(output, new HttpRequestOptions { Headers = ["X-Own: 1"], Body = new BytesBody(Encoding.UTF8.GetBytes("{\"name\":\"x\"}"), "application/x-www-form-urlencoded") });
        ITransferContext? sent = null;
        TransferResult answered = TransferResult.Success(12);

        TransferResult result = await new ConnectTransferHook("unary").ExecuteAsync(context, async given =>
        {
            sent = given;
            await given.Output.WriteAsync(Encoding.UTF8.GetBytes("{\"greeting\":\"hi\"}"));
            return answered;
        });

        Assert.AreSame(answered, result);
        BytesBody body = (BytesBody)sent!.Http!.Body!;
        Assert.AreEqual("application/json", body.ContentType);
        Assert.AreEqual("{\"name\":\"x\"}", Encoding.UTF8.GetString(body.Content.Span));
        CollectionAssert.AreEqual(new[] { "Connect-Protocol-Version: 1", "X-Own: 1" }, sent.Http.Headers.ToArray());
        Assert.AreEqual("{\"greeting\":\"hi\"}", Encoding.UTF8.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_NoMessageGiven_SendsTheEmptyObject()
    {
        ITransferContext? sent = null;

        await new ConnectTransferHook("unary").ExecuteAsync(Context(new MemoryStream(), new HttpRequestOptions()), given =>
        {
            sent = given;
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        Assert.AreEqual("{}", Encoding.UTF8.GetString(((BytesBody)sent!.Http!.Body!).Content.Span));
    }

    [TestMethod]
    public async Task ExecuteAsync_MaxTime_SendsTheTimeoutRoundedUpToMilliseconds_Timeout()
    {
        Assert.AreEqual("Connect-Timeout-Ms: 1501", await TimeoutHeaderFor(TimeSpan.FromTicks(15_000_001)));
        Assert.AreEqual("Connect-Timeout-Ms: 9999999999", await TimeoutHeaderFor(TimeSpan.FromDays(200)));
    }

    [TestMethod]
    public async Task ExecuteAsync_AStreamingCall_PostsEnvelopesAndWritesEachReplyMessage_StreamingRequestAndResponse()
    {
        MemoryStream output = new();
        TransferContext context = Context(output, new HttpRequestOptions { Body = new BytesBody(Encoding.UTF8.GetBytes("{\"n\":1} {\"n\":2}"), "x") });
        ITransferContext? sent = null;
        TransferResult answered = TransferResult.Success(40);

        TransferResult result = await new ConnectTransferHook("stream").ExecuteAsync(context, async given =>
        {
            sent = given;
            await given.Output.WriteAsync(ConnectStreamReplyTests.Framed((0, "{\"sum\":3}"), (2, "{}")));
            return answered;
        });

        Assert.AreSame(answered, result);
        BytesBody body = (BytesBody)sent!.Http!.Body!;
        Assert.AreEqual("application/connect+json", body.ContentType);
        CollectionAssert.AreEqual(ConnectStreamReplyTests.Framed((0, "{\"n\":1}"), (0, "{\"n\":2}")), body.Content.ToArray());
        CollectionAssert.AreEqual(new[] { "Connect-Protocol-Version: 1" }, sent.Http.Headers.ToArray());
        Assert.AreEqual("{\"sum\":3}\n", Encoding.UTF8.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_AStreamEndingInAnError_WritesItsMessagesAndFailsWithExit22_EndStreamMessage()
    {
        MemoryStream output = new();

        TransferResult result = await new ConnectTransferHook("stream").ExecuteAsync(Context(output, new HttpRequestOptions()), async given =>
        {
            await given.Output.WriteAsync(ConnectStreamReplyTests.Framed((0, "{}"), (2, "{\"error\":{\"code\":\"unavailable\"}}")));
            return TransferResult.Success(30);
        });

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("Connect error unavailable", result.ErrorMessage);
        Assert.AreEqual(30, result.BytesTransferred);
        Assert.AreEqual("{}\n", Encoding.UTF8.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_AResponseThatIsNotAConnectStream_FailsWithExit8()
    {
        MemoryStream output = new();

        TransferResult result = await new ConnectTransferHook("stream").ExecuteAsync(Context(output, new HttpRequestOptions()), async given =>
        {
            await given.Output.WriteAsync(Encoding.UTF8.GetBytes("404 page not found"));
            return TransferResult.Success(18);
        });

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_AFailedStreamingTransfer_GivesItsResultAndWritesNothing()
    {
        MemoryStream output = new();
        TransferResult failed = TransferResult.Failure(CurlExitCode.RecvError, "reset");

        TransferResult result = await new ConnectTransferHook("stream").ExecuteAsync(Context(output, new HttpRequestOptions()), async given =>
        {
            await given.Output.WriteAsync(new byte[] { 0, 0 });
            return failed;
        });

        Assert.AreSame(failed, result);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_StreamingMessagesThatAreNotJson_FailWithExit2BeforeSending()
    {
        await AssertRefusedBeforeSending("stream", new HttpRequestOptions { Body = new BytesBody(Encoding.UTF8.GetBytes("{"), "x") }, "--connect-rpc: the request messages");
    }

    [TestMethod]
    public async Task ExecuteAsync_AnUnknownCallType_FailsWithExit2BeforeSending()
    {
        await AssertRefusedBeforeSending("bidi", new HttpRequestOptions(), "--connect-rpc: 'bidi' is not a call type");
    }

    [TestMethod]
    public async Task ExecuteAsync_ABodyReadFromAStream_FailsWithExit2BeforeSending()
    {
        await AssertRefusedBeforeSending("unary", new HttpRequestOptions { Body = new StreamBody(new MemoryStream(), 0, "x") }, "--connect-rpc: give the request message");
    }

    [TestMethod]
    public async Task ExecuteAsync_ATransferThatIsNotHttp_RunsUnchanged()
    {
        TransferContext context = Context(new MemoryStream(), null);
        ITransferContext? sent = null;

        await new ConnectTransferHook("unary").ExecuteAsync(context, given =>
        {
            sent = given;
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        Assert.AreSame(context, sent);
    }

    [TestMethod]
    public void RequestContext_GivesEveryMemberButOutputAndHttpFromTheTransfer()
    {
        TransferContext inner = Context(new MemoryStream(), new HttpRequestOptions());
        MemoryStream output = new();
        HttpRequestOptions http = new();
        ConnectRequestContext context = new(inner, output, http);

        foreach (System.Reflection.PropertyInfo property in typeof(ITransferContext).GetProperties())
        {
            object? expected = property.Name switch
            {
                nameof(ITransferContext.Output) => output,
                nameof(ITransferContext.Http) => http,
                _ => property.GetValue(inner),
            };
            Assert.AreEqual(expected, property.GetValue(context), property.Name);
        }
    }

    private static async Task AssertRefusedBeforeSending(string callType, HttpRequestOptions http, string messageStart)
    {
        bool ran = false;

        TransferResult result = await new ConnectTransferHook(callType).ExecuteAsync(Context(new MemoryStream(), http), _ =>
        {
            ran = true;
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        Assert.IsFalse(ran);
        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        StringAssert.StartsWith(result.ErrorMessage, messageStart);
    }

    private static async Task<string> TimeoutHeaderFor(TimeSpan maxTime)
    {
        ITransferContext? sent = null;
        TransferContext context = Context(new MemoryStream(), new HttpRequestOptions(), maxTime);

        await new ConnectTransferHook("unary").ExecuteAsync(context, given =>
        {
            sent = given;
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        return sent!.Http!.Headers[1];
    }

    private static TransferContext Context(Stream output, HttpRequestOptions? http, TimeSpan? maxTime = null) => new()
    {
        Url = CurlUrl.Parse("http://example.com/greet.v1.GreetService/Greet"),
        Output = output,
        HeaderOutput = new MemoryStream(),
        DumpHeaderOutput = new MemoryStream(),
        Http = http,
        MaxTime = maxTime,
    };
}
