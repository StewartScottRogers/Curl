using Curl.Api.Callbacks;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>Pins which escaping exceptions <c>FollowUnlessAbortedAsync</c> tells the handler are a read callback's abort (AF-0796, BL-5247).</summary>
[TestClass]
public sealed class ManagedEasyTransferFollowUnlessAbortedTests
{
    private const string Url = "http://127.0.0.1:18282/a";

    [TestMethod]
    public async Task FollowUnlessAbortedAsync_IOExceptionAndNoReadAborted_TellsTheHandlerFailedInitAndRethrows_AF0796()
    {
        using CallbackReadStream notAborted = new(_ => 0);
        FailingHandler handler = new(new IOException("connection reset"));

        await Assert.ThrowsExactlyAsync<IOException>(async () => await FollowAsync(handler, notAborted));

        Assert.AreEqual(CurlExitCode.FailedInit, handler.Told!.ExitCode);
    }

    [TestMethod]
    public async Task FollowUnlessAbortedAsync_NonIOExceptionAfterAReadAborted_TellsTheHandlerFailedInitAndRethrows_AF0796()
    {
        using CallbackReadStream aborted = await AbortedStreamAsync();
        FailingHandler handler = new(new InvalidOperationException("broke"));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await FollowAsync(handler, aborted));

        Assert.AreEqual(CurlExitCode.FailedInit, handler.Told!.ExitCode);
    }

    [TestMethod]
    public async Task FollowUnlessAbortedAsync_IOExceptionAfterAReadAborted_TellsTheHandlerAbortedByCallbackAndReturnsNull_AF0796()
    {
        using CallbackReadStream aborted = await AbortedStreamAsync();
        FailingHandler handler = new(new IOException(CallbackReadStream.AbortedText));

        TransferResult? result = await FollowAsync(handler, aborted);

        Assert.IsNull(result);
        Assert.AreEqual(CurlExitCode.AbortedByCallback, handler.Told!.ExitCode);
    }

    private static Task<TransferResult?> FollowAsync(FailingHandler handler, CallbackReadStream read) =>
        ManagedEasyTransfer.FollowUnlessAbortedAsync(
            new RedirectFollower(new ProtocolDispatcher([handler])),
            new EngineTransferState(),
            new TransferContext { Url = CurlUrl.Parse(Url), Output = Stream.Null },
            new RedirectPolicy(),
            null,
            [read]);

    private static Task<CallbackReadStream> AbortedStreamAsync()
    {
        CallbackReadStream stream = new(_ => ReadCallbackResult.Abort);
        Assert.ThrowsExactly<IOException>(() => stream.Read(new byte[1], 0, 1));
        return Task.FromResult(stream);
    }

    private sealed class FailingHandler(Exception failure) : IProtocolHandler
    {
        public TransferResult? Told { get; private set; }

        public IReadOnlyCollection<string> SupportedSchemes { get; } = ["http"];

        public ValueTask TransferChainFinishedAsync(ITransferContext first, TransferResult result)
        {
            Told = result;
            return ValueTask.CompletedTask;
        }

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context) => throw failure;
    }
}
