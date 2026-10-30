using System.Text;

namespace Curl.Extension.GrpcWeb;

[TestClass]
public sealed class GrpcWebTransferHookTests
{
    private static readonly byte[] OkTrailers = [0x80, 0, 0, 0, 15, .. "grpc-status:0\r\n"u8];

    [TestMethod]
    public async Task ExecuteAsync_AnHttpTransfer_SendsTheDataAsOneFramedMessageAndWritesTheReplysMessages_ContentTypeAndMessageFraming()
    {
        MemoryStream output = new();
        HttpRequestOptions http = new() { Headers = ["X-Own: 1"], Body = new BytesBody(new byte[] { 0x08, 0x01 }, "application/x-www-form-urlencoded") };
        ITransferContext? sent = null;

        TransferResult result = await new GrpcWebTransferHook().ExecuteAsync(Context(output, http), async given =>
        {
            sent = given;
            byte[] reply = [0, 0, 0, 0, 2, 0x10, 0x02, 0, 0, 0, 0, 1, 0x18, .. OkTrailers];
            await given.Output.WriteAsync(reply);
            return Answered(200, "application/grpc-web+proto");
        });

        Assert.IsTrue(result.IsSuccess);
        BytesBody body = (BytesBody)sent!.Http!.Body!;
        Assert.AreEqual("application/grpc-web+proto", body.ContentType);
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0, 2, 0x08, 0x01 }, body.Content.ToArray());
        CollectionAssert.AreEqual(new[] { "X-Own: 1", "Accept: application/grpc-web+proto", "X-Grpc-Web: 1" }, sent.Http.Headers.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x10, 0x02, 0x18 }, output.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_NoDataAndAnAcceptGiven_SendsAnEmptyMessageAndLeavesTheGivenAccept()
    {
        HttpRequestOptions http = new() { Headers = ["accept: application/grpc-web"] };
        ITransferContext? sent = null;

        await new GrpcWebTransferHook().ExecuteAsync(Context(new MemoryStream(), http), async given =>
        {
            sent = given;
            await given.Output.WriteAsync(OkTrailers);
            return Answered(200, "application/grpc-web");
        });

        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0, 0 }, ((BytesBody)sent!.Http!.Body!).Content.ToArray());
        CollectionAssert.AreEqual(new[] { "accept: application/grpc-web", "X-Grpc-Web: 1" }, sent.Http.Headers.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_ATrailersOnlyReply_ChecksTheStatusInTheHeaders_TrailersOnlyResponses()
    {
        TransferResult result = await new GrpcWebTransferHook().ExecuteAsync(Context(new MemoryStream(), new HttpRequestOptions()), _ =>
            ValueTask.FromResult(Answered(200, "application/grpc-web+proto", [new("grpc-status", "12"), new("grpc-message", "unimplemented")])));

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("--grpc-web: the call failed with grpc-status 12: unimplemented", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(new byte[] { 0, 0, 0, 0, 9, 1 }, CurlExitCode.WeirdServerReply, DisplayName = "cut short")]
    [DataRow(new byte[] { 1, 0, 0, 0, 1, 1 }, CurlExitCode.BadContentEncoding, DisplayName = "compressed")]
    public async Task ExecuteAsync_AReplyItCannotRead_FailsKeepingTheReport(byte[] reply, CurlExitCode expected)
    {
        TransferResult answered = Answered(200, "application/grpc-web+proto");

        TransferResult result = await new GrpcWebTransferHook().ExecuteAsync(Context(new MemoryStream(), new HttpRequestOptions()), async given =>
        {
            await given.Output.WriteAsync(reply);
            return answered;
        });

        Assert.AreEqual(expected, result.ExitCode);
        Assert.AreSame(answered.Report, result.Report);
    }

    [TestMethod]
    [DataRow(404, "application/grpc-web+proto", DisplayName = "not 200")]
    [DataRow(200, null, DisplayName = "no content type")]
    [DataRow(200, "text/html", DisplayName = "not gRPC-Web")]
    [DataRow(200, "application/grpc-web-text", DisplayName = "text encoded")]
    public async Task ExecuteAsync_AReplyThatIsNotABinaryGrpcWebOne_WritesItAsReceived(int code, string? contentType)
    {
        await AssertWrittenAsReceived(Answered(code, contentType));
    }

    [TestMethod]
    public async Task ExecuteAsync_AFailedOrUnreportedTransfer_WritesTheBodyAsReceived()
    {
        await AssertWrittenAsReceived(TransferResult.Failure(CurlExitCode.RecvError, "cut"));
        await AssertWrittenAsReceived(TransferResult.Success(4));
    }

    [TestMethod]
    public async Task ExecuteAsync_ATransferThatIsNotHttp_RunsUnchanged()
    {
        TransferContext context = Context(new MemoryStream(), null);
        ITransferContext? sent = null;

        await new GrpcWebTransferHook().ExecuteAsync(context, given =>
        {
            sent = given;
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        Assert.AreSame(context, sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_AFormBody_FailsWithExit2BeforeSending()
    {
        bool ran = false;
        HttpRequestOptions http = new() { Body = new StreamBody(new MemoryStream(), 0, "multipart/form-data") };

        TransferResult result = await new GrpcWebTransferHook().ExecuteAsync(Context(new MemoryStream(), http), _ =>
        {
            ran = true;
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        Assert.IsFalse(ran);
        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
    }

    [TestMethod]
    public void RequestContext_GivesEveryMemberButOutputAndHttpFromTheTransfer()
    {
        TransferContext inner = Context(new MemoryStream(), new HttpRequestOptions());
        MemoryStream output = new();
        HttpRequestOptions http = new();
        GrpcWebRequestContext context = new(inner, output, http);

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

    private static async Task AssertWrittenAsReceived(TransferResult answered)
    {
        MemoryStream output = new();

        TransferResult result = await new GrpcWebTransferHook().ExecuteAsync(Context(output, new HttpRequestOptions()), async given =>
        {
            await given.Output.WriteAsync(Encoding.ASCII.GetBytes("oops"));
            return answered;
        });

        Assert.AreSame(answered, result);
        Assert.AreEqual("oops", Encoding.ASCII.GetString(output.ToArray()));
    }

    private static TransferResult Answered(int code, string? contentType, IReadOnlyList<KeyValuePair<string, string>>? headers = null) =>
        TransferResult.Success(20) with { Report = new TransferReport { ResponseCode = code, ContentType = contentType, ResponseHeaders = headers ?? [] } };

    private static TransferContext Context(Stream output, HttpRequestOptions? http) => new()
    {
        Url = CurlUrl.Parse("http://example.com/helloworld.Greeter/SayHello"),
        Output = output,
        HeaderOutput = new MemoryStream(),
        DumpHeaderOutput = new MemoryStream(),
        Http = http,
    };
}
