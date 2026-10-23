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
/// A <c>curl_multi_perform</c> and <c>curl_multi_poll</c> (or <c>curl_multi_wait</c>) loop over the
/// engine's multi handle, for a transfer with <c>CURLOPT_TIMEOUT_MS</c> set whose perform, like the
/// engine's, ends with <see cref="CurlCode.OperationTimedOut" /> a moment after its timeout: each wait blocks
/// up to its timeout or the handle's timer, so the loop turns about once per 100 ms and never spins
/// at full CPU between the timeout passing and the transfer ending (BL-3206).
/// </summary>
[TestClass]
public sealed class CurlMultiPollBusyWaitTests
{
    private const int WaitMilliseconds = 100;

    // Far more than a blocking loop turns in the transfer's 1.1 seconds; a spinning one passes it at once.
    private const int SpinningIterations = 1000;

    [TestMethod]
    [DataRow(true, DisplayName = "PollAsync")]
    [DataRow(false, DisplayName = "WaitAsync")]
    public async Task PerformAndWaitLoop_TimeoutMsSetOverOneSecond_TurnsAboutOncePerWait(bool poll)
    {
        await using var multi = NewMulti();
        using var easy = TimedEasy(1000);
        Assert.AreEqual(CurlMultiCode.Ok, multi.AddHandle(easy));

        var (iterations, result) = await RunUntilDoneAsync(multi, poll, WaitMilliseconds);

        Assert.IsLessThanOrEqualTo(20, iterations);
        Assert.AreEqual(CurlCode.OperationTimedOut, result);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "PollAsync")]
    [DataRow(false, DisplayName = "WaitAsync")]
    public async Task Wait_HandleTimeoutSoonerThanTheWait_ReturnsEarlyAndTheTransferFailsWith28(bool poll)
    {
        await using var multi = NewMulti();
        using var easy = TimedEasy(200);
        multi.AddHandle(easy);
        await multi.PerformAsync();

        var clock = Stopwatch.StartNew();
        var first = await WaitOnceAsync(multi, poll, 10_000);
        var firstWait = clock.Elapsed;
        var (_, result) = await RunUntilDoneAsync(multi, poll, 10_000);

        Assert.AreEqual(CurlMultiCode.Ok, first.Code);
        Assert.IsLessThan(TimeSpan.FromSeconds(5), firstWait);
        Assert.AreEqual(CurlCode.OperationTimedOut, result);
    }

    private static async Task<(int Iterations, CurlCode? Result)> RunUntilDoneAsync(CurlMulti multi, bool poll, int waitMilliseconds)
    {
        var iterations = 0;
        while (iterations < SpinningIterations)
        {
            var (_, running) = await multi.PerformAsync();
            if (running == 0)
            {
                return (iterations, multi.InfoRead(out _)?.Result);
            }

            await WaitOnceAsync(multi, poll, waitMilliseconds);
            iterations++;
        }

        return (iterations, null);
    }

    private static ValueTask<(CurlMultiCode Code, int ReadySockets)> WaitOnceAsync(CurlMulti multi, bool poll, int waitMilliseconds) =>
        poll
            ? multi.PollAsync(Memory<CurlWaitSocket>.Empty, waitMilliseconds)
            : multi.WaitAsync(Memory<CurlWaitSocket>.Empty, waitMilliseconds);

    private static CurlMulti NewMulti() =>
        new(new ManagedMultiTransfer(new ConcurrentDictionary<IEasyTransfer, ManagedMultiTransfer>(ReferenceEqualityComparer.Instance), TimeProvider.System, SocketWaitPoller.Instance, () => ValueTask.CompletedTask, _ => ValueTask.CompletedTask, () => { }));

    private static CurlEasy TimedEasy(long timeoutMilliseconds)
    {
        var easy = new CurlEasy(new LateTimingOutTransfer(), EasyOptionDispatch.Default);
        Assert.AreEqual(CurlCode.Ok, easy.SetOption(CurlOption.TimeoutMs, timeoutMilliseconds));
        return easy;
    }

    /// <summary>
    /// A transfer whose perform ends with <see cref="CurlCode.OperationTimedOut" /> 100 ms after its
    /// <c>CURLOPT_TIMEOUT</c>, as the engine's max-time watchdog ends a transfer a moment after the multi
    /// handle's timer for it has passed.
    /// </summary>
    private sealed class LateTimingOutTransfer : IEasyTransfer
    {
        public EasyOptions Options { get; set; } = new();

        public TransferInfo Info { get; } = new();

        public string ErrorText => string.Empty;

        public ITransferHeaders Headers => throw new NotSupportedException();

        public IEasyWebSocket WebSocket => throw new NotSupportedException();

        public IConnectOnlyConnection Connection => throw new NotSupportedException();

        public async ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Options.Transfer.Timeout + TimeSpan.FromMilliseconds(100), cancellationToken);
            return CurlCode.OperationTimedOut;
        }

        public IEasyTransfer Duplicate() => throw new NotSupportedException();

        public void Reset() => throw new NotSupportedException();

        public CurlCode Pause(CurlPause directions) => throw new NotSupportedException();

        public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
