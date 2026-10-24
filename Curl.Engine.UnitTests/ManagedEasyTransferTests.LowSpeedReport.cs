using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Network;
using Curl.Console;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

public sealed partial class ManagedEasyTransferTests
{
    [TestMethod]
    public async Task PerformAsync_StalledBelowSpeedLimitAfterTheHead_ReportsTheSizesAndCodeSeen()
    {
        SteppingTimeProvider clock = new();
        TaskCompletionSource stalled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingProtocolHandler handler = new("http", async context =>
        {
            context.Events.ReportRequestHeader(new byte[62]);
            context.Events.ReportResponseHeader(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n"));
            context.Events.ReportResponseHeader(new byte[59]);
            stalled.TrySetResult();
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return TransferResult.Success(0);
        });
        await using ManagedEasyTransfer easy = EasyOn(clock, handler);
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Transfer = new EasyTransferOptions { LowSpeedLimit = 100, LowSpeedTime = TimeSpan.FromSeconds(2) },
        };

        Task<CurlCode> perform = easy.PerformAsync(CancellationToken.None).AsTask();
        await stalled.Task;
        clock.Advance(TimeSpan.FromSeconds(3));
        CurlCode code = await perform;

        Assert.AreEqual(CurlCode.OperationTimedOut, code);
        Assert.AreEqual(200, easy.Info.ResponseCode);
        Assert.AreEqual(76, easy.Info.HeaderSize);
        Assert.AreEqual(62, easy.Info.RequestSize);
    }
}
