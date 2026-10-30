using System.Text;

namespace Curl.Extension.JsonRpc;

[TestClass]
public sealed class JsonRpcTransferHookTests
{
    [TestMethod]
    public async Task ExecuteAsync_AnHttpTransfer_PostsTheCallsAndWritesThePairedReplies_Sections4And6()
    {
        MemoryStream output = new();
        TransferContext context = Context(output, new HttpRequestOptions { Headers = ["X-Own: 1"] });
        ITransferContext? sent = null;
        TransferResult answered = TransferResult.Success(60);

        TransferResult result = await new JsonRpcTransferHook("sum [1,2]; subtract [42,23]").ExecuteAsync(context, async given =>
        {
            sent = given;
            await given.Output.WriteAsync(Encoding.UTF8.GetBytes("[{\"result\":19,\"id\":2},{\"result\":3,\"id\":1}]"));
            return answered;
        });

        Assert.AreSame(answered, result);
        BytesBody body = (BytesBody)sent!.Http!.Body!;
        Assert.AreEqual("application/json", body.ContentType);
        Assert.AreEqual(
            "[{\"jsonrpc\":\"2.0\",\"method\":\"sum\",\"params\":[1,2],\"id\":1},{\"jsonrpc\":\"2.0\",\"method\":\"subtract\",\"params\":[42,23],\"id\":2}]",
            Encoding.UTF8.GetString(body.Content.Span));
        CollectionAssert.AreEqual(new[] { "X-Own: 1" }, sent.Http.Headers.ToArray());
        Assert.AreEqual("{\"result\":3,\"id\":1}\n{\"result\":19,\"id\":2}\n", Encoding.UTF8.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_ATransferThatIsNotHttp_RunsUnchanged()
    {
        TransferContext context = Context(new MemoryStream(), null);
        ITransferContext? sent = null;

        await new JsonRpcTransferHook("sum").ExecuteAsync(context, given =>
        {
            sent = given;
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        Assert.AreSame(context, sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_AValueThatNamesNoValidCall_FailsWithExit2BeforeSending()
    {
        bool ran = false;

        TransferResult result = await new JsonRpcTransferHook("sum 42").ExecuteAsync(Context(new MemoryStream(), new HttpRequestOptions()), _ =>
        {
            ran = true;
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        Assert.IsFalse(ran);
        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        StringAssert.StartsWith(result.ErrorMessage, "--jsonrpc: the call to sum");
    }

    [TestMethod]
    public void RequestContext_GivesEveryMemberButOutputAndHttpFromTheTransfer()
    {
        TransferContext inner = Context(new MemoryStream(), new HttpRequestOptions());
        MemoryStream output = new();
        HttpRequestOptions http = new();
        JsonRpcRequestContext context = new(inner, output, http);

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

    private static TransferContext Context(Stream output, HttpRequestOptions? http) => new()
    {
        Url = CurlUrl.Parse("http://example.com/rpc"),
        Output = output,
        HeaderOutput = new MemoryStream(),
        DumpHeaderOutput = new MemoryStream(),
        Http = http,
    };
}
