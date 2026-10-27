using System.Collections.Concurrent;
using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options;
using Curl.Api.Options.Network;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// CGR-146: a multi or easy call made from inside a transfer's callback is refused as libcurl 8.22.0's
/// <c>Curl_is_in_callback</c> check refuses it: every multi call answers <c>CURLM_RECURSIVE_API_CALL</c>
/// (info_read no message, wakeup still <c>CURLM_OK</c>), and <c>curl_easy_perform</c> of the handle whose
/// callback runs answers <c>CURLE_RECURSIVE_API_CALL</c>; a perform of another easy handle still runs.
/// </summary>
[TestClass]
public sealed class Cgr146Tests
{
    [TestMethod]
    public async Task MultiCalls_FromInsideAWriteCallback_AreRefusedAndOnlyTheUnrelatedEasyPerformRuns()
    {
        RecordingProtocolHandler handler = new();
        await using ManagedCurlEngine engine = EngineOver(handler);
        await using IMultiTransfer multi = engine.CreateMulti();
        await using IEasyTransfer e1 = engine.CreateEasy();
        await using IEasyTransfer e2 = engine.CreateEasy();
        await using IEasyTransfer e3 = engine.CreateEasy();
        e2.Options = OptionsFor("/two", data => data.Length);
        e3.Options = OptionsFor("/three", data => data.Length);
        List<string> answers = [];
        ValueTask<(CurlMultiCode Code, int Count)>[] multiCalls = [];
        ValueTask<CurlCode> otherPerform = ValueTask.FromResult(CurlCode.FailedInit);
        bool called = false;
        e1.Options = OptionsFor("/one", data =>
        {
            if (!called)
            {
                called = true;
                ValueTask<(CurlMultiCode, int)> perform = multi.PerformAsync(CancellationToken.None);
                answers.Add($"add {multi.Add(e2)}");
                answers.Add($"add again {multi.Add(e1)}");
                answers.Add($"remove {multi.Remove(e1)}");
                otherPerform = e3.PerformAsync(CancellationToken.None);
                multiCalls =
                [
                    perform,
                    multi.WaitAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromSeconds(1), CancellationToken.None),
                    multi.PollAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromSeconds(1), CancellationToken.None),
                    multi.SocketActionAsync(CurlMultiSockets.Timeout, default, CancellationToken.None),
                ];
                answers.Add($"timeout {multi.GetTimeout().Code}");
                answers.Add($"info_read {multi.TryReadInfo(out _, out _)}");
                answers.Add($"wakeup {multi.Wakeup()}");
            }

            return data.Length;
        });
        multi.Add(e1);

        List<CurlMultiMessage> messages = await DriveUntilIdleAsync(multi);
        foreach (ValueTask<(CurlMultiCode Code, int Count)> call in multiCalls)
        {
            answers.Add($"multi {(await call).Code}");
        }

        answers.Add($"easy_perform {await otherPerform}");

        CollectionAssert.AreEqual(
            new[]
            {
                "add RecursiveApiCall", "add again RecursiveApiCall", "remove RecursiveApiCall", "timeout RecursiveApiCall", "info_read False", "wakeup Ok",
                "multi RecursiveApiCall", "multi RecursiveApiCall", "multi RecursiveApiCall", "multi RecursiveApiCall", "easy_perform Ok",
            },
            answers);
        CollectionAssert.AreEqual(new[] { new CurlMultiMessage(CurlMultiMessageType.Done, e1, CurlCode.Ok) }, messages);
        CollectionAssert.AreEquivalent(new[] { "/one", "/three" }, handler.Paths.ToArray());
    }

    [TestMethod]
    public async Task EasyPerform_OfItselfFromInsideItsWriteCallback_AnswersRecursiveApiCallAndAnotherHandleRuns()
    {
        RecordingProtocolHandler handler = new();
        await using ManagedCurlEngine engine = EngineOver(handler);
        await using IEasyTransfer e1 = engine.CreateEasy();
        await using IEasyTransfer e2 = engine.CreateEasy();
        e2.Options = OptionsFor("/two", data => data.Length);
        ValueTask<CurlCode> self = ValueTask.FromResult(CurlCode.FailedInit);
        ValueTask<CurlCode> other = ValueTask.FromResult(CurlCode.FailedInit);
        bool called = false;
        e1.Options = OptionsFor("/one", data =>
        {
            if (!called)
            {
                called = true;
                self = e1.PerformAsync(CancellationToken.None);
                other = e2.PerformAsync(CancellationToken.None);
            }

            return data.Length;
        });

        CurlCode outer = await e1.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, outer);
        Assert.AreEqual(CurlCode.RecursiveApiCall, await self);
        Assert.AreEqual(CurlCode.Ok, await other);
        CollectionAssert.AreEqual(new[] { "/one", "/two" }, handler.Paths.ToArray());
    }

    private static EasyOptions OptionsFor(string path, WriteCallback write) =>
        new() { Network = new EasyNetworkOptions { Url = "http://h" + path }, Callbacks = new EasyCallbackOptions { Write = write } };

    // The probe's loop: perform and wait until no transfer runs, keeping every message read.
    private static async Task<List<CurlMultiMessage>> DriveUntilIdleAsync(IMultiTransfer multi)
    {
        List<CurlMultiMessage> messages = [];
        DateTime deadline = DateTime.UtcNow.AddSeconds(60);
        int running;
        do
        {
            (_, running) = await multi.PerformAsync(CancellationToken.None);
            while (multi.TryReadInfo(out CurlMultiMessage? message, out _))
            {
                messages.Add(message);
            }

            await multi.WaitAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromMilliseconds(100), CancellationToken.None);
        }
        while ((running > 0 || messages.Count == 0) && DateTime.UtcNow < deadline);
        return messages;
    }

    private static ManagedCurlEngine EngineOver(RecordingProtocolHandler handler) =>
        new(TimeProvider.System, (_, _, _) => new ProtocolDispatcher([handler]), readEnvironmentVariable: _ => null);

    // The probe's server: records each request's path and answers it with a 5-byte body.
    private sealed class RecordingProtocolHandler : IProtocolHandler
    {
        public ConcurrentQueue<string> Paths { get; } = new();

        public IReadOnlyCollection<string> SupportedSchemes { get; } = ["http"];

        public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            Paths.Enqueue(context.Url.AbsolutePath);
            await context.Output.WriteAsync(Encoding.ASCII.GetBytes("hello"), context.CancellationToken);
            return TransferResult.Success(5);
        }
    }
}
