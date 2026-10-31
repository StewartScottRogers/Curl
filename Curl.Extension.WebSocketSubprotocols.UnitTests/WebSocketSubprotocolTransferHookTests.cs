using System.Text;
using Curl.WebSocket;

namespace Curl.Extension.WebSocketSubprotocols;

[TestClass]
public sealed class WebSocketSubprotocolTransferHookTests
{
    [TestMethod]
    public async Task ExecuteAsync_AWsTransfer_AsksForTheSubprotocolAndUploadsOneTextFramePerMessage_Rfc6455Section4_1()
    {
        TransferContext context = Context("ws://broker.example/stomp", new HttpRequestOptions { Headers = ["X-Own: 1"] });
        ITransferContext? sent = null;
        WsUploadMessage[] uploaded = [];
        long uploadLength = -1;
        TransferResult answered = TransferResult.Success(0);

        TransferResult result = await new WebSocketSubprotocolTransferHook("v12.stomp /queue/a").ExecuteAsync(context, given =>
        {
            sent = given;
            uploaded = [.. ((IWsUploadMessageSource)given.Upload!).Messages];
            uploadLength = given.Upload!.Length;
            return ValueTask.FromResult(answered);
        });

        Assert.AreSame(answered, result);
        CollectionAssert.AreEqual(new[] { "X-Own: 1", "Sec-WebSocket-Protocol: v12.stomp" }, sent!.Http!.Headers.ToArray());
        Assert.AreSame(context.Output, sent.Output);
        Assert.AreEqual(0, uploadLength);
        Assert.HasCount(2, uploaded);
        Assert.IsTrue(uploaded.All(message => message.Opcode == WsOpcode.Text));
        Assert.AreEqual("CONNECT\naccept-version:1.2\nhost:broker.example\n\n\0", Encoding.UTF8.GetString(uploaded[0].Payload.Span));
        Assert.AreEqual("SUBSCRIBE\nid:0\ndestination:/queue/a\n\n\0", Encoding.UTF8.GetString(uploaded[1].Payload.Span));
    }

    [TestMethod]
    public async Task ExecuteAsync_AWssTransfer_IsHookedToo()
    {
        ITransferContext? sent = null;

        await new WebSocketSubprotocolTransferHook("wamp.2.json realm1").ExecuteAsync(Context("WSS://router.example/ws", new HttpRequestOptions()), given =>
        {
            sent = given;
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        CollectionAssert.AreEqual(new[] { "Sec-WebSocket-Protocol: wamp.2.json" }, sent!.Http!.Headers.ToArray());
    }

    [TestMethod]
    [DataRow("http://example.com/", true)]
    [DataRow("ws://example.com/", false)]
    public async Task ExecuteAsync_ATransferThatIsNotWebSocket_RunsUnchanged(string url, bool http)
    {
        TransferContext context = Context(url, http ? new HttpRequestOptions() : null);
        ITransferContext? sent = null;

        await new WebSocketSubprotocolTransferHook("v12.stomp").ExecuteAsync(context, given =>
        {
            sent = given;
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        Assert.AreSame(context, sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_AValueThatNamesNoSubprotocol_FailsWithExit2BeforeSending()
    {
        bool ran = false;

        TransferResult result = await new WebSocketSubprotocolTransferHook("mqtt").ExecuteAsync(Context("ws://example.com/", new HttpRequestOptions()), _ =>
        {
            ran = true;
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        Assert.IsFalse(ran);
        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        Assert.AreEqual(2, (int)result.ExitCode);
        StringAssert.StartsWith(result.ErrorMessage, "--ws-subprotocol: 'mqtt'");
    }

    [TestMethod]
    public void Context_GivesEveryMemberButUploadAndHttpFromTheTransfer()
    {
        TransferContext inner = Context("ws://example.com/", new HttpRequestOptions());
        MemoryStream upload = new();
        HttpRequestOptions http = new();
        WebSocketSubprotocolContext context = new(inner, upload, http);

        foreach (System.Reflection.PropertyInfo property in typeof(ITransferContext).GetProperties())
        {
            object? expected = property.Name switch
            {
                nameof(ITransferContext.Upload) => upload,
                nameof(ITransferContext.Http) => http,
                _ => property.GetValue(inner),
            };
            Assert.AreEqual(expected, property.GetValue(context), property.Name);
        }
    }

    private static TransferContext Context(string url, HttpRequestOptions? http) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = new MemoryStream(),
        HeaderOutput = new MemoryStream(),
        DumpHeaderOutput = new MemoryStream(),
        Http = http,
    };
}
