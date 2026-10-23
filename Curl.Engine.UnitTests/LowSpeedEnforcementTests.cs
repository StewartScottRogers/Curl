using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Network;
using Curl.Console;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins <c>-y</c>/<c>-Y</c> ending a transfer end to end through <see cref="ManagedEasyTransfer" /> as the curl
/// tool's runner performs it (BL-3163): the handler writes 10 bytes and stalls, as Stewart's server that sends 10
/// bytes and pauses 3 s, and under <c>-y 2 -Y 100000</c> the perform ends with exit 28 and curl's message once 2
/// seconds have passed since the transfer started (curl 8.21.0, measured on Windows 2026-10-22: exit 28 after about
/// 2.2 s). The watchdog wraps the context's output in the shared path, so it holds for every scheme alike.
/// </summary>
[TestClass]
public sealed class LowSpeedEnforcementTests
{
    [TestMethod]
    [DataRow("http", DisplayName = "http")]
    [DataRow("ftp", DisplayName = "ftp")]
    public async Task PerformAsync_TenBytesThenAStallUnderTheSpeedLimit_EndsWithExit28AfterTheSpeedTime(string scheme)
    {
        SteppingTimeProvider clock = new();
        TenBytesThenStallingProtocolHandler handler = new(scheme);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)new ManagedCurlEngine(clock, (_, _, _) => new ProtocolDispatcher([handler])).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = $"{scheme}://h/x" },
            Transfer = new EasyTransferOptions { LowSpeedLimit = 100000, LowSpeedTime = TimeSpan.FromSeconds(2) },
        };
        easy.ToolProgress = new EngineToolTransferProgress(NoTransferProgress.Instance, CancellationToken.None);

        Task<CurlCode> perform = easy.PerformAsync(CancellationToken.None).AsTask();
        await handler.Stalled;
        clock.Advance(TimeSpan.FromSeconds(1));
        clock.Advance(TimeSpan.FromSeconds(1));
        CurlCode code = await perform;

        Assert.AreEqual(CurlCode.OperationTimedOut, code);
        Assert.AreEqual("Operation too slow. Less than 100000 bytes/sec transferred the last 2 seconds", easy.ErrorText);
    }

    // Writes 10 bytes to the output, then waits until the transfer is cancelled, as a server that pauses.
    private sealed class TenBytesThenStallingProtocolHandler(string scheme) : IProtocolHandler
    {
        private readonly TaskCompletionSource stalled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Stalled => stalled.Task;

        public IReadOnlyCollection<string> SupportedSchemes { get; } = [scheme];

        public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            await context.Output.WriteAsync(new byte[10], context.CancellationToken);
            stalled.TrySetResult();
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return TransferResult.Success(10);
        }
    }
}
