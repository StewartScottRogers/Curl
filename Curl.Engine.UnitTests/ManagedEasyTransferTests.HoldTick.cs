using System.Net;
using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Network;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

// The progress callback called about once a second while a server holds a transfer, with the time elapsed so far
// on CURLINFO_TOTAL_TIME_T (BL-5796).
public sealed partial class ManagedEasyTransferTests
{
    [TestMethod]
    public async Task PerformAsync_ServerHoldsTheResponse_CallsXferinfoEachSecondWithElapsedTimeAndAbortEnds42()
    {
        TimerManualTimeProvider clock = new();
        ManagedCurlEngine engine = new(clock, (_, _, cookies) => CreateDispatcher(new HoldingConnector(), cookies), readEnvironmentVariable: _ => null);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();
        List<TimeSpan> totalTimes = [];
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/slow" },
            Behaviour = new EasyBehaviourOptions { NoProgress = false },
            Callbacks = new()
            {
                TransferInfo = (_, _, _, _) =>
                {
                    lock (totalTimes)
                    {
                        totalTimes.Add(easy.Info.TotalTime);
                        return totalTimes.Count >= 3 ? 1 : 0;
                    }
                },
            },
        };

        Task<CurlCode> perform = easy.PerformAsync(CancellationToken.None).AsTask();
        while (!perform.IsCompleted)
        {
            clock.Advance(1000);
            await Task.Delay(1);
        }

        CurlCode code = await perform.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.AreEqual(CurlCode.AbortedByCallback, code);
        Assert.IsGreaterThanOrEqualTo(3, totalTimes.Count);
        Assert.IsGreaterThan(TimeSpan.Zero, totalTimes[^1]);
        Assert.IsGreaterThan(totalTimes[0], totalTimes[^1]);
    }

    private sealed class HoldingConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Connected(new HoldingConnection(), null));
    }

    private sealed class HoldingConnection : IConnection
    {
        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            await new TaskCompletionSource().Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
