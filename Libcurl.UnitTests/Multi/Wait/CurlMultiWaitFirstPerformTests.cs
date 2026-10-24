using System.Collections.Concurrent;
using System.Diagnostics;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Engine;
using Libcurl.Easy;
using Libcurl.Multi;
using Libcurl.Options;

namespace Libcurl.UnitTests.Multi.Wait;

/// <summary>
/// After the perform that started a transfer, libcurl's socket for it is writable until the next perform, so
/// <c>curl_multi_poll</c> and <c>curl_multi_wait</c> with no extra sockets return at once with numfds 1
/// instead of sleeping the whole timeout (BL-3308, differential case m_in 24).
/// </summary>
[TestClass]
public sealed class CurlMultiWaitFirstPerformTests
{
    [TestMethod]
    [DataRow(true, DisplayName = "PollAsync")]
    [DataRow(false, DisplayName = "WaitAsync")]
    public async Task Wait_AfterTheFirstPerformOfASlowTransfer_ReturnsAtOnceWithOneSocket(bool poll)
    {
        await using var multi = NewMulti();
        using var easy = new CurlEasy(new NeverEndingTransfer(), EasyOptionDispatch.Default);
        multi.AddHandle(easy);
        await multi.PerformAsync();

        var clock = Stopwatch.StartNew();
        var first = await WaitOnceAsync(multi, poll);
        var second = await WaitOnceAsync(multi, poll);

        Assert.AreEqual((CurlMultiCode.Ok, 1), first);
        Assert.AreEqual((CurlMultiCode.Ok, 1), second);
        Assert.IsLessThan(TimeSpan.FromSeconds(5), clock.Elapsed);
    }

    [TestMethod]
    public async Task Poll_AfterASecondPerform_WaitsTheWholeTimeoutWithNoSocketReady()
    {
        await using var multi = NewMulti();
        using var easy = new CurlEasy(new NeverEndingTransfer(), EasyOptionDispatch.Default);
        multi.AddHandle(easy);
        await multi.PerformAsync();
        await multi.PerformAsync();

        var clock = Stopwatch.StartNew();
        var result = await multi.PollAsync(Memory<CurlWaitSocket>.Empty, 300);

        Assert.AreEqual((CurlMultiCode.Ok, 0), result);
        Assert.IsGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(250), clock.Elapsed);
    }

    private static ValueTask<(CurlMultiCode Code, int ReadySockets)> WaitOnceAsync(CurlMulti multi, bool poll) =>
        poll
            ? multi.PollAsync(Memory<CurlWaitSocket>.Empty, 10_000)
            : multi.WaitAsync(Memory<CurlWaitSocket>.Empty, 10_000);

    private static CurlMulti NewMulti() =>
        new(new ManagedMultiTransfer(new ConcurrentDictionary<IEasyTransfer, ManagedMultiTransfer>(ReferenceEqualityComparer.Instance), TimeProvider.System, SocketWaitPoller.Instance, () => ValueTask.CompletedTask, _ => ValueTask.CompletedTask, () => { }));

    /// <summary>A transfer whose perform ends only when it is cancelled, like one waiting on a slow body.</summary>
    private sealed class NeverEndingTransfer : IEasyTransfer
    {
        public EasyOptions Options { get; set; } = new();

        public TransferInfo Info { get; } = new();

        public string ErrorText => string.Empty;

        public ITransferHeaders Headers => throw new NotSupportedException();

        public IEasyWebSocket WebSocket => throw new NotSupportedException();

        public IConnectOnlyConnection Connection => throw new NotSupportedException();

        public async ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return CurlCode.Ok;
        }

        public IEasyTransfer Duplicate() => throw new NotSupportedException();

        public void Reset() => throw new NotSupportedException();

        public CurlCode Pause(CurlPause directions) => throw new NotSupportedException();

        public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
