using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Network;
using Curl.Console;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins a library handle's <c>CURLOPT_LOW_SPEED_LIMIT</c> and <c>CURLOPT_LOW_SPEED_TIME</c> through
/// <see cref="ManagedEasyTransfer" /> with no <see cref="ManagedEasyTransfer.ToolProgress" /> (BL-3196): a transfer
/// that stays below the limit for the time ends with <see cref="CurlCode.OperationTimedOut" /> and libcurl's
/// <c>Operation too slow</c> text, as libcurl 8.22.0's <c>Curl_speedcheck</c> does; one at or above the limit, and one
/// with either option 0, completes.
/// </summary>
[TestClass]
public sealed class LowSpeedLimitTests
{
    [TestMethod]
    public async Task PerformAsync_LibraryHandleBelowTheLimitForTheTime_EndsWithOperationTimedOut()
    {
        SteppingTimeProvider clock = new();
        StallingProtocolHandler handler = new(bytesPerTick: 10, ticks: 5);
        await using ManagedEasyTransfer easy = EasyOver(clock, handler, limit: 100000, seconds: 2);

        Task<CurlCode> perform = easy.PerformAsync(CancellationToken.None).AsTask();
        await handler.Started;
        await handler.AdvanceTicksAsync(clock);
        CurlCode code = await perform;

        Assert.AreEqual(CurlCode.OperationTimedOut, code);
        Assert.AreEqual("Operation too slow. Less than 100000 bytes/sec transferred the last 2 seconds", easy.ErrorText);
    }

    [TestMethod]
    [DataRow(100L, 2L, DisplayName = "above the limit")]
    [DataRow(100000L, 0L, DisplayName = "LOW_SPEED_TIME 0")]
    [DataRow(0L, 2L, DisplayName = "LOW_SPEED_LIMIT 0")]
    public async Task PerformAsync_LibraryHandleNotTooSlow_Completes(long limit, long seconds)
    {
        SteppingTimeProvider clock = new();
        StallingProtocolHandler handler = new(bytesPerTick: 1000, ticks: 5);
        await using ManagedEasyTransfer easy = EasyOver(clock, handler, limit, seconds);

        Task<CurlCode> perform = easy.PerformAsync(CancellationToken.None).AsTask();
        await handler.Started;
        await handler.AdvanceTicksAsync(clock);
        CurlCode code = await perform;

        Assert.AreEqual(CurlCode.Ok, code);
    }

    private static ManagedEasyTransfer EasyOver(SteppingTimeProvider clock, StallingProtocolHandler handler, long limit, long seconds)
    {
        ManagedEasyTransfer easy = (ManagedEasyTransfer)new ManagedCurlEngine(clock, (_, _, _) => new ProtocolDispatcher([handler])).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Behaviour = new EasyBehaviourOptions { NoProgress = true },
            Transfer = new EasyTransferOptions { LowSpeedLimit = limit, LowSpeedTime = TimeSpan.FromSeconds(seconds) },
        };
        return easy;
    }

    // Writes bytesPerTick bytes each time the test advances the clock a second, ticks times, then finishes; a
    // cancelled transfer ends at once.
    private sealed class StallingProtocolHandler(int bytesPerTick, int ticks) : IProtocolHandler
    {
        private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private TaskCompletionSource tick = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private TaskCompletionSource written = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => started.Task;

        public IReadOnlyCollection<string> SupportedSchemes { get; } = ["http"];

        public async Task AdvanceTicksAsync(SteppingTimeProvider clock)
        {
            for (int i = 0; i < ticks && !ended.Task.IsCompleted; i++)
            {
                TaskCompletionSource next = written;
                tick.TrySetResult();
                await Task.WhenAny(next.Task, ended.Task);
                clock.Advance(TimeSpan.FromSeconds(1));
            }
        }

        private readonly TaskCompletionSource ended = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            started.TrySetResult();
            try
            {
                for (int i = 0; i < ticks; i++)
                {
                    await tick.Task.WaitAsync(context.CancellationToken);
                    tick = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    await context.Output.WriteAsync(new byte[bytesPerTick], context.CancellationToken);
                    TaskCompletionSource done = written;
                    written = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    done.TrySetResult();
                }

                return TransferResult.Success((long)bytesPerTick * ticks);
            }
            finally
            {
                ended.TrySetResult();
            }
        }
    }
}
