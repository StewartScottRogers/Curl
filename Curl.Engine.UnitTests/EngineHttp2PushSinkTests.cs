using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options;
using Curl.Original.Protocol.Http;

namespace Curl.Engine;

/// <summary>
/// The push sink the engine gives the HTTP handler of a transfer in a multi handle with
/// <c>CURLMOPT_PUSHFUNCTION</c> set, and the pushed transfer it adds (BL-2469, ADR-0605).
/// </summary>
[TestClass]
public sealed class EngineHttp2PushSinkTests
{
    private static readonly string[] PromiseHeaders = [":method:GET", ":scheme:https", ":authority:h.example", ":path:/style.css"];

    [TestMethod]
    public async Task TryAcceptPush_CallbackAnswersOk_OffersTheHeadersAndAddsADuplicateThatReadsThePushedResponse()
    {
        await using ManagedCurlEngine engine = new();
        await using ManagedMultiTransfer multi = (ManagedMultiTransfer)engine.CreateMulti();
        List<(IEasyTransfer Parent, IEasyTransfer Pushed, IReadOnlyList<string> Headers)> offers = [];
        multi.Options = new MultiOptions { Push = (parent, pushed, headers) => { offers.Add((parent, pushed, headers)); return CurlPushResult.Ok; } };
        ManagedEasyTransfer parent = (ManagedEasyTransfer)engine.CreateEasy();
        multi.Add(parent);
        EngineHttp2PushSink sink = new(parent, multi);

        bool taken = sink.TryAcceptPush(PromiseHeaders, new FakePushedResponse("HTTP/2 200 \r\ncontent-type: text/css\r\n\r\nbody{}"));

        Assert.IsTrue(taken);
        Assert.HasCount(1, offers);
        Assert.AreSame(parent, offers[0].Parent);
        CollectionAssert.AreEqual(PromiseHeaders, offers[0].Headers.ToArray());
        ManagedEasyTransfer pushed = (ManagedEasyTransfer)offers[0].Pushed;
        Assert.IsNotNull(pushed.PushedResponse);
        CollectionAssert.AreEqual(new IEasyTransfer[] { parent, pushed }, multi.GetHandles().ToArray());
    }

    [TestMethod]
    public async Task PerformAsync_PushedTransfer_GivesTheHeadToTheHeaderCallbackAndTheBodyToTheWriteCallback()
    {
        await using ManagedCurlEngine engine = new();
        ManagedEasyTransfer parent = (ManagedEasyTransfer)engine.CreateEasy();
        List<string> headerLines = [];
        StringBuilder body = new();
        parent.Options = new EasyOptions
        {
            Callbacks = new EasyCallbackOptions
            {
                Header = bytes => { headerLines.Add(Encoding.ASCII.GetString(bytes)); return bytes.Length; },
                Write = bytes => { body.Append(Encoding.ASCII.GetString(bytes)); return bytes.Length; },
            },
        };
        ManagedEasyTransfer pushed = parent.DuplicateForPush(new FakePushedResponse("HTTP/2 200 \r\ncontent-type: text/css\r\n\r\nbody{}", chunkSize: 5));

        CurlCode code = await pushed.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        CollectionAssert.AreEqual(new[] { "HTTP/2 200 \r\n", "content-type: text/css\r\n", "\r\n" }, headerLines);
        Assert.AreEqual("body{}", body.ToString());
        Assert.IsNull(pushed.PushedResponse);
        Assert.IsNull(parent.PushedResponse);
    }

    [TestMethod]
    public async Task PerformAsync_PushedStreamFails_AnswersRecvErrorWithTheFailure()
    {
        await using ManagedCurlEngine engine = new();
        ManagedEasyTransfer pushed = ((ManagedEasyTransfer)engine.CreateEasy()).DuplicateForPush(new FakePushedResponse(new IOException("stream reset")));

        CurlCode code = await pushed.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.RecvError, code);
        Assert.AreEqual("stream reset", pushed.ErrorText);
    }

    [TestMethod]
    public async Task PerformAsync_PushedTransferWriteCallbackTakesTooFew_AnswersWriteError()
    {
        await using ManagedCurlEngine engine = new();
        ManagedEasyTransfer parent = (ManagedEasyTransfer)engine.CreateEasy();
        parent.Options = new EasyOptions { Callbacks = new EasyCallbackOptions { Write = _ => 0 } };
        ManagedEasyTransfer pushed = parent.DuplicateForPush(new FakePushedResponse("HTTP/2 200 \r\n\r\nx"));

        CurlCode code = await pushed.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.WriteError, code);
        Assert.IsNotEmpty(pushed.ErrorText);
    }

    [TestMethod]
    public async Task TryAcceptPush_CallbackAnswersErrorOut_RefusesThePushAndEndsTheParentWithHttp2()
    {
        await using ManagedCurlEngine engine = new();
        await using ManagedMultiTransfer multi = (ManagedMultiTransfer)engine.CreateMulti();
        multi.Options = new MultiOptions { Push = (_, _, _) => CurlPushResult.ErrorOut };
        ManagedEasyTransfer parent = (ManagedEasyTransfer)engine.CreateEasy();
        multi.Add(parent);
        EngineHttp2PushSink sink = new(parent, multi);

        bool taken = sink.TryAcceptPush(PromiseHeaders, new FakePushedResponse(string.Empty));
        await PerformUntilDoneAsync(multi);

        Assert.IsFalse(taken);
        CollectionAssert.AreEqual(new IEasyTransfer[] { parent }, multi.GetHandles().ToArray());
        Assert.IsTrue(multi.TryReadInfo(out CurlMultiMessage? message, out _));
        Assert.AreEqual(new CurlMultiMessage(CurlMultiMessageType.Done, parent, CurlCode.Http2), message);
    }

    [TestMethod]
    public async Task TryAcceptPush_CallbackAnswersDeny_RefusesThePushAndAddsNothing()
    {
        await using ManagedCurlEngine engine = new();
        await using ManagedMultiTransfer multi = (ManagedMultiTransfer)engine.CreateMulti();
        multi.Options = new MultiOptions { Push = (_, _, _) => CurlPushResult.Deny };
        ManagedEasyTransfer parent = (ManagedEasyTransfer)engine.CreateEasy();
        multi.Add(parent);

        bool taken = new EngineHttp2PushSink(parent, multi).TryAcceptPush(PromiseHeaders, new FakePushedResponse(string.Empty));

        Assert.IsFalse(taken);
        CollectionAssert.AreEqual(new IEasyTransfer[] { parent }, multi.GetHandles().ToArray());
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task PerformAsync_TransferInAMultiWithOrWithoutAPushCallback_RunsToItsOwnResult(bool setsPush)
    {
        await using ManagedCurlEngine engine = new();
        await using ManagedMultiTransfer multi = (ManagedMultiTransfer)engine.CreateMulti();
        multi.Options = setsPush ? new MultiOptions { Push = (_, _, _) => CurlPushResult.Ok } : new MultiOptions();
        ManagedEasyTransfer transfer = (ManagedEasyTransfer)engine.CreateEasy();
        transfer.Options = new EasyOptions { Network = new() { Url = "file:///no-such-dir-bl2469/missing.txt" } };
        multi.Add(transfer);

        await PerformUntilDoneAsync(multi);

        Assert.IsTrue(multi.TryReadInfo(out CurlMultiMessage? message, out _));
        Assert.AreEqual(CurlCode.FileCouldntReadFile, message!.Result);
    }

    private static async Task PerformUntilDoneAsync(ManagedMultiTransfer multi)
    {
        int running;
        do
        {
            (_, running) = await multi.PerformAsync(CancellationToken.None);
            await multi.WaitAsync(Memory<Curl.Api.EngineContracts.Multi.CurlWaitSocket>.Empty, TimeSpan.FromMilliseconds(100), CancellationToken.None);
        }
        while (running > 0);
    }

    /// <summary>A pushed response over fixed bytes, read in chunks, or one whose read fails.</summary>
    private sealed class FakePushedResponse : IHttpPushedResponse
    {
        public FakePushedResponse(string content, int chunkSize = 4096) => Content = new ChunkedReadStream(Encoding.ASCII.GetBytes(content), chunkSize);

        public FakePushedResponse(IOException failure) => Content = new FailingReadStream(failure);

        public int StreamId => 2;

        public Stream Content { get; }
    }

    private sealed class ChunkedReadStream(byte[] bytes, int chunkSize) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, chunkSize)], cancellationToken);
    }

    private sealed class FailingReadStream(IOException failure) : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(failure);
    }
}
